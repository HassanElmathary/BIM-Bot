/**
 * Mock Unreal Engine HTTP Server
 * 
 * Simulates the Remote Control API for testing purposes.
 * Responds to the same endpoints the real Unreal Engine would.
 */

const http = require('http');

class MockUnrealServer {
  constructor(port = 30010) {
    this.port = port;
    this.server = null;
    this.requestLog = [];
    this._sockets = new Set();
  }

  /** Start the mock server */
  start() {
    return new Promise((resolve, reject) => {
      this.server = http.createServer((req, res) => {
        let body = '';
        req.on('data', (chunk) => { body += chunk; });
        req.on('end', () => {
          const entry = {
            method: req.method,
            url: req.url,
            body: body ? JSON.parse(body) : null,
            timestamp: Date.now(),
          };
          this.requestLog.push(entry);
          res.setHeader('Connection', 'close');
          this._handleRequest(req, res, entry);
        });
      });

      this.server.on('error', reject);
      this.server.on('connection', (socket) => {
        this._sockets.add(socket);
        socket.on('close', () => this._sockets.delete(socket));
      });
      this.server.listen(this.port, '127.0.0.1', () => {
        resolve();
      });
    });
  }

  /** Stop the mock server */
  stop() {
    return new Promise((resolve) => {
      if (this.server) {
        // Destroy all open sockets to prevent hanging
        for (const socket of this._sockets) {
          socket.destroy();
        }
        this._sockets.clear();
        this.server.close(() => resolve());
      } else {
        resolve();
      }
    });
  }

  /** Clear the request log */
  clearLog() {
    this.requestLog = [];
  }

  /** Get the last N requests */
  getLastRequests(n = 1) {
    return this.requestLog.slice(-n);
  }

  /** @private Route handler */
  _handleRequest(req, res, entry) {
    const respond = (status, body) => {
      res.writeHead(status, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify(body));
    };

    // GET /remote/info — engine info
    if (req.method === 'GET' && req.url === '/remote/info') {
      return respond(200, {
        ActiveLevelName: '/Game/Maps/TestLevel',
        EngineVersion: '5.4.0-0+++UE5+Release-5.4',
        IsEditor: true,
        MachineName: 'TEST-MACHINE',
      });
    }

    // PUT /remote/object/call — function calls
    if (req.method === 'PUT' && req.url === '/remote/object/call') {
      const { objectPath, functionName, parameters } = entry.body || {};

      // Python execution — same response shape as UE 5.8: printed lines in LogOutput,
      // the traceback in CommandResult and ReturnValue false when the script raised
      if (objectPath?.includes('PythonScriptLibrary') && functionName === 'ExecutePythonCommandEx') {
        const script = parameters?.PythonCommand || '';
        if (script.includes('raise ')) {
          return respond(200, {
            CommandResult: 'Traceback (most recent call last):\n  File "<string>", line 1, in <module>\nValueError: boom\n',
            LogOutput: [],
            ReturnValue: false,
          });
        }
        return respond(200, {
          CommandResult: 'None',
          LogOutput: [{ Type: 'Info', Output: `${this._simulatePython(script)}\r\n` }],
          ReturnValue: true,
        });
      }

      // Generic function call
      return respond(200, {
        ReturnValue: { success: true, function: functionName },
      });
    }

    // PUT /remote/object/property — property access
    if (req.method === 'PUT' && req.url === '/remote/object/property') {
      const { access, propertyName } = entry.body || {};
      if (access === 'READ_ACCESS') {
        return respond(200, {
          [propertyName]: 'mock_value',
        });
      }
      if (access === 'WRITE_ACCESS') {
        return respond(200, { success: true });
      }
      return respond(400, { error: 'Invalid access mode' });
    }

    // PUT /remote/search/assets
    if (req.method === 'PUT' && req.url === '/remote/search/assets') {
      return respond(200, {
        Assets: [
          { Name: 'TestMesh', Class: 'StaticMesh', Path: '/Game/Meshes/TestMesh' },
          { Name: 'TestMaterial', Class: 'Material', Path: '/Game/Materials/TestMaterial' },
        ],
      });
    }

    // Fallback 404
    respond(404, { error: `Unknown endpoint: ${req.method} ${req.url}` });
  }

  /** @private Simulate Python script output */
  _simulatePython(script) {
    // Detect what the script is trying to do and return plausible output
    if (script.includes('get_all_level_actors')) {
      return JSON.stringify([
        { name: 'Floor', class: 'StaticMeshActor', path: '/Game/Maps/TestLevel.TestLevel:Floor', location: { x: 0, y: 0, z: 0 } },
        { name: 'PointLight', class: 'PointLight', path: '/Game/Maps/TestLevel.TestLevel:PointLight', location: { x: 100, y: 200, z: 300 } },
        { name: 'PlayerStart', class: 'PlayerStart', path: '/Game/Maps/TestLevel.TestLevel:PlayerStart', location: { x: 0, y: 0, z: 50 } },
      ]);
    }

    if (script.includes('get_editor_world')) {
      return JSON.stringify({
        world_name: 'TestLevel',
        path: '/Game/Maps/TestLevel',
        actor_count: 3,
        classes: ['StaticMeshActor', 'PointLight', 'PlayerStart'],
      });
    }

    if (script.includes('spawn_actor_from_class')) {
      return JSON.stringify({ success: true, actor: 'NewActor', path: '/Game/Maps/TestLevel.TestLevel:NewActor' });
    }

    if (script.includes('destroy_actor')) {
      return JSON.stringify({ success: true, deleted: 'TargetActor' });
    }

    if (script.includes('save_all_dirty_levels')) {
      return JSON.stringify({ success: true });
    }

    if (script.includes('compile_blueprint')) {
      return JSON.stringify({ success: true, blueprint: '/Game/Blueprints/TestBP' });
    }

    if (script.includes('get_selected_level_actors')) {
      return JSON.stringify([]);
    }

    if (script.includes('build_lighting')) {
      return JSON.stringify({ success: true, quality: 'Preview' });
    }

    // Actor extras
    if (script.includes('pat in label.lower()')) {
      return JSON.stringify([
        { label: 'Floor', class: 'StaticMeshActor', path: '/Game/Maps/TestLevel.TestLevel:Floor', location: { x: 0, y: 0, z: 0 } },
      ]);
    }

    if (script.includes('set_editor_property') && script.includes('property_name')) {
      return JSON.stringify({ success: true, label: 'Floor', property: 'bHidden' });
    }

    if (script.includes('load_blueprint_class') && script.includes('spawn_actor_from_class')) {
      return JSON.stringify({ success: true, actor: 'BP_MyActor', path: '/Game/Maps/TestLevel.TestLevel:BP_MyActor' });
    }

    // Blueprint class tools
    if (script.includes('BlueprintFactory') && script.includes('create_asset')) {
      return JSON.stringify({ success: true, name: 'BP_Test', path: '/Game/Blueprints/BP_Test.BP_Test' });
    }

    if (script.includes('SubobjectDataSubsystem') && script.includes('add_new_subobject')) {
      return JSON.stringify({ success: true, component: 'MyMesh', type: 'StaticMeshComponent' });
    }

    if (script.includes('SubobjectDataSubsystem') && script.includes('static_mesh')) {
      return JSON.stringify({ success: true, component: 'MyMesh', mesh: '/Engine/BasicShapes/Cube.Cube' });
    }

    if (script.includes('SubobjectDataSubsystem') && script.includes('property_name')) {
      return JSON.stringify({ success: true, component: 'MyMesh', property: 'CastShadow' });
    }

    if (script.includes('simulate_physics')) {
      return JSON.stringify({ success: true, component: 'MyMesh', simulate_physics: false });
    }

    if (script.includes('get_default_object') && script.includes('set_editor_property')) {
      return JSON.stringify({ success: true, property: 'SomeProperty' });
    }

    if (script.includes('AutoReceiveInput') || script.includes('use_controller_rotation')) {
      return JSON.stringify({ success: true, blueprint: '/Game/Blueprints/BP_Test' });
    }

    // Blueprint node graph tools
    if (script.includes('BPVariableDescription') || script.includes('new_variables')) {
      return JSON.stringify({ success: true, variable: 'MyVar', type: 'Boolean' });
    }

    if (script.includes('get_blueprint_event_graphs') && script.includes('node_class')) {
      return JSON.stringify([
        { node_id: 'K2Node_Event_0', node_class: 'K2Node_Event', graph: 'EventGraph', position: { x: 0, y: 0 } },
      ]);
    }

    if (script.includes('add_new_function_graph') || script.includes('add_function_to_blueprint_graph')) {
      return JSON.stringify({ success: true, event: 'ReceiveBeginPlay', graph: 'EventGraph' });
    }

    if (script.includes('InpActEvt_')) {
      return JSON.stringify({ success: true, action: 'Jump' });
    }

    if (script.includes('try_create_connection') || script.includes('can_create_connection')) {
      return JSON.stringify({ success: true, from: 'Node_0.then', to: 'Node_1.execute' });
    }

    if (script.includes('GetMyMesh') || script.includes('get_all_pins')) {
      return JSON.stringify({ success: true, component: 'MyMesh' });
    }

    if (script.includes("'Self'") && script.includes('SelfReference')) {
      return JSON.stringify({ success: true, node: 'SelfReference', graph: 'EventGraph' });
    }

    // UMG tools
    if (script.includes('WidgetBlueprintFactory')) {
      return JSON.stringify({ success: true, name: 'WBP_HUD', path: '/Game/UI/WBP_HUD.WBP_HUD' });
    }

    if (script.includes('construct_widget') && script.includes('TextBlock')) {
      return JSON.stringify({ success: true, text_block: 'MyText' });
    }

    if (script.includes('construct_widget') && script.includes('Button')) {
      return JSON.stringify({ success: true, button: 'MyButton' });
    }

    if (script.includes('function_graphs') && script.includes('On')) {
      return JSON.stringify({ success: true, bound_event: 'OnClicked', component: 'MyButton', function: 'OnMyButton_OnClicked' });
    }

    if (script.includes('load_blueprint_class') && script.includes('z_order')) {
      return JSON.stringify({ success: true, widget_class: '/Game/UI/WBP_HUD_C', z_order: 0, note: 'Call CreateWidget...' });
    }

    if (script.includes('BPVariableDescription') && script.includes('default_value')) {
      return JSON.stringify({ success: true, variable: 'MyBindingProp', getter: 'GetMyBindingProp', text_block: 'MyText' });
    }

    // Input mapping
    if (script.includes('InputActionKeyMapping') || script.includes('InputAxisKeyMapping')) {
      return JSON.stringify({ success: true, action: 'Jump', key: 'SpaceBar', type: 'Action' });
    }

    if (script.includes('set_actor_label')) {
      return JSON.stringify({ success: true, old_label: 'OldActor', new_label: 'NewActor' });
    }

    if (script.includes('duplicate_actor')) {
      return JSON.stringify({ success: true, original: 'Floor', duplicate: 'Floor_Copy' });
    }

    if (script.includes('get_actor_bounds')) {
      return JSON.stringify({
        label: 'Floor',
        origin: { x: 0, y: 0, z: 0 },
        extent: { x: 200, y: 200, z: 10 },
        size: { x: 400, y: 400, z: 20 },
      });
    }

    if (script.includes('set_is_temporarily_hidden_in_editor')) {
      return JSON.stringify({ success: true, visible: true });
    }

    if (script.includes('set_actor_location') || script.includes('set_actor_rotation') || script.includes('set_actor_scale3d')) {
      return JSON.stringify({ success: true, actor: 'Floor' });
    }

    if (script.includes('set_material')) {
      return JSON.stringify({ success: true });
    }

    if (script.includes('LightingBuildOptions') || script.includes('build_lighting')) {
      return JSON.stringify({ success: true, quality: 'Preview' });
    }

    if (script.includes('target.tags')) {
      return JSON.stringify({ success: true, label: 'Floor', tags: ['MyTag'] });
    }

    if (script.includes('load_level')) {
      return JSON.stringify({ success: true });
    }

    if (script.includes('load_asset') && script.includes('class')) {
      return JSON.stringify({ name: 'TestMesh', class: 'StaticMesh', path: '/Game/Meshes/TestMesh.TestMesh' });
    }

    if (script.includes('ar.get_assets') || script.includes('AssetRegistryHelpers')) {
      return JSON.stringify([
        { name: 'TestMesh', class: 'StaticMesh', path: '/Game/Meshes/TestMesh' },
      ]);
    }

    if (script.includes('KismetEditorUtilities.compile_blueprint')) {
      return JSON.stringify({ success: true, blueprint: '/Game/Blueprints/TestBP' });
    }

    if (script.includes('play_in_editor') || script.includes('EditorPlayInSettings')) {
      return JSON.stringify({ success: true });
    }

    if (script.includes('end_play_map')) {
      return JSON.stringify({ success: true });
    }

    if (script.includes('spawn_actor_from_class')) {
      return JSON.stringify({ success: true, actor: 'NewActor', path: '/Game/Maps/TestLevel.TestLevel:NewActor' });
    }

    // Default
    return 'Script executed successfully';
  }
}

module.exports = { MockUnrealServer };
