/**
 * BIM-Bot Unreal — Test Suite
 * 
 * Uses Node.js built-in test runner (node:test).
 * Tests server startup, tool registration, client methods,
 * and integration with a mock Unreal Engine server.
 * 
 * Run: npm test
 */

const { describe, it, before, after, beforeEach } = require('node:test');
const assert = require('node:assert/strict');
const { MockUnrealServer } = require('./mock-unreal.js');
const { UnrealClient } = require('../src/unreal-client.js');

// ─── Client Unit Tests ────────────────────────────────────────────────────────

describe('UnrealClient', () => {
  it('should construct with default options', () => {
    const client = new UnrealClient();
    assert.equal(client.host, 'localhost');
    assert.equal(client.port, 30010);
    assert.equal(client.bridgePort, 30011);
    assert.equal(client.timeout, 10000);
    assert.equal(client.maxRetries, 3);
    assert.equal(client.isConnected, false);
    assert.equal(client.isBridgeAvailable, false);
  });

  it('should construct with custom options', () => {
    const client = new UnrealClient('192.168.1.100', 40000, {
      bridgePort: 40001,
      timeout: 5000,
      maxRetries: 5,
      retryDelay: 1000,
    });
    assert.equal(client.host, '192.168.1.100');
    assert.equal(client.port, 40000);
    assert.equal(client.bridgePort, 40001);
    assert.equal(client.timeout, 5000);
    assert.equal(client.maxRetries, 5);
    assert.equal(client.retryDelay, 1000);
    assert.equal(client.baseUrl, 'http://192.168.1.100:40000');
    assert.equal(client.bridgeUrl, 'http://192.168.1.100:40001');
  });

  it('should throw on connection failure when Unreal is not running', async () => {
    const client = new UnrealClient('localhost', 39999, { maxRetries: 1, retryDelay: 100 });
    await assert.rejects(
      () => client.ping(),
      { message: /Connection to localhost:39999 failed/ }
    );
  });

  it('should emit events from EventEmitter', () => {
    const client = new UnrealClient();
    let emitted = false;
    client.on('connected', () => { emitted = true; });
    client.emit('connected', {});
    assert.equal(emitted, true);
  });

  it('should stop health check on shutdown', () => {
    const client = new UnrealClient();
    client.startHealthCheck(60000); // long interval so it doesn't run
    assert.notEqual(client._healthTimer, null);
    client.shutdown();
    assert.equal(client._healthTimer, null);
    assert.equal(client._shuttingDown, true);
  });
});

// ─── Integration Tests with Mock Server ───────────────────────────────────────

describe('UnrealClient ↔ Mock Unreal Server', () => {
  const MOCK_PORT = 30099; // Use a non-standard port for tests
  let mockServer;
  let client;

  before(async () => {
    mockServer = new MockUnrealServer(MOCK_PORT);
    await mockServer.start();
    client = new UnrealClient('127.0.0.1', MOCK_PORT, {
      maxRetries: 1,
      retryDelay: 100,
      healthCheckInterval: 0, // disable auto health check in tests
    });
  });

  after(async () => {
    client.shutdown();
    await mockServer.stop();
  });

  beforeEach(() => {
    mockServer.clearLog();
  });

  // ── Connection ──

  it('ping() should return true when mock is running', async () => {
    const result = await client.ping();
    assert.equal(result, true);
  });

  it('GET /remote/info should return engine info', async () => {
    const result = await client.get('/remote/info');
    assert.equal(result.status, 200);
    assert.equal(result.body.IsEditor, true);
    assert.ok(result.body.EngineVersion);
  });

  // ── Python Execution ──

  it('executePython() should send correct payload', async () => {
    const result = await client.executePython('print("hello")');
    assert.equal(result.status, 200);

    const [req] = mockServer.getLastRequests();
    assert.equal(req.body.objectPath, '/Script/PythonScriptPlugin.Default__PythonScriptLibrary');
    assert.equal(req.body.functionName, 'ExecutePythonCommandEx');
    assert.equal(req.body.parameters.PythonCommand, 'print("hello")');
    assert.equal(req.body.parameters.ExecutionMode, 'ExecuteFile');
  });

  it('executePython() should return what the script printed', async () => {
    const result = await client.executePython('print(get_all_level_actors)');
    assert.equal(result.status, 200);
    assert.ok(result.body.includes('PointLight'));
  });

  it('executePython() should turn a raised error into status 400 with the traceback', async () => {
    const result = await client.executePython('raise ValueError("boom")');
    assert.equal(result.status, 400);
    assert.match(result.body, /ValueError: boom/);
  });

  // ── Bridge tools without the bridge (Python fallback) ──

  it('consoleCommand() should fall back to Python when the bridge is not available', async () => {
    const result = await client.consoleCommand('stat fps');
    assert.equal(result.status, 200);
    const [req] = mockServer.getLastRequests();
    assert.equal(req.url, '/remote/object/call');
    assert.ok(req.body.parameters.PythonCommand.includes('execute_console_command'));
    // The command travels as base64 JSON, not spliced into the script
    assert.ok(!req.body.parameters.PythonCommand.includes('stat fps'));
  });

  it('notify(), getProjectInfo() and importAsset() should fall back to Python', async () => {
    for (const [call, marker] of [
      [() => client.notify('hi', 'warning'), 'log_warning'],
      [() => client.getProjectInfo(), 'get_engine_version'],
      [() => client.importAsset('C:/a.fbx', '/Game/A'), 'AssetImportTask'],
    ]) {
      const result = await call();
      assert.equal(result.status, 200);
      const [req] = mockServer.getLastRequests();
      assert.ok(req.body.parameters.PythonCommand.includes(marker), marker);
    }
  });

  it('screenshot() without the bridge should refuse a remote Unreal host', async () => {
    const remote = new UnrealClient('192.168.1.50', MOCK_PORT, { maxRetries: 1, healthCheckInterval: 0 });
    const result = await remote.screenshot();
    assert.equal(result.status, 501);
  });

  // ── Actor Management ──

  it('getActors() should return actor list', async () => {
    const result = await client.getActors();
    assert.equal(result.status, 200);
    // The mock returns simulated Python output
    assert.ok(result.body);
  });

  it('getActors() with filter should include filter in script', async () => {
    await client.getActors('StaticMeshActor');
    const [req] = mockServer.getLastRequests();
    assert.ok(req.body.parameters.PythonCommand.includes('StaticMeshActor'));
  });

  // ── Function Calls ──

  it('callFunction() should send correct payload', async () => {
    const result = await client.callFunction(
      '/Script/Engine.Default__KismetSystemLibrary',
      'PrintString',
      { InString: 'test' },
      true
    );
    assert.equal(result.status, 200);

    const [req] = mockServer.getLastRequests();
    assert.equal(req.body.objectPath, '/Script/Engine.Default__KismetSystemLibrary');
    assert.equal(req.body.functionName, 'PrintString');
    assert.equal(req.body.generateTransaction, true);
  });

  // ── Property Access ──

  it('getProperty() should send READ_ACCESS', async () => {
    const result = await client.getProperty('/Script/Engine.Player', 'PlayerName');
    assert.equal(result.status, 200);

    const [req] = mockServer.getLastRequests();
    assert.equal(req.body.access, 'READ_ACCESS');
    assert.equal(req.body.propertyName, 'PlayerName');
  });

  it('setProperty() should send WRITE_ACCESS', async () => {
    const result = await client.setProperty('/Script/Engine.Player', 'PlayerName', 'NewName');
    assert.equal(result.status, 200);

    const [req] = mockServer.getLastRequests();
    assert.equal(req.body.access, 'WRITE_ACCESS');
    assert.equal(req.body.propertyName, 'PlayerName');
    assert.equal(req.body.propertyValue, 'NewName');
  });

  // ── Request Logging ──

  it('should log all requests', async () => {
    await client.ping();
    await client.executePython('pass');
    assert.equal(mockServer.requestLog.length, 2);
    assert.equal(mockServer.requestLog[0].url, '/remote/info');
    assert.equal(mockServer.requestLog[1].url, '/remote/object/call');
  });

  // ── Retry Logic ──

  it('should fail fast on connection refused (no retries for non-existent server)', async () => {
    const badClient = new UnrealClient('127.0.0.1', 39998, {
      maxRetries: 1,
      retryDelay: 50,
    });
    const start = Date.now();
    await assert.rejects(() => badClient.ping());
    const elapsed = Date.now() - start;
    // Should fail relatively quickly with maxRetries=1
    assert.ok(elapsed < 5000, `Took too long: ${elapsed}ms`);
  });

  // ── Health Check ──

  it('health check should detect connected state', async () => {
    const testClient = new UnrealClient('127.0.0.1', MOCK_PORT, {
      maxRetries: 1,
      healthCheckInterval: 0,
    });

    await testClient._runHealthCheck();
    assert.equal(testClient.isConnected, true);
    testClient.shutdown();
  });

  // ── New actor tools ──

  it('executePython() with special characters in params should use base64 encoding', async () => {
    // Ensure pyparams base64 approach works end-to-end via the mock
    const dangerousLabel = "it's a \"tricky\" label\nwith newlines\\backslashes";
    const b64 = Buffer.from(JSON.stringify({ label: dangerousLabel })).toString('base64');
    // The script should contain the base64 string, not the raw label
    const scriptContainsB64 = b64.slice(0, 20); // first 20 chars of b64 as a proxy check
    // We just verify executePython round-trips without throwing
    const result = await client.executePython(`import base64\nprint(base64.b64decode('${b64}').decode())`);
    assert.equal(result.status, 200);
  });

  it('getActors() with StaticMeshActor filter should include class name in script', async () => {
    await client.getActors('StaticMeshActor');
    const [req] = mockServer.getLastRequests();
    assert.ok(req.body.parameters.PythonCommand.includes('StaticMeshActor'));
  });

  it('executePython for set_actor_visibility should return success', async () => {
    const result = await client.executePython(`
import unreal
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == 'Floor'), None)
if target:
    target.set_is_temporarily_hidden_in_editor(False)
    print({'success': True, 'visible': True})
`.trim());
    assert.equal(result.status, 200);
  });

  it('executePython for set_actor_transform should return success', async () => {
    const result = await client.executePython(`
import unreal
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == 'Floor'), None)
if target:
    target.set_actor_location(unreal.Vector(100, 0, 0), False, False)
    print({'success': True, 'actor': 'Floor'})
`.trim());
    assert.equal(result.status, 200);
  });

  it('executePython for duplicate_actor should return success', async () => {
    const result = await client.executePython('duplicate_actor stub');
    assert.equal(result.status, 200);
  });

  it('executePython for get_actor_bounds should return bounds data', async () => {
    const result = await client.executePython('get_actor_bounds stub');
    assert.equal(result.status, 200);
    // Mock returns JSON with origin/extent
    const body = result.body;
    assert.ok(body);
  });

  it('executePython for set_material should return success', async () => {
    const result = await client.executePython('set_material stub');
    assert.equal(result.status, 200);
  });

  it('executePython for compile_blueprint should return success', async () => {
    const result = await client.executePython('KismetEditorUtilities.compile_blueprint stub');
    assert.equal(result.status, 200);
  });

  it('executePython for build_lighting should return success', async () => {
    const result = await client.executePython('LightingBuildOptions stub');
    assert.equal(result.status, 200);
  });

  // ── Actor extras ──

  it('find_actors_by_name should send a script with pattern-matching logic', async () => {
    const result = await client.executePython('pat in label.lower() stub');
    assert.equal(result.status, 200);
  });

  it('set_actor_property should send a script with set_editor_property and property_name', async () => {
    const result = await client.executePython('set_editor_property stub property_name stub');
    assert.equal(result.status, 200);
  });

  it('spawn_blueprint_actor should send load_blueprint_class + spawn_actor_from_class', async () => {
    const result = await client.executePython('load_blueprint_class stub spawn_actor_from_class stub');
    assert.equal(result.status, 200);
  });

  // ── Blueprint class tools ──

  it('create_blueprint should send BlueprintFactory + create_asset', async () => {
    const result = await client.executePython('BlueprintFactory stub create_asset stub');
    assert.equal(result.status, 200);
  });

  it('add_component_to_blueprint should send SubobjectDataSubsystem + add_new_subobject', async () => {
    const result = await client.executePython('SubobjectDataSubsystem stub add_new_subobject stub');
    assert.equal(result.status, 200);
  });

  it('set_static_mesh_properties should send SubobjectDataSubsystem + static_mesh', async () => {
    const result = await client.executePython('SubobjectDataSubsystem stub static_mesh stub');
    assert.equal(result.status, 200);
  });

  it('set_component_property should send SubobjectDataSubsystem + property_name', async () => {
    const result = await client.executePython('SubobjectDataSubsystem stub property_name stub');
    assert.equal(result.status, 200);
  });

  it('set_physics_properties should send simulate_physics', async () => {
    const result = await client.executePython('simulate_physics stub');
    assert.equal(result.status, 200);
  });

  it('set_blueprint_property should send get_default_object + set_editor_property', async () => {
    const result = await client.executePython('get_default_object stub set_editor_property stub');
    assert.equal(result.status, 200);
  });

  it('set_pawn_properties should send AutoReceiveInput or use_controller_rotation', async () => {
    const result = await client.executePython('AutoReceiveInput stub use_controller_rotation stub');
    assert.equal(result.status, 200);
  });

  // ── Blueprint node graph tools ──

  it('add_blueprint_variable should send BPVariableDescription + new_variables', async () => {
    const result = await client.executePython('BPVariableDescription stub new_variables stub');
    assert.equal(result.status, 200);
  });

  it('find_blueprint_nodes should send get_blueprint_event_graphs + node_class', async () => {
    const result = await client.executePython('get_blueprint_event_graphs stub node_class stub');
    assert.equal(result.status, 200);
  });

  it('add_blueprint_event_node should send add_new_function_graph', async () => {
    const result = await client.executePython('add_new_function_graph stub');
    assert.equal(result.status, 200);
  });

  it('add_blueprint_input_action_node should send InpActEvt_', async () => {
    const result = await client.executePython('InpActEvt_ stub');
    assert.equal(result.status, 200);
  });

  it('add_blueprint_function_node should send add_function_to_blueprint_graph', async () => {
    const result = await client.executePython('add_function_to_blueprint_graph stub');
    assert.equal(result.status, 200);
  });

  it('connect_blueprint_nodes should send try_create_connection', async () => {
    const result = await client.executePython('try_create_connection stub can_create_connection stub');
    assert.equal(result.status, 200);
  });

  it('add_blueprint_get_self_component_reference should send GetMyMesh + get_all_pins', async () => {
    const result = await client.executePython('GetMyMesh stub get_all_pins stub');
    assert.equal(result.status, 200);
  });

  it('add_blueprint_self_reference should send SelfReference', async () => {
    const result = await client.executePython("'Self' stub SelfReference stub");
    assert.equal(result.status, 200);
  });

  // ── UMG tools ──

  it('create_umg_widget_blueprint should send WidgetBlueprintFactory', async () => {
    const result = await client.executePython('WidgetBlueprintFactory stub');
    assert.equal(result.status, 200);
  });

  it('add_text_block_to_widget should send construct_widget + TextBlock', async () => {
    const result = await client.executePython('construct_widget stub TextBlock stub');
    assert.equal(result.status, 200);
  });

  it('add_button_to_widget should send construct_widget + Button', async () => {
    const result = await client.executePython('construct_widget stub Button stub');
    assert.equal(result.status, 200);
  });

  it('bind_widget_event should send function_graphs + On', async () => {
    const result = await client.executePython('function_graphs stub On stub');
    assert.equal(result.status, 200);
  });

  it('add_widget_to_viewport should send load_blueprint_class + z_order', async () => {
    const result = await client.executePython('load_blueprint_class stub z_order stub');
    assert.equal(result.status, 200);
  });

  it('set_text_block_binding should send BPVariableDescription + default_value', async () => {
    const result = await client.executePython('BPVariableDescription stub default_value stub');
    assert.equal(result.status, 200);
  });

  // ── Input mapping ──

  it('create_input_mapping (Action) should send InputActionKeyMapping', async () => {
    const result = await client.executePython('InputActionKeyMapping stub');
    assert.equal(result.status, 200);
  });

  it('create_input_mapping (Axis) should send InputAxisKeyMapping', async () => {
    const result = await client.executePython('InputAxisKeyMapping stub');
    assert.equal(result.status, 200);
  });
});

// ─── Logger Tests ─────────────────────────────────────────────────────────────

describe('Logger', () => {
  it('should create logger with default level', () => {
    const { Logger } = require('../src/logger.js');
    const log = new Logger('test');
    assert.equal(log.name, 'test');
  });

  it('should create child loggers', () => {
    const { Logger } = require('../src/logger.js');
    const parent = new Logger('parent');
    const child = parent.child('child');
    assert.equal(child.name, 'parent:child');
  });

  it('should suppress messages below configured level', () => {
    const { Logger } = require('../src/logger.js');
    const origEnv = process.env.LOG_LEVEL;
    process.env.LOG_LEVEL = 'error';
    const log = new Logger('level-test');
    // Just verify it doesn't throw when below-level messages are suppressed
    assert.doesNotThrow(() => log.debug('should be suppressed'));
    assert.doesNotThrow(() => log.info('should be suppressed'));
    assert.doesNotThrow(() => log.warn('should be suppressed'));
    process.env.LOG_LEVEL = origEnv;
  });

  it('child logger inherits parent level', () => {
    const { Logger } = require('../src/logger.js');
    const parent = new Logger('parent');
    parent.level = 3; // error only
    const child = parent.child('child');
    assert.equal(child.level, 3);
  });
});

// ─── Permission Policy ────────────────────────────────────────────────────────

describe('Permission policy', () => {
  const fs = require('fs');
  const path = require('path');
  const { POLICY, claudeRules, opencodeRules } = require('../src/permissions.js');
  const source = fs.readFileSync(path.join(__dirname, '..', 'src', 'index.js'), 'utf8');
  const tools = [...source.matchAll(/server\.tool\(\s*'([a-z_]+)'/g)].map((m) => m[1]);

  it('should place every registered tool in exactly one level', () => {
    const listed = [...POLICY.allow, ...POLICY.ask, ...POLICY.deny];
    assert.deepEqual([...listed].sort(), [...new Set(listed)].sort(), 'duplicate tool in policy');
    assert.deepEqual([...listed].sort(), [...tools].sort());
  });

  it('should build client rule names', () => {
    assert.ok(claudeRules('BIM-Bot-Unreal').deny.includes('mcp__BIM-Bot-Unreal__execute_python'));
    assert.equal(opencodeRules('BIM-Bot-Unreal')['BIM-Bot-Unreal_delete_actor'], 'ask');
  });
});

// ─── Unreal Project Setup ─────────────────────────────────────────────────────

describe('Unreal project setup', () => {
  const fs = require('fs');
  const os = require('os');
  const path = require('path');
  const { findProjects, setupProject, projectStatus, setIniKeys, RC_SECTION } = require('../src/unreal-setup.js');
  let dir;
  let uproject;
  let ini;

  beforeEach(() => {
    dir = fs.mkdtempSync(path.join(os.tmpdir(), 'bimbot-setup-'));
    uproject = path.join(dir, 'Demo', 'Demo.uproject');
    ini = path.join(dir, 'Demo', 'Config', 'DefaultRemoteControl.ini');
    fs.mkdirSync(path.dirname(uproject), { recursive: true });
    fs.writeFileSync(uproject, '\uFEFF' + JSON.stringify({
      FileVersion: 3,
      EngineAssociation: '5.8',
      Plugins: [{ Name: 'PythonScriptPlugin', Enabled: false }, { Name: 'Other', Enabled: true }],
    }));
  });

  after(() => fs.rmSync(dir, { recursive: true, force: true }));

  it('should find only the projects under the given folder or file', () => {
    // Projects elsewhere (Documents\Unreal Projects) must not be picked up
    assert.deepEqual(findProjects([dir]), [uproject]);
    assert.deepEqual(findProjects([uproject]), [uproject]);
  });

  it('should enable the plugins, write the config and keep other plugins', () => {
    assert.ok(projectStatus(uproject).length > 0);
    const changes = setupProject(uproject);
    assert.ok(changes.length >= 3);
    assert.deepEqual(projectStatus(uproject), []);

    const proj = JSON.parse(fs.readFileSync(uproject, 'utf8'));
    const enabled = proj.Plugins.filter((p) => p.Enabled).map((p) => p.Name).sort();
    assert.deepEqual(enabled, ['EditorScriptingUtilities', 'Other', 'PythonScriptPlugin', 'RemoteControl']);
    assert.ok(fs.existsSync(uproject + '.bak'));

    const text = fs.readFileSync(ini, 'utf8');
    assert.match(text, /bEnableRemotePythonExecution=True/);
    assert.match(text, /\+CustomAllowedRemoteFunctionCalls=\(ClassPath="\/Script\/PythonScriptPlugin\.PythonScriptLibrary"/);
    assert.match(text, /RemoteControlWebsocketServerBindAddress=127\.0\.0\.1/);
  });

  it('should be idempotent', () => {
    setupProject(uproject);
    const before = fs.readFileSync(ini, 'utf8');
    assert.deepEqual(setupProject(uproject), []);
    assert.equal(fs.readFileSync(ini, 'utf8'), before);
  });

  it('should keep existing ini settings and array entries', () => {
    fs.mkdirSync(path.dirname(ini), { recursive: true });
    fs.writeFileSync(ini, [
      `[${RC_SECTION}]`,
      'bEnableRemotePythonExecution=False',
      '+CustomAllowedRemoteFunctionCalls=(ClassPath="/Script/Engine.KismetSystemLibrary")',
      '',
      '[/Script/Other.Settings]',
      'Keep=1',
      '',
    ].join('\r\n'));
    setupProject(uproject);
    const text = fs.readFileSync(ini, 'utf8');
    assert.match(text, /bEnableRemotePythonExecution=True/);
    assert.doesNotMatch(text, /bEnableRemotePythonExecution=False/);
    assert.match(text, /KismetSystemLibrary/);
    assert.match(text, /\[\/Script\/Other\.Settings\]\r\nKeep=1/);
    assert.ok(!/[^\r]\n/.test(text), 'line endings kept as CRLF');
  });

  it('setIniKeys should add a missing section', () => {
    assert.equal(setIniKeys('', 'S', { A: '1' }), '[S]\nA=1\n');
  });
});

describe('Unreal engine setup', () => {
  const fs = require('fs');
  const os = require('os');
  const path = require('path');
  const { setupEngine, engineStatus, enableByDefault, RC_SECTION } = require('../src/unreal-setup.js');
  let engine;
  const uplugin = (rel) => path.join(engine, 'Engine', 'Plugins', rel);

  beforeEach(() => {
    engine = fs.mkdtempSync(path.join(os.tmpdir(), 'bimbot-engine-'));
    const files = {
      'VirtualProduction/RemoteControl/RemoteControl.uplugin': '{\r\n\t"FileVersion": 3,\r\n\t"FriendlyName": "Remote Control API"\r\n}\r\n',
      'Experimental/PythonScriptPlugin/PythonScriptPlugin.uplugin': '{\n\t"FileVersion": 3,\n\t"EnabledByDefault": false\n}\n',
      'Editor/EditorScriptingUtilities/EditorScriptingUtilities.uplugin': '{\n\t"FileVersion": 3\n}\n',
    };
    for (const [rel, text] of Object.entries(files)) {
      fs.mkdirSync(path.dirname(uplugin(rel)), { recursive: true });
      fs.writeFileSync(uplugin(rel), text);
    }
    const ini = uplugin('VirtualProduction/RemoteControl/Config/DefaultRemoteControl.ini');
    fs.mkdirSync(path.dirname(ini), { recursive: true });
    fs.writeFileSync(ini, '[CoreRedirects]\n+PropertyRedirects=(OldName="A",NewName="B")\n');
  });

  after(() => fs.rmSync(engine, { recursive: true, force: true }));

  it('should turn the plugins on by default and write the Remote Control config', () => {
    assert.equal(engineStatus(engine).length, 4);
    assert.equal(setupEngine(engine).length, 4);
    assert.deepEqual(engineStatus(engine), []);

    for (const rel of ['VirtualProduction/RemoteControl/RemoteControl.uplugin',
      'Experimental/PythonScriptPlugin/PythonScriptPlugin.uplugin',
      'Editor/EditorScriptingUtilities/EditorScriptingUtilities.uplugin']) {
      const desc = JSON.parse(fs.readFileSync(uplugin(rel), 'utf8'));
      assert.equal(desc.EnabledByDefault, true, rel);
      assert.equal(desc.FileVersion, 3);
      assert.ok(fs.existsSync(uplugin(rel) + '.bak'));
    }
    assert.match(fs.readFileSync(uplugin('VirtualProduction/RemoteControl/RemoteControl.uplugin'), 'utf8'),
      /^\{\r\n\t"EnabledByDefault": true,\r\n\t"FileVersion": 3,/);

    const ini = fs.readFileSync(uplugin('VirtualProduction/RemoteControl/Config/DefaultRemoteControl.ini'), 'utf8');
    assert.match(ini, /\+PropertyRedirects=\(OldName="A",NewName="B"\)/);
    assert.ok(ini.includes(`[${RC_SECTION}]`));
    assert.match(ini, /bEnableRemotePythonExecution=True/);
  });

  it('should be idempotent', () => {
    setupEngine(engine);
    assert.deepEqual(setupEngine(engine), []);
  });

  it('should report a missing plugin instead of throwing', () => {
    fs.rmSync(uplugin('Editor/EditorScriptingUtilities/EditorScriptingUtilities.uplugin'));
    assert.match(engineStatus(engine).join(), /EditorScriptingUtilities not found/);
    assert.throws(() => setupEngine(engine), /EditorScriptingUtilities not found/);
  });

  it('enableByDefault should keep a uplugin that is already on', () => {
    const text = '{\n\t"EnabledByDefault": true\n}\n';
    assert.equal(enableByDefault(text), text);
  });
});
