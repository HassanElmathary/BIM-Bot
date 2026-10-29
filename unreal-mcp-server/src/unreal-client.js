/**
 * Unreal Engine Remote Control API Client
 *
 * Connects to Unreal Engine's built-in HTTP Remote Control API
 * and optionally to the UnrealMCPBridge custom plugin.
 *
 * Remote Control API: http://localhost:30010 (stock UE plugin)
 * MCP Bridge Plugin: http://localhost:30011 (optional custom C++ plugin; every bridge
 *                    method falls back to Python over the Remote Control API without it)
 *
 * Enable Remote Control via: Edit > Plugins > Remote Control API, or run
 * BIM-Bot-Unreal.exe --setup, which also allows the Python calls (see unreal-setup.js)
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const http = require('http');
const { EventEmitter } = require('events');
const { logger: rootLogger } = require('./logger.js');

const log = rootLogger.child('client');

const PYTHON_LIBRARY = '/Script/PythonScriptPlugin.Default__PythonScriptLibrary';

let _reqIdCounter = 0;

/** Same as index.js pyparams: pass values to Python as base64 JSON, never by string splicing */
function pyHead(obj) {
  const b64 = Buffer.from(JSON.stringify(obj)).toString('base64');
  return `import json as _j,base64 as _b\n_p=_j.loads(_b.b64decode('${b64}').decode())`;
}

/**
 * Turn an ExecutePythonCommandEx response into { status, body } where body is what the
 * script printed. A script that raised comes back as status 400 with the traceback.
 */
function parsePythonResult(result) {
  if (result.status >= 400 || !result.body || typeof result.body !== 'object') return result;
  const logs = Array.isArray(result.body.LogOutput) ? result.body.LogOutput : [];
  const text = (type) => logs.filter((l) => type(l.Type)).map((l) => l.Output).join('').trim();
  const printed = text((t) => t === 'Info');
  if (result.body.ReturnValue === false) {
    // CommandResult holds the traceback; keep what was printed before the error
    const errors = [text((t) => t !== 'Info'), result.body.CommandResult].filter(Boolean).join('\n').trim();
    return { status: 400, body: [printed, errors].filter(Boolean).join('\n') || 'Python script failed' };
  }
  return { status: result.status, body: printed || null };
}

class UnrealClient extends EventEmitter {
  /**
   * @param {string} host
   * @param {number} port - Remote Control API port (default 30010)
   * @param {object} [opts]
   * @param {number} [opts.bridgePort=30011] - UnrealMCPBridge plugin port
   * @param {number} [opts.timeout=10000] - Request timeout in ms
   * @param {number} [opts.maxRetries=3] - Max retry attempts for retryable errors
   * @param {number} [opts.retryDelay=500] - Base retry delay in ms (exponential backoff)
   * @param {number} [opts.healthCheckInterval=5000] - Health check interval in ms (0 to disable)
   */
  constructor(host = 'localhost', port = 30010, opts = {}) {
    super();
    this.host = host;
    this.port = port;
    this.bridgePort = opts.bridgePort ?? 30011;
    this.timeout = opts.timeout ?? parseInt(process.env.UNREAL_TIMEOUT || '10000');
    this.maxRetries = opts.maxRetries ?? 3;
    this.retryDelay = opts.retryDelay ?? 500;
    this.healthCheckInterval = opts.healthCheckInterval ?? 5000;

    this.baseUrl = `http://${host}:${port}`;
    this.bridgeUrl = `http://${host}:${this.bridgePort}`;

    // undefined = never attempted, false = was connected but lost, true = connected
    this._connected = undefined;
    this._bridgeAvailable = false;
    this._healthTimer = null;
    this._shuttingDown = false;
  }

  /** Whether the Unreal Remote Control API is reachable */
  get isConnected() { return this._connected === true; }

  /** Whether the custom MCP Bridge plugin is available */
  get isBridgeAvailable() { return this._bridgeAvailable; }

  // ─── Core HTTP ────────────────────────────────────────────────────────────────

  /**
   * Make an HTTP request with retry logic.
   * @param {string} method
   * @param {string} path
   * @param {object|null} body
   * @param {object} [opts] - Override options
   * @param {number} [opts.timeout] - Override timeout for this request
   * @param {number} [opts.port] - Override port for this request
   * @param {boolean} [opts.noRetry] - Skip retry logic
   */
  async request(method, path, body = null, opts = {}) {
    const reqId = `req-${++_reqIdCounter}`;
    const targetPort = opts.port ?? this.port;
    const reqTimeout = opts.timeout ?? this.timeout;
    const bodyStr = body ? JSON.stringify(body) : null;
    const maxAttempts = opts.noRetry ? 1 : this.maxRetries;

    log.debug(`→ ${method} ${path}`, { reqId, port: targetPort, bodySize: bodyStr?.length ?? 0 });

    let lastError = null;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      const startTime = Date.now();

      try {
        const result = await this._doRequest(method, path, bodyStr, targetPort, reqTimeout);
        const latency = Date.now() - startTime;

        log.debug(`← ${result.status} ${path}`, {
          reqId, attempt, latency,
          bodySize: typeof result.body === 'string' ? result.body.length : JSON.stringify(result.body ?? '').length,
        });

        // Don't retry on success or client errors (4xx)
        if (result.status < 500) {
          return result;
        }

        // 5xx → retryable
        lastError = new Error(`Server error ${result.status}: ${JSON.stringify(result.body)}`);
        log.warn(`Retryable server error on attempt ${attempt}/${maxAttempts}`, { reqId, status: result.status });

      } catch (e) {
        lastError = e;
        const latency = Date.now() - startTime;
        log.warn(`Request failed on attempt ${attempt}/${maxAttempts}`, { reqId, latency, error: e.message });
      }

      // Wait before retrying (exponential backoff)
      if (attempt < maxAttempts) {
        const delay = this.retryDelay * Math.pow(2, attempt - 1);
        log.debug(`Retrying in ${delay}ms...`, { reqId, attempt: attempt + 1 });
        await this._sleep(delay);
      }
    }

    throw lastError || new Error('Request failed after all retries');
  }

  /** @private */
  _doRequest(method, path, bodyStr, port, timeout) {
    return new Promise((resolve, reject) => {
      const options = {
        hostname: this.host,
        port,
        path,
        method,
        headers: { 'Content-Type': 'application/json' },
      };
      if (bodyStr) {
        options.headers['Content-Length'] = Buffer.byteLength(bodyStr);
      }

      const req = http.request(options, (res) => {
        let data = '';
        res.on('data', (chunk) => { data += chunk; });
        res.on('end', () => {
          try {
            resolve({ status: res.statusCode, body: data ? JSON.parse(data) : null });
          } catch {
            resolve({ status: res.statusCode, body: data });
          }
        });
      });

      req.on('error', (err) => {
        reject(new Error(
          `Connection to ${this.host}:${port} failed: ${err.message}. ` +
          `Make sure Unreal Engine is running with the required plugins enabled.`
        ));
      });

      req.setTimeout(timeout, () => {
        req.destroy();
        reject(new Error(`Request timed out after ${timeout}ms`));
      });

      if (bodyStr) req.write(bodyStr);
      req.end();
    });
  }

  /** @private */
  _sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  // ─── Convenience Methods ──────────────────────────────────────────────────────

  async get(path, opts) { return this.request('GET', path, null, opts); }
  async put(path, body, opts) { return this.request('PUT', path, body, opts); }
  async post(path, body, opts) { return this.request('POST', path, body, opts); }

  // ─── Health Check ─────────────────────────────────────────────────────────────

  /**
   * Start periodic health checking.
   * Emits 'connected', 'disconnected', 'reconnected' events.
   */
  startHealthCheck(intervalMs) {
    const interval = intervalMs ?? this.healthCheckInterval;
    if (interval <= 0 || this._healthTimer) return;

    log.info('Starting health check', { intervalMs: interval });

    // Initial check
    this._runHealthCheck();

    this._healthTimer = setInterval(() => {
      if (!this._shuttingDown) this._runHealthCheck();
    }, interval);

    // Don't keep the process alive just for the health check
    if (this._healthTimer.unref) this._healthTimer.unref();
  }

  /** Stop the health check loop */
  stopHealthCheck() {
    if (this._healthTimer) {
      clearInterval(this._healthTimer);
      this._healthTimer = null;
      log.info('Health check stopped');
    }
  }

  /** @private */
  async _runHealthCheck() {
    try {
      const result = await this.request('GET', '/remote/info', null, { noRetry: true, timeout: 3000 });
      const wasConnected = this._connected;
      this._connected = result.status === 200;

      if (this._connected && !wasConnected) {
        // undefined = first ever connection, false = reconnection after dropout
        const event = wasConnected === undefined ? 'connected' : 'reconnected';
        log.info(`Unreal Engine ${event}`, { info: result.body });
        this.emit(event, result.body);
      }
    } catch {
      if (this._connected) {
        this._connected = false;
        log.warn('Unreal Engine disconnected');
        this.emit('disconnected');
      }
    }

    // Also check bridge availability (silent, no retry)
    try {
      const bridgeResult = await this.request('GET', '/mcp/info', null, {
        noRetry: true, timeout: 2000, port: this.bridgePort,
      });
      this._bridgeAvailable = bridgeResult.status === 200;
    } catch {
      this._bridgeAvailable = false;
    }
  }

  // ─── Shutdown ─────────────────────────────────────────────────────────────────

  shutdown() {
    this._shuttingDown = true;
    this.stopHealthCheck();
    log.info('Client shut down');
  }

  // ─── Remote Control API Methods ───────────────────────────────────────────────

  /** Check if Unreal is reachable */
  async ping() {
    const result = await this.get('/remote/info');
    return result.status === 200;
  }

  /**
   * Execute a Python script in the Unreal editor. Resolves to { status, body } with body
   * set to what the script printed (ExecutePythonScript would only return true/false).
   */
  async executePython(script) {
    const result = await this.put('/remote/object/call', {
      objectPath: PYTHON_LIBRARY,
      functionName: 'ExecutePythonCommandEx',
      parameters: { PythonCommand: script, ExecutionMode: 'ExecuteFile', FileExecutionScope: 'Private' },
      generateTransaction: false,
    });
    return parsePythonResult(result);
  }

  /** Call a function on a UObject */
  async callFunction(objectPath, functionName, parameters = {}, generateTransaction = true) {
    return this.put('/remote/object/call', {
      objectPath,
      functionName,
      parameters,
      generateTransaction,
    });
  }

  /** Get a property value from a UObject */
  async getProperty(objectPath, propertyName) {
    return this.put('/remote/object/property', {
      objectPath,
      propertyName,
      access: 'READ_ACCESS',
    });
  }

  /** Set a property value on a UObject */
  async setProperty(objectPath, propertyName, propertyValue) {
    return this.put('/remote/object/property', {
      objectPath,
      propertyName,
      propertyValue,
      access: 'WRITE_ACCESS',
    });
  }

  /** Search for assets */
  async searchAssets(query, filter = {}) {
    return this.put('/remote/search/assets', {
      Query: query,
      Filter: filter,
    });
  }

  /** Get all actors in the current level */
  async getActors(classFilter = '') {
    const script = classFilter
      ? `
import unreal
actors = unreal.EditorLevelLibrary.get_all_level_actors()
filtered = [a for a in actors if '${classFilter}' in a.get_class().get_name()]
result = [{'name': a.get_actor_label(), 'class': a.get_class().get_name(), 'path': str(a.get_path_name()), 'location': {'x': a.get_actor_location().x, 'y': a.get_actor_location().y, 'z': a.get_actor_location().z}} for a in filtered]
print(result)
`
      : `
import unreal
actors = unreal.EditorLevelLibrary.get_all_level_actors()
result = [{'name': a.get_actor_label(), 'class': a.get_class().get_name(), 'path': str(a.get_path_name()), 'location': {'x': a.get_actor_location().x, 'y': a.get_actor_location().y, 'z': a.get_actor_location().z}} for a in actors]
print(result)
`;
    return this.executePython(script.trim());
  }

  // ─── MCP Bridge Plugin Methods (port 30011, Python fallback) ─────────────────
  // Each uses the UnrealMCPBridge plugin when it answers, otherwise Python.

  /** Whether Unreal runs on this machine, so files it writes can be read back */
  get _isLocal() {
    return ['localhost', '127.0.0.1', '::1'].includes(this.host);
  }

  /** Capture a viewport screenshot → { status, body: { image: <base64 png> } } */
  async screenshot(opts = {}) {
    const width = opts.width ?? 1920;
    const height = opts.height ?? 1080;
    if (this._bridgeAvailable) {
      return this.post('/mcp/screenshot', { width, height, format: opts.format ?? 'png' },
        { port: this.bridgePort, timeout: 15000 });
    }
    if (!this._isLocal) {
      return { status: 501, body: 'Screenshots without the UnrealMCPBridge plugin need Unreal on this machine' };
    }

    // The editor writes the PNG a frame or two after the call returns, so poll for it
    const file = path.join(os.tmpdir(), `bimbot-shot-${process.pid}-${Date.now()}.png`);
    const result = await this.executePython(`
import unreal
${pyHead({ width, height, file: file.replace(/\\/g, '/') })}
unreal.AutomationLibrary.take_high_res_screenshot(_p['width'], _p['height'], _p['file'])
`.trim());
    if (result.status >= 400) return result;

    const deadline = Date.now() + (opts.timeout ?? 15000);
    let lastSize = -1;
    while (Date.now() < deadline) {
      await this._sleep(250);
      let size;
      try { size = fs.statSync(file).size; } catch { continue; }
      // Wait until the size stops changing so the PNG is fully written
      if (size > 0 && size === lastSize) {
        const image = fs.readFileSync(file).toString('base64');
        fs.rmSync(file, { force: true });
        return { status: 200, body: { image } };
      }
      lastSize = size;
    }
    return { status: 504, body: 'Timed out waiting for the screenshot; is the editor window minimized?' };
  }

  /** Execute a console command */
  async consoleCommand(command) {
    if (this._bridgeAvailable) {
      return this.post('/mcp/console', { command }, { port: this.bridgePort });
    }
    return this.executePython(`
import unreal
${pyHead({ command })}
world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
unreal.SystemLibrary.execute_console_command(world, _p['command'])
print({'success': True, 'command': _p['command']})
`.trim());
  }

  /**
   * Show an editor notification. Python has no toast API, so without the bridge the
   * message goes to the Output Log at the matching severity.
   */
  async notify(message, severity = 'info', duration = 5.0) {
    if (this._bridgeAvailable) {
      return this.post('/mcp/notify', { message, severity, duration }, { port: this.bridgePort });
    }
    return this.executePython(`
import unreal
${pyHead({ message, severity })}
{'warning': unreal.log_warning, 'error': unreal.log_error}.get(_p['severity'], unreal.log)('[BIM-Bot] ' + _p['message'])
print({'success': True, 'shown_in': 'Output Log', 'note': 'toast pop-ups need the UnrealMCPBridge plugin'})
`.trim());
  }

  /** Get project metadata: engine version, project name and file, enabled plugins */
  async getProjectInfo() {
    if (this._bridgeAvailable) {
      return this.get('/mcp/project', { port: this.bridgePort });
    }
    return this.executePython(`
import unreal, json
project_file = unreal.Paths.convert_relative_path_to_full(unreal.Paths.get_project_file_path())
try:
    with open(project_file, encoding='utf-8-sig') as f:
        uproject = json.load(f)
except Exception:
    uproject = {}
print(json.dumps({
    'project_name': unreal.SystemLibrary.get_game_name(),
    'project_file': project_file,
    'project_dir': unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir()),
    'engine_version': unreal.SystemLibrary.get_engine_version(),
    'engine_association': uproject.get('EngineAssociation'),
    'plugins': [p['Name'] for p in uproject.get('Plugins', []) if p.get('Enabled')],
}, indent=2))
`.trim());
  }

  /** Import a file into the content browser */
  async importAsset(filePath, destinationPath) {
    if (this._bridgeAvailable) {
      return this.post('/mcp/import', { filePath, destinationPath }, {
        port: this.bridgePort,
        timeout: 30000, // asset imports can be slow
      });
    }
    const result = await this.executePython(`
import unreal, json
${pyHead({ filePath, destinationPath })}
task = unreal.AssetImportTask()
task.filename = _p['filePath']
task.destination_path = _p['destinationPath']
task.automated = True
task.save = True
task.replace_existing = True
unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
print(json.dumps({'imported': [str(p) for p in task.imported_object_paths]}))
`.trim());
    if (result.status < 400 && /"imported": \[\]/.test(result.body || '')) {
      return { status: 400, body: `Nothing was imported from ${filePath} (missing file or unsupported format?)` };
    }
    return result;
  }
}

module.exports = { UnrealClient, parsePythonResult };
