const { McpServer } = require('@modelcontextprotocol/sdk/server/mcp.js');
const { StdioServerTransport } = require('@modelcontextprotocol/sdk/server/stdio.js');
const { z } = require('zod');
const { UnrealClient } = require('./unreal-client.js');
const { logger } = require('./logger.js');

const log = logger.child('server');

const client = new UnrealClient(
  process.env.UNREAL_HOST || 'localhost',
  parseInt(process.env.UNREAL_PORT || '30010'),
  {
    bridgePort: parseInt(process.env.UNREAL_BRIDGE_PORT || '30011'),
    timeout: parseInt(process.env.UNREAL_TIMEOUT || '10000'),
  }
);

const server = new McpServer({
  name: 'BIM-Bot-Unreal',
  version: '1.0.0',
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

function ok(data) {
  return { content: [{ type: 'text', text: typeof data === 'string' ? data : JSON.stringify(data, null, 2) }] };
}

function err(msg) {
  return { content: [{ type: 'text', text: `ERROR: ${msg}` }], isError: true };
}

async function safe(fn) {
  try {
    return await fn();
  } catch (e) {
    log.error('Tool execution error', { error: e.message, stack: e.stack });
    return err(e.message);
  }
}

/**
 * Safely encode arbitrary JS values as base64-encoded JSON for injection into
 * Python scripts. Prevents all injection attacks regardless of the input content.
 *
 * Usage in a Python script template:
 *   const { head, ref } = pyparams({ label, path });
 *   `${head}\nactor_label = ${ref}['label']\n...`
 *
 * This generates:
 *   import json as _j,base64 as _b
 *   _p = _j.loads(_b.b64decode('<b64>').decode())
 *   actor_label = _p['label']
 */
function pyparams(obj) {
  const b64 = Buffer.from(JSON.stringify(obj)).toString('base64');
  return {
    head: `import json as _j,base64 as _b\n_p=_j.loads(_b.b64decode('${b64}').decode())`,
    ref: '_p',
  };
}

// ─── Connection ───────────────────────────────────────────────────────────────

server.tool(
  'ping_unreal',
  'Check if Unreal Engine is reachable via Remote Control API',
  {},
  async () => safe(async () => {
    const result = await client.get('/remote/info');
    if (result.status === 200) {
      return ok({
        connected: true,
        bridge_available: client.isBridgeAvailable,
        info: result.body,
      });
    }
    return err(`Unreal returned status ${result.status}`);
  })
);

// ─── Python Execution ─────────────────────────────────────────────────────────

server.tool(
  'execute_python',
  'Execute a Python script inside the Unreal Editor. Requires the Python Editor Script Plugin to be enabled.',
  {
    script: z.string().describe('Python script to execute in Unreal Editor context'),
  },
  async ({ script }) => safe(async () => {
    const result = await client.executePython(script);
    if (result.status >= 400) {
      return err(`Python execution failed (${result.status}): ${JSON.stringify(result.body)}`);
    }
    return ok(result.body ?? 'Script executed successfully');
  })
);

// ─── Actor Management ─────────────────────────────────────────────────────────

server.tool(
  'get_actors',
  'List all actors in the current Unreal level',
  {
    class_filter: z.string().optional().describe('Optional class name filter (e.g. "StaticMeshActor")'),
  },
  async ({ class_filter }) => safe(async () => {
    const result = await client.getActors(class_filter || '');
    if (result.status >= 400) {
      return err(`Failed to get actors: ${JSON.stringify(result.body)}`);
    }
    return ok(result.body);
  })
);

server.tool(
  'spawn_actor',
  'Spawn an actor in the current Unreal level',
  {
    class_path: z.string().describe('Asset path of the actor class, e.g. "/Script/Engine.PointLight"'),
    label: z.string().optional().describe('Label for the new actor'),
    location: z.object({
      x: z.number().default(0),
      y: z.number().default(0),
      z: z.number().default(0),
    }).optional().describe('World location in Unreal units (cm)'),
    rotation: z.object({
      pitch: z.number().default(0),
      yaw: z.number().default(0),
      roll: z.number().default(0),
    }).optional().describe('Rotation in degrees'),
    scale: z.object({
      x: z.number().default(1),
      y: z.number().default(1),
      z: z.number().default(1),
    }).optional().describe('Scale'),
  },
  async ({ class_path, label, location, rotation, scale }) => safe(async () => {
    const loc = location || { x: 0, y: 0, z: 0 };
    const rot = rotation || { pitch: 0, yaw: 0, roll: 0 };
    const scl = scale || { x: 1, y: 1, z: 1 };
    const { head, ref } = pyparams({ class_path, label: label || null });

    const script = `
import unreal
${head}
loc = unreal.Vector(${loc.x}, ${loc.y}, ${loc.z})
rot = unreal.Rotator(${rot.pitch}, ${rot.yaw}, ${rot.roll})
actor_class = unreal.load_class(None, ${ref}['class_path'])
if actor_class is None:
    print('ERROR: Could not load class: ' + ${ref}['class_path'])
else:
    actor = unreal.EditorLevelLibrary.spawn_actor_from_class(actor_class, loc, rot)
    if actor:
        actor.set_actor_scale3d(unreal.Vector(${scl.x}, ${scl.y}, ${scl.z}))
        if ${ref}['label']:
            actor.set_actor_label(${ref}['label'])
        print({'success': True, 'actor': actor.get_actor_label(), 'path': str(actor.get_path_name())})
    else:
        print('ERROR: Failed to spawn actor')
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) {
      return err(`Spawn failed: ${JSON.stringify(result.body)}`);
    }
    return ok(result.body ?? 'Actor spawned');
  })
);

server.tool(
  'delete_actor',
  'Delete an actor from the current level by its label',
  {
    label: z.string().describe('Actor label (as shown in the Outliner)'),
  },
  async ({ label }) => safe(async () => {
    const { head, ref } = pyparams({ label });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found: ' + ${ref}['label'])
else:
    unreal.EditorLevelLibrary.destroy_actor(target)
    print({'success': True, 'deleted': ${ref}['label']})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) {
      return err(`Delete failed: ${JSON.stringify(result.body)}`);
    }
    return ok(result.body);
  })
);

server.tool(
  'set_actor_transform',
  'Set the location, rotation, and/or scale of an actor',
  {
    label: z.string().describe('Actor label (as shown in Outliner)'),
    location: z.object({
      x: z.number(),
      y: z.number(),
      z: z.number(),
    }).optional(),
    rotation: z.object({
      pitch: z.number(),
      yaw: z.number(),
      roll: z.number(),
    }).optional(),
    scale: z.object({
      x: z.number(),
      y: z.number(),
      z: z.number(),
    }).optional(),
  },
  async ({ label, location, rotation, scale }) => safe(async () => {
    const { head, ref } = pyparams({ label });
    const lines = [
      `import unreal`,
      head,
      `actors = unreal.EditorLevelLibrary.get_all_level_actors()`,
      `target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)`,
      `if target is None:`,
      `    print('ERROR: Actor not found: ' + ${ref}['label'])`,
      `else:`,
    ];
    if (location) lines.push(`    target.set_actor_location(unreal.Vector(${location.x}, ${location.y}, ${location.z}), False, False)`);
    if (rotation) lines.push(`    target.set_actor_rotation(unreal.Rotator(${rotation.pitch}, ${rotation.yaw}, ${rotation.roll}), False)`);
    if (scale) lines.push(`    target.set_actor_scale3d(unreal.Vector(${scale.x}, ${scale.y}, ${scale.z}))`);
    lines.push(`    print({'success': True, 'actor': target.get_actor_label()})`);

    const result = await client.executePython(lines.join('\n'));
    if (result.status >= 400) {
      return err(`Transform failed: ${JSON.stringify(result.body)}`);
    }
    return ok(result.body);
  })
);

server.tool(
  'get_actor_properties',
  'Get properties of a specific actor by label',
  {
    label: z.string().describe('Actor label'),
  },
  async ({ label }) => safe(async () => {
    const { head, ref } = pyparams({ label });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found: ' + ${ref}['label'])
else:
    loc = target.get_actor_location()
    rot = target.get_actor_rotation()
    scl = target.get_actor_scale3d()
    print({
        'label': target.get_actor_label(),
        'class': target.get_class().get_name(),
        'path': str(target.get_path_name()),
        'location': {'x': loc.x, 'y': loc.y, 'z': loc.z},
        'rotation': {'pitch': rot.pitch, 'yaw': rot.yaw, 'roll': rot.roll},
        'scale': {'x': scl.x, 'y': scl.y, 'z': scl.z},
        'hidden': target.is_hidden_ed(),
        'tags': [str(t) for t in target.tags],
    })
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_actor_visibility',
  'Show or hide an actor in the editor',
  {
    label: z.string().describe('Actor label'),
    visible: z.boolean().describe('true to show, false to hide'),
  },
  async ({ label, visible }) => safe(async () => {
    const { head, ref } = pyparams({ label });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found')
else:
    target.set_is_temporarily_hidden_in_editor(${visible ? 'False' : 'True'})
    print({'success': True, 'visible': ${visible ? 'True' : 'False'}})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Level Management ─────────────────────────────────────────────────────────

server.tool(
  'get_world_info',
  'Get information about the current Unreal world/level',
  {},
  async () => safe(async () => {
    const script = `
import unreal
world = unreal.EditorLevelLibrary.get_editor_world()
actors = unreal.EditorLevelLibrary.get_all_level_actors()
print({
    'world_name': world.get_name(),
    'path': str(world.get_path_name()),
    'actor_count': len(actors),
    'classes': list(set(a.get_class().get_name() for a in actors)),
})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'save_level',
  'Save the current level',
  {},
  async () => safe(async () => {
    const script = `
import unreal
result = unreal.EditorLevelLibrary.save_all_dirty_levels()
print({'success': result})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'open_level',
  'Open a level by asset path',
  {
    level_path: z.string().describe('Asset path to the level, e.g. "/Game/Maps/MyLevel"'),
  },
  async ({ level_path }) => safe(async () => {
    const { head, ref } = pyparams({ level_path });
    const script = `
import unreal
${head}
result = unreal.EditorLevelLibrary.load_level(${ref}['level_path'])
print({'success': result})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Asset Management ─────────────────────────────────────────────────────────

server.tool(
  'search_assets',
  'Search for assets in the Unreal content browser',
  {
    query: z.string().describe('Search query string'),
    class_filter: z.string().optional().describe('Optional class filter, e.g. "StaticMesh", "Blueprint", "Material"'),
    path_filter: z.string().optional().describe('Optional path filter, e.g. "/Game/MyFolder"'),
  },
  async ({ query, class_filter, path_filter }) => safe(async () => {
    const { head, ref } = pyparams({ query, class_filter: class_filter || '', path_filter: path_filter || '/Game' });
    const script = `
import unreal
${head}
ar = unreal.AssetRegistryHelpers.get_asset_registry()
# ARFilter fields are read-only after construction in UE5, and class_names is gone;
# filter on the class name below instead
assets = ar.get_assets(unreal.ARFilter(package_paths=[${ref}['path_filter']], recursive_paths=True))
results = []
for a in assets:
    name = str(a.asset_name)
    cls = str(a.asset_class_path.asset_name)
    if ${ref}['class_filter'] and cls.lower() != ${ref}['class_filter'].lower():
        continue
    if ${ref}['query'].lower() in name.lower():
        results.append({'name': name, 'class': cls, 'path': str(a.package_name)})
print(results[:50])
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'get_asset_info',
  'Get detailed info about a specific asset',
  {
    asset_path: z.string().describe('Full asset path, e.g. "/Game/MyFolder/MyMesh"'),
  },
  async ({ asset_path }) => safe(async () => {
    const { head, ref } = pyparams({ asset_path });
    const script = `
import unreal
${head}
asset = unreal.load_asset(${ref}['asset_path'])
if asset is None:
    print('ERROR: Asset not found: ' + ${ref}['asset_path'])
else:
    print({
        'name': asset.get_name(),
        'class': asset.get_class().get_name(),
        'path': str(asset.get_path_name()),
    })
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Blueprint Management ─────────────────────────────────────────────────────

server.tool(
  'get_blueprints',
  'List all Blueprint assets in the project',
  {
    path_filter: z.string().optional().describe('Optional path filter, e.g. "/Game/Blueprints"'),
  },
  async ({ path_filter }) => safe(async () => {
    const { head, ref } = pyparams({ path_filter: path_filter || '/Game' });
    const script = `
import unreal
${head}
ar = unreal.AssetRegistryHelpers.get_asset_registry()
assets = ar.get_assets(unreal.ARFilter(package_paths=[${ref}['path_filter']], recursive_paths=True))
results = [{'name': str(a.asset_name), 'path': str(a.package_name)} for a in assets
           if str(a.asset_class_path.asset_name) == 'Blueprint']
print(results)
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'compile_blueprint',
  'Compile a Blueprint asset',
  {
    asset_path: z.string().describe('Asset path to the Blueprint, e.g. "/Game/Blueprints/MyBlueprint"'),
  },
  async ({ asset_path }) => safe(async () => {
    const { head, ref } = pyparams({ asset_path });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['asset_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['asset_path'])
else:
    unreal.KismetEditorUtilities.compile_blueprint(bp)
    print({'success': True, 'blueprint': ${ref}['asset_path']})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Editor Control ───────────────────────────────────────────────────────────

server.tool(
  'play_in_editor',
  'Start Play-in-Editor (PIE)',
  {
    mobile_preview: z.boolean().optional().default(false).describe('Launch as mobile preview'),
  },
  async ({ mobile_preview }) => safe(async () => {
    const script = `
import unreal
settings = unreal.EditorPlayInSettings()
settings.set_editor_property('new_window_width', 1280)
settings.set_editor_property('new_window_height', 720)
unreal.UnrealEditorSubsystem().play_in_editor(settings, ${mobile_preview ? 'True' : 'False'})
print({'success': True})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body ?? 'PIE started');
  })
);

server.tool(
  'stop_play_in_editor',
  'Stop Play-in-Editor (PIE)',
  {},
  async () => safe(async () => {
    const script = `
import unreal
unreal.UnrealEditorSubsystem().end_play_map()
print({'success': True})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body ?? 'PIE stopped');
  })
);

server.tool(
  'undo',
  'Undo the last operation in Unreal Editor',
  {},
  async () => safe(async () => {
    const result = await client.callFunction(
      '/Script/UnrealEd.Default__EditorEngine',
      'UndoTransaction',
      {},
      false
    );
    if (result.status >= 400) return err(`Undo failed: ${JSON.stringify(result.body)}`);
    return ok('Undo successful');
  })
);

server.tool(
  'get_selected_actors',
  'Get currently selected actors in the editor',
  {},
  async () => safe(async () => {
    const script = `
import unreal
selected = unreal.EditorLevelLibrary.get_selected_level_actors()
result = [{'label': a.get_actor_label(), 'class': a.get_class().get_name(), 'path': str(a.get_path_name())} for a in selected]
print(result)
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'select_actors_by_label',
  'Select actors in the editor by their labels',
  {
    labels: z.array(z.string()).describe('List of actor labels to select'),
  },
  async ({ labels }) => safe(async () => {
    const labelsJson = JSON.stringify(labels);
    const script = `
import unreal
labels = ${labelsJson}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
targets = [a for a in actors if a.get_actor_label() in labels]
unreal.EditorLevelLibrary.set_selected_level_actors(targets)
print({'success': True, 'selected': [a.get_actor_label() for a in targets]})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Material / Mesh ──────────────────────────────────────────────────────────

server.tool(
  'set_static_mesh_material',
  'Set a material on a StaticMeshActor',
  {
    actor_label: z.string().describe('Label of the StaticMeshActor'),
    material_path: z.string().describe('Asset path to the material, e.g. "/Game/Materials/MyMat"'),
    slot_index: z.number().int().min(0).default(0).describe('Material slot index'),
  },
  async ({ actor_label, material_path, slot_index }) => safe(async () => {
    const { head, ref } = pyparams({ actor_label, material_path });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['actor_label']), None)
if target is None:
    print('ERROR: Actor not found')
else:
    mat = unreal.load_asset(${ref}['material_path'])
    if mat is None:
        print('ERROR: Material not found: ' + ${ref}['material_path'])
    else:
        mesh_comp = target.get_component_by_class(unreal.StaticMeshComponent)
        if mesh_comp is None:
            print('ERROR: No StaticMeshComponent found on actor')
        else:
            mesh_comp.set_material(${slot_index}, mat)
            print({'success': True})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// Starting values per preset; any value passed to create_material overrides them
const MATERIAL_PRESETS = {
  opaque: { base_color: [0.8, 0.8, 0.8], metallic: 0, roughness: 0.5, specular: 0.5, opacity: 1, ior: 1, two_sided: false },
  // Translucent, slightly blue-tinted, more see-through head-on than at grazing angles
  glass: { base_color: [0.85, 0.95, 1.0], metallic: 0, roughness: 0.03, specular: 1, opacity: 0.15, ior: 1.5, two_sided: true },
  metal: { base_color: [0.9, 0.9, 0.9], metallic: 1, roughness: 0.25, specular: 0.5, opacity: 1, ior: 1, two_sided: false },
};

server.tool(
  'create_material',
  'Create (or overwrite) a material asset from a preset: glass, metal or opaque. ' +
  'Any of the values overrides the preset. Saved to disk; apply it with set_static_mesh_material',
  {
    name: z.string().regex(/^[A-Za-z0-9_]+$/).describe('Asset name, e.g. "M_Glass"'),
    path: z.string().regex(/^\/Game(\/[A-Za-z0-9_]+)*$/).default('/Game/BIMBot/Materials').describe('Content folder'),
    preset: z.enum(['glass', 'metal', 'opaque']).default('opaque'),
    base_color: z.array(z.number().min(0).max(1)).length(3).optional().describe('Linear RGB, 0–1'),
    metallic: z.number().min(0).max(1).optional(),
    roughness: z.number().min(0).max(1).optional(),
    specular: z.number().min(0).max(1).optional(),
    opacity: z.number().min(0).max(1).optional().describe('Below 1 makes the material translucent'),
    ior: z.number().min(1).max(3).optional().describe('Index of refraction for translucent materials (glass 1.5, water 1.33)'),
    two_sided: z.boolean().optional(),
    overwrite: z.boolean().default(false).describe('Replace an existing material of the same name'),
  },
  async ({ name, path: folder, preset, overwrite, ...overrides }) => safe(async () => {
    const values = { ...MATERIAL_PRESETS[preset] };
    for (const [k, v] of Object.entries(overrides)) if (v !== undefined) values[k] = v;
    const { head, ref } = pyparams({ name, folder, overwrite, ...values });
    const script = `
import unreal
${head}
mel = unreal.MaterialEditingLibrary
eal = unreal.EditorAssetLibrary
full = ${ref}['folder'] + '/' + ${ref}['name']
if eal.does_asset_exist(full) and not ${ref}['overwrite']:
    raise Exception(full + ' already exists; pass overwrite=true to replace it')
mat = eal.load_asset(full) if eal.does_asset_exist(full) else unreal.AssetToolsHelpers.get_asset_tools().create_asset(
    ${ref}['name'], ${ref}['folder'], unreal.Material, unreal.MaterialFactoryNew())
if not isinstance(mat, unreal.Material):
    raise Exception(full + ' exists and is not a Material')

translucent = ${ref}['opacity'] < 1
mat.set_editor_property('blend_mode', unreal.BlendMode.BLEND_TRANSLUCENT if translucent else unreal.BlendMode.BLEND_OPAQUE)
mat.set_editor_property('two_sided', ${ref}['two_sided'])
if translucent:
    # Per-pixel lit translucency gives glass its highlights and reflections
    mat.set_editor_property('translucency_lighting_mode', unreal.TranslucencyLightingMode.TLM_SURFACE_PER_PIXEL_LIGHTING)

mel.delete_all_material_expressions(mat)
P = unreal.MaterialProperty
def scalar(value, y, prop):
    n = mel.create_material_expression(mat, unreal.MaterialExpressionConstant, -400, y)
    n.set_editor_property('r', value)
    mel.connect_material_property(n, '', prop)

color = mel.create_material_expression(mat, unreal.MaterialExpressionConstant3Vector, -400, -200)
color.set_editor_property('constant', unreal.LinearColor(*${ref}['base_color'], 1.0))
mel.connect_material_property(color, '', P.MP_BASE_COLOR)
scalar(${ref}['metallic'], -50, P.MP_METALLIC)
scalar(${ref}['specular'], 50, P.MP_SPECULAR)
scalar(${ref}['roughness'], 150, P.MP_ROUGHNESS)
if translucent:
    # Fresnel: the given opacity head-on, rising toward opaque at grazing angles
    fres = mel.create_material_expression(mat, unreal.MaterialExpressionFresnel, -650, 300)
    lerp = mel.create_material_expression(mat, unreal.MaterialExpressionLinearInterpolate, -400, 300)
    lerp.set_editor_property('const_a', ${ref}['opacity'])
    lerp.set_editor_property('const_b', min(1.0, ${ref}['opacity'] + 0.45))
    mel.connect_material_expressions(fres, '', lerp, 'Alpha')
    mel.connect_material_property(lerp, '', P.MP_OPACITY)
    scalar(${ref}['ior'], 450, P.MP_REFRACTION)

mel.recompile_material(mat)
eal.save_asset(full)
print({'success': True, 'material': full, 'blend_mode': 'translucent' if translucent else 'opaque'})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Remote Control Direct API ────────────────────────────────────────────────

server.tool(
  'call_remote_function',
  'Directly call a function on a UObject via the Remote Control API',
  {
    object_path: z.string().describe('Full UObject path, e.g. "/Script/Engine.Default__GameplayStatics"'),
    function_name: z.string().describe('Function name to call'),
    parameters: z.record(z.string(), z.unknown()).optional().describe('Function parameters as key-value pairs'),
    generate_transaction: z.boolean().optional().default(true),
  },
  async ({ object_path, function_name, parameters, generate_transaction }) => safe(async () => {
    const result = await client.callFunction(object_path, function_name, parameters || {}, generate_transaction ?? true);
    if (result.status >= 400) return err(`Function call failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'get_remote_property',
  'Get a property value from a UObject via the Remote Control API',
  {
    object_path: z.string().describe('Full UObject path'),
    property_name: z.string().describe('Property name'),
  },
  async ({ object_path, property_name }) => safe(async () => {
    const result = await client.getProperty(object_path, property_name);
    if (result.status >= 400) return err(`Get property failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_remote_property',
  'Set a property value on a UObject via the Remote Control API',
  {
    object_path: z.string().describe('Full UObject path'),
    property_name: z.string().describe('Property name'),
    property_value: z.unknown().describe('New property value'),
  },
  async ({ object_path, property_name, property_value }) => safe(async () => {
    const result = await client.setProperty(object_path, property_name, property_value);
    if (result.status >= 400) return err(`Set property failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Lighting ─────────────────────────────────────────────────────────────────

server.tool(
  'build_lighting',
  'Build lighting in the current level',
  {
    quality: z.enum(['Preview', 'Medium', 'High', 'Production']).optional().default('Preview'),
  },
  async ({ quality }) => safe(async () => {
    // Map quality string to UE LightingBuildQuality enum value
    const qualityMap = { Preview: 0, Medium: 1, High: 2, Production: 3 };
    const qualityInt = qualityMap[quality] ?? 0;
    const { head, ref } = pyparams({ quality });
    const script = `
import unreal
${head}
options = unreal.LightingBuildOptions()
options.quality = unreal.LightingBuildQuality(${qualityInt})
unreal.EditorLevelLibrary.build_lighting(options)
print({'success': True, 'quality': ${ref}['quality']})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body ?? 'Lighting build started');
  })
);

// ─── Editor Tools ─────────────────────────────────────────────────────────────
// These use the UnrealMCPBridge C++ plugin when it is installed, otherwise Python
// (see the bridge methods in unreal-client.js).

server.tool(
  'capture_viewport',
  'Capture a screenshot of the active Unreal Editor viewport',
  {
    width: z.number().int().min(1).max(7680).optional().default(1920).describe('Screenshot width in pixels'),
    height: z.number().int().min(1).max(4320).optional().default(1080).describe('Screenshot height in pixels'),
  },
  async ({ width, height }) => safe(async () => {
    const result = await client.screenshot({ width, height });
    if (result.status >= 400) return err(`Screenshot failed: ${JSON.stringify(result.body)}`);

    // If the response contains base64 image data, return it as an image
    if (result.body?.image) {
      return {
        content: [
          { type: 'image', data: result.body.image, mimeType: 'image/png' },
          { type: 'text', text: `Viewport screenshot captured (${width}x${height})` },
        ],
      };
    }
    return ok(result.body);
  })
);

server.tool(
  'execute_console_command',
  'Execute a console command in the Unreal Editor',
  {
    command: z.string().describe('Console command to execute, e.g. "stat fps", "obj list"'),
  },
  async ({ command }) => safe(async () => {
    const result = await client.consoleCommand(command);
    if (result.status >= 400) return err(`Console command failed: ${JSON.stringify(result.body)}`);
    return ok(result.body ?? 'Command executed');
  })
);

server.tool(
  'show_notification',
  'Show a message in the Unreal Editor: a toast with the UnrealMCPBridge plugin, otherwise an Output Log line',
  {
    message: z.string().describe('Message to display'),
    severity: z.enum(['info', 'warning', 'error']).optional().default('info').describe('Notification severity'),
    duration: z.number().optional().default(5.0).describe('How long to show the notification (seconds)'),
  },
  async ({ message, severity, duration }) => safe(async () => {
    const result = await client.notify(message, severity, duration);
    if (result.status >= 400) return err(`Notification failed: ${JSON.stringify(result.body)}`);
    return ok(result.body ?? 'Notification shown');
  })
);

server.tool(
  'get_project_info',
  'Get project metadata including engine version, project name, and enabled plugins',
  {},
  async () => safe(async () => {
    const result = await client.getProjectInfo();
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'import_asset',
  'Import a file into the Unreal content browser',
  {
    file_path: z.string().describe('Absolute path to the file to import, e.g. "C:/Models/chair.fbx"'),
    destination_path: z.string().describe('Content browser destination, e.g. "/Game/Models"'),
  },
  async ({ file_path, destination_path }) => safe(async () => {
    const result = await client.importAsset(file_path, destination_path);
    if (result.status >= 400) return err(`Import failed: ${JSON.stringify(result.body)}`);
    return ok(result.body ?? 'Asset imported');
  })
);

// ─── Actor Extras ─────────────────────────────────────────────────────────────

server.tool(
  'find_actors_by_name',
  'Find actors in the current level whose label matches a pattern (case-insensitive substring or glob)',
  {
    pattern: z.string().describe('Search pattern — matched as a case-insensitive substring of the actor label'),
    class_filter: z.string().optional().describe('Optional class name filter, e.g. "StaticMeshActor"'),
  },
  async ({ pattern, class_filter }) => safe(async () => {
    const { head, ref } = pyparams({ pattern, class_filter: class_filter || '' });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
pat = ${ref}['pattern'].lower()
cls = ${ref}['class_filter'].lower()
results = []
for a in actors:
    label = a.get_actor_label()
    class_name = a.get_class().get_name()
    if pat in label.lower():
        if not cls or cls in class_name.lower():
            results.append({
                'label': label,
                'class': class_name,
                'path': str(a.get_path_name()),
                'location': {'x': a.get_actor_location().x, 'y': a.get_actor_location().y, 'z': a.get_actor_location().z},
            })
print(results)
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_actor_property',
  'Set a single named property on an actor by its label',
  {
    label: z.string().describe('Actor label'),
    property_name: z.string().describe('Property name, e.g. "bHidden", "CustomDepthStencilValue"'),
    property_value: z.unknown().describe('New property value'),
  },
  async ({ label, property_name, property_value }) => safe(async () => {
    const { head, ref } = pyparams({ label, property_name, property_value });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found: ' + ${ref}['label'])
else:
    target.set_editor_property(${ref}['property_name'], ${ref}['property_value'])
    print({'success': True, 'label': ${ref}['label'], 'property': ${ref}['property_name']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'spawn_blueprint_actor',
  'Spawn an actor in the level from a Blueprint asset path',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint, e.g. "/Game/Blueprints/BP_MyActor"'),
    actor_name: z.string().optional().describe('Label to give the spawned actor'),
    location: z.object({ x: z.number(), y: z.number(), z: z.number() }).optional(),
    rotation: z.object({ pitch: z.number(), yaw: z.number(), roll: z.number() }).optional(),
    scale: z.object({ x: z.number(), y: z.number(), z: z.number() }).optional(),
  },
  async ({ blueprint_path, actor_name, location, rotation, scale }) => safe(async () => {
    const loc = location || { x: 0, y: 0, z: 0 };
    const rot = rotation || { pitch: 0, yaw: 0, roll: 0 };
    const scl = scale || { x: 1, y: 1, z: 1 };
    const { head, ref } = pyparams({ blueprint_path, actor_name: actor_name || null });
    const script = `
import unreal
${head}
bp_class = unreal.EditorAssetLibrary.load_blueprint_class(${ref}['blueprint_path'])
if bp_class is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    loc = unreal.Vector(${loc.x}, ${loc.y}, ${loc.z})
    rot = unreal.Rotator(${rot.pitch}, ${rot.yaw}, ${rot.roll})
    actor = unreal.EditorLevelLibrary.spawn_actor_from_class(bp_class, loc, rot)
    if actor is None:
        print('ERROR: Failed to spawn Blueprint actor')
    else:
        actor.set_actor_scale3d(unreal.Vector(${scl.x}, ${scl.y}, ${scl.z}))
        if ${ref}['actor_name']:
            actor.set_actor_label(${ref}['actor_name'])
        print({'success': True, 'actor': actor.get_actor_label(), 'path': str(actor.get_path_name())})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Blueprint Class Tools ────────────────────────────────────────────────────

server.tool(
  'create_blueprint',
  'Create a new Blueprint class asset in the content browser',
  {
    name: z.string().describe('Name of the new Blueprint asset, e.g. "BP_MyActor"'),
    path: z.string().optional().default('/Game/Blueprints').describe('Content browser folder path'),
    parent_class: z.string().optional().default('/Script/Engine.Actor').describe(
      'Parent class path, e.g. "/Script/Engine.Actor", "/Script/Engine.Pawn", "/Script/Engine.Character", "/Script/Engine.GameModeBase"'
    ),
  },
  async ({ name, path, parent_class }) => safe(async () => {
    const { head, ref } = pyparams({ name, path, parent_class });
    const script = `
import unreal
${head}
parent_cls = unreal.load_class(None, ${ref}['parent_class'])
if parent_cls is None:
    print('ERROR: Parent class not found: ' + ${ref}['parent_class'])
else:
    factory = unreal.BlueprintFactory()
    factory.set_editor_property('parent_class', parent_cls)
    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    bp = asset_tools.create_asset(${ref}['name'], ${ref}['path'], unreal.Blueprint, factory)
    if bp is None:
        print('ERROR: Failed to create Blueprint — asset may already exist')
    else:
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'name': ${ref}['name'], 'path': str(bp.get_path_name())})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_component_to_blueprint',
  'Add a component to a Blueprint\'s component tree',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    component_type: z.string().describe(
      'Component class name without U prefix, e.g. "StaticMeshComponent", "PointLightComponent", "SphereComponent", "CameraComponent"'
    ),
    component_name: z.string().describe('Name to give the new component'),
    location: z.object({ x: z.number(), y: z.number(), z: z.number() }).optional().describe('Relative location'),
    rotation: z.object({ pitch: z.number(), yaw: z.number(), roll: z.number() }).optional().describe('Relative rotation'),
    scale: z.object({ x: z.number(), y: z.number(), z: z.number() }).optional().describe('Relative scale'),
  },
  async ({ blueprint_path, component_type, component_name, location, rotation, scale }) => safe(async () => {
    const loc = location || { x: 0, y: 0, z: 0 };
    const rot = rotation || { pitch: 0, yaw: 0, roll: 0 };
    const scl = scale || { x: 1, y: 1, z: 1 };
    const { head, ref } = pyparams({ blueprint_path, component_type, component_name });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    comp_class = unreal.load_class(None, '/Script/Engine.' + ${ref}['component_type'])
    if comp_class is None:
        print('ERROR: Component class not found: ' + ${ref}['component_type'])
    else:
        subsystem = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
        handles = subsystem.k2_gather_subobject_data_for_blueprint(bp)
        root_handle = handles[0]
        params = unreal.AddNewSubobjectParams(
            parent_handle=root_handle,
            new_class=comp_class,
            blueprint_context=bp
        )
        sub_handle, fail_reason = subsystem.add_new_subobject(params)
        if fail_reason:
            print('ERROR: ' + str(fail_reason))
        else:
            subsystem.rename_subobject(sub_handle, unreal.Text(${ref}['component_name']))
            data = subsystem.k2_find_subobject_data_from_handle(sub_handle)
            obj = unreal.SubobjectDataBlueprintFunctionLibrary.get_object(data)
            if obj and hasattr(obj, 'set_relative_location'):
                obj.set_relative_location(unreal.Vector(${loc.x}, ${loc.y}, ${loc.z}), False, False)
                obj.set_relative_rotation(unreal.Rotator(${rot.pitch}, ${rot.yaw}, ${rot.roll}), False)
                obj.set_relative_scale3d(unreal.Vector(${scl.x}, ${scl.y}, ${scl.z}))
            unreal.KismetEditorUtilities.compile_blueprint(bp)
            print({'success': True, 'component': ${ref}['component_name'], 'type': ${ref}['component_type']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_static_mesh_properties',
  'Assign a static mesh and optional material to a StaticMeshComponent inside a Blueprint',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    component_name: z.string().describe('Name of the StaticMeshComponent inside the Blueprint'),
    static_mesh_path: z.string().optional().default('/Engine/BasicShapes/Cube.Cube').describe('Asset path to the StaticMesh'),
    material_path: z.string().optional().describe('Asset path to the Material (optional)'),
  },
  async ({ blueprint_path, component_name, static_mesh_path, material_path }) => safe(async () => {
    const { head, ref } = pyparams({ blueprint_path, component_name, static_mesh_path, material_path: material_path || null });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    subsystem = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
    handles = subsystem.k2_gather_subobject_data_for_blueprint(bp)
    comp = None
    for handle in handles:
        data = subsystem.k2_find_subobject_data_from_handle(handle)
        obj = unreal.SubobjectDataBlueprintFunctionLibrary.get_object(data)
        if obj and obj.get_name() == ${ref}['component_name']:
            comp = obj
            break
    if comp is None:
        print('ERROR: Component not found: ' + ${ref}['component_name'])
    elif not isinstance(comp, unreal.StaticMeshComponent):
        print('ERROR: Component is not a StaticMeshComponent')
    else:
        mesh = unreal.load_asset(${ref}['static_mesh_path'])
        if mesh:
            comp.set_editor_property('static_mesh', mesh)
        if ${ref}['material_path']:
            mat = unreal.load_asset(${ref}['material_path'])
            if mat:
                comp.set_material(0, mat)
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'component': ${ref}['component_name'], 'mesh': ${ref}['static_mesh_path']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_component_property',
  'Set any property on a named component inside a Blueprint',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    component_name: z.string().describe('Name of the component'),
    property_name: z.string().describe('Property name'),
    property_value: z.unknown().describe('New property value'),
  },
  async ({ blueprint_path, component_name, property_name, property_value }) => safe(async () => {
    const { head, ref } = pyparams({ blueprint_path, component_name, property_name, property_value });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    subsystem = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
    handles = subsystem.k2_gather_subobject_data_for_blueprint(bp)
    comp = None
    for handle in handles:
        data = subsystem.k2_find_subobject_data_from_handle(handle)
        obj = unreal.SubobjectDataBlueprintFunctionLibrary.get_object(data)
        if obj and obj.get_name() == ${ref}['component_name']:
            comp = obj
            break
    if comp is None:
        print('ERROR: Component not found: ' + ${ref}['component_name'])
    else:
        comp.set_editor_property(${ref}['property_name'], ${ref}['property_value'])
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'component': ${ref}['component_name'], 'property': ${ref}['property_name']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_physics_properties',
  'Configure physics simulation on a PrimitiveComponent inside a Blueprint',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    component_name: z.string().describe('Name of the PrimitiveComponent'),
    simulate_physics: z.boolean().optional().default(false),
    gravity_enabled: z.boolean().optional().default(true),
    mass: z.number().optional().describe('Override mass in kg (omit to auto-calculate from density)'),
    linear_damping: z.number().optional().default(0.01),
    angular_damping: z.number().optional().default(0.0),
  },
  async ({ blueprint_path, component_name, simulate_physics, gravity_enabled, mass, linear_damping, angular_damping }) => safe(async () => {
    const { head, ref } = pyparams({ blueprint_path, component_name });
    const massLines = mass !== undefined
      ? `        comp.set_editor_property('mass_override_in_kg', ${mass})\n        comp.set_editor_property('override_mass', True)`
      : '';
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    subsystem = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
    handles = subsystem.k2_gather_subobject_data_for_blueprint(bp)
    comp = None
    for handle in handles:
        data = subsystem.k2_find_subobject_data_from_handle(handle)
        obj = unreal.SubobjectDataBlueprintFunctionLibrary.get_object(data)
        if obj and obj.get_name() == ${ref}['component_name']:
            comp = obj
            break
    if comp is None:
        print('ERROR: Component not found: ' + ${ref}['component_name'])
    elif not isinstance(comp, unreal.PrimitiveComponent):
        print('ERROR: Component is not a PrimitiveComponent')
    else:
        comp.set_editor_property('simulate_physics', ${simulate_physics ? 'True' : 'False'})
        comp.set_editor_property('enable_gravity', ${gravity_enabled ? 'True' : 'False'})
        comp.set_editor_property('linear_damping', ${linear_damping ?? 0.01})
        comp.set_editor_property('angular_damping', ${angular_damping ?? 0.0})
${massLines}
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'component': ${ref}['component_name'], 'simulate_physics': ${simulate_physics ? 'True' : 'False'}})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_blueprint_property',
  'Set a property on the Blueprint\'s Class Default Object (CDO)',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    property_name: z.string().describe('Property name'),
    property_value: z.unknown().describe('New property value'),
  },
  async ({ blueprint_path, property_name, property_value }) => safe(async () => {
    const { head, ref } = pyparams({ blueprint_path, property_name, property_value });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    bp_class = unreal.load_class(None, str(bp.get_path_name()) + '_C')
    if bp_class is None:
        print('ERROR: Blueprint class not found — compile the Blueprint first')
    else:
        cdo = unreal.get_default_object(bp_class)
        cdo.set_editor_property(${ref}['property_name'], ${ref}['property_value'])
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'property': ${ref}['property_name']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_pawn_properties',
  'Set Pawn-specific Class Default Object properties on a Blueprint that inherits from Pawn or Character',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    auto_possess_player: z.enum(['Disabled', 'Player0', 'Player1', 'Player2', 'Player3']).optional(),
    use_controller_rotation_yaw: z.boolean().optional(),
    use_controller_rotation_pitch: z.boolean().optional(),
    use_controller_rotation_roll: z.boolean().optional(),
    can_be_damaged: z.boolean().optional(),
  },
  async ({ blueprint_path, auto_possess_player, use_controller_rotation_yaw, use_controller_rotation_pitch, use_controller_rotation_roll, can_be_damaged }) => safe(async () => {
    const { head, ref } = pyparams({ blueprint_path });
    const possessMap = { Disabled: 0, Player0: 1, Player1: 2, Player2: 3, Player3: 4 };
    const propLines = [];
    if (auto_possess_player !== undefined)
      propLines.push(`        cdo.set_editor_property('auto_possess_player', unreal.AutoReceiveInput(${possessMap[auto_possess_player]}))`);
    if (use_controller_rotation_yaw !== undefined)
      propLines.push(`        cdo.set_editor_property('use_controller_rotation_yaw', ${use_controller_rotation_yaw ? 'True' : 'False'})`);
    if (use_controller_rotation_pitch !== undefined)
      propLines.push(`        cdo.set_editor_property('use_controller_rotation_pitch', ${use_controller_rotation_pitch ? 'True' : 'False'})`);
    if (use_controller_rotation_roll !== undefined)
      propLines.push(`        cdo.set_editor_property('use_controller_rotation_roll', ${use_controller_rotation_roll ? 'True' : 'False'})`);
    if (can_be_damaged !== undefined)
      propLines.push(`        cdo.set_editor_property('can_be_damaged', ${can_be_damaged ? 'True' : 'False'})`);
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    bp_class = unreal.load_class(None, str(bp.get_path_name()) + '_C')
    if bp_class is None:
        print('ERROR: Blueprint class not found — compile the Blueprint first')
    else:
        cdo = unreal.get_default_object(bp_class)
${propLines.join('\n')}
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'blueprint': ${ref}['blueprint_path']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Blueprint Node Graph Tools ───────────────────────────────────────────────

server.tool(
  'add_blueprint_variable',
  'Add a member variable to a Blueprint',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    variable_name: z.string().describe('Variable name'),
    variable_type: z.enum(['Boolean', 'Integer', 'Float', 'String', 'Vector', 'Rotator', 'Transform', 'Object']).describe('Variable type'),
    is_instance_editable: z.boolean().optional().default(false).describe('Expose the variable on instances in the editor'),
  },
  async ({ blueprint_path, variable_name, variable_type, is_instance_editable }) => safe(async () => {
    const { head, ref } = pyparams({ blueprint_path, variable_name, variable_type });
    // Map type to (category, subcategory) for UE5 pin type
    const typeMap = {
      Boolean:   "('bool', '')",
      Integer:   "('int', '')",
      Float:     "('real', '')",
      String:    "('string', '')",
      Vector:    "('struct', '/Script/CoreUObject.Vector')",
      Rotator:   "('struct', '/Script/CoreUObject.Rotator')",
      Transform: "('struct', '/Script/CoreUObject.Transform')",
      Object:    "('object', '/Script/CoreUObject.Object')",
    };
    const pinTypeTuple = typeMap[variable_type] || "('bool', '')";
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    cat, sub = ${pinTypeTuple}
    new_var = unreal.BPVariableDescription()
    new_var.var_name = unreal.Name(${ref}['variable_name'])
    pin_type = unreal.EditorBlueprintLibrary.make_editor_pin_info_from_type(cat, sub)
    new_var.var_type = pin_type
    new_var.property_flags = 0x0000000000000004 if ${is_instance_editable ? 'True' : 'False'} else 0
    bp.new_variables.append(new_var)
    unreal.KismetEditorUtilities.compile_blueprint(bp)
    print({'success': True, 'variable': ${ref}['variable_name'], 'type': ${ref}['variable_type']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'find_blueprint_nodes',
  'Search a Blueprint\'s event graph for nodes by type or event name',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    node_type: z.string().optional().describe('Node class name filter, e.g. "K2Node_Event", "K2Node_CallFunction"'),
    event_name: z.string().optional().describe('Event name to search for, e.g. "ReceiveBeginPlay"'),
  },
  async ({ blueprint_path, node_type, event_name }) => safe(async () => {
    const { head, ref } = pyparams({ blueprint_path, node_type: node_type || '', event_name: event_name || '' });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    graphs = unreal.BlueprintEditorLibrary.get_blueprint_event_graphs(bp)
    results = []
    node_filter = ${ref}['node_type'].lower()
    event_filter = ${ref}['event_name'].lower()
    for graph in graphs:
        for node in graph.nodes:
            class_name = node.get_class().get_name().lower()
            node_name = node.get_name().lower()
            if node_filter and node_filter not in class_name:
                continue
            if event_filter and event_filter not in node_name:
                continue
            results.append({
                'node_id': str(node.get_name()),
                'node_class': node.get_class().get_name(),
                'graph': graph.get_name(),
                'position': {'x': int(node.node_pos_x), 'y': int(node.node_pos_y)},
            })
    print(results)
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_blueprint_event_node',
  'Add an event node to a Blueprint\'s event graph (e.g. BeginPlay, Tick, custom events)',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    event_name: z.string().describe(
      'Event name — use "ReceiveBeginPlay" for BeginPlay, "ReceiveTick" for Tick, or a custom event name'
    ),
    node_position: z.object({ x: z.number(), y: z.number() }).optional().describe('Position in the graph canvas'),
  },
  async ({ blueprint_path, event_name, node_position }) => safe(async () => {
    const pos = node_position || { x: 0, y: 0 };
    const { head, ref } = pyparams({ blueprint_path, event_name });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    graphs = unreal.BlueprintEditorLibrary.get_blueprint_event_graphs(bp)
    if not graphs:
        print('ERROR: No event graphs found in Blueprint')
    else:
        graph = graphs[0]
        node = unreal.BlueprintEditorLibrary.add_new_function_graph(bp, ${ref}['event_name'])
        if node is None:
            # Try adding as an override event
            unreal.BlueprintEditorLibrary.add_function_to_blueprint_graph(bp, ${ref}['event_name'])
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'event': ${ref}['event_name'], 'graph': graph.get_name()})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_blueprint_input_action_node',
  'Add an InputAction event node to a Blueprint\'s event graph',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    action_name: z.string().describe('Input action name as defined in Project Settings > Input, e.g. "Jump"'),
    node_position: z.object({ x: z.number(), y: z.number() }).optional(),
  },
  async ({ blueprint_path, action_name, node_position }) => safe(async () => {
    const pos = node_position || { x: 0, y: 0 };
    const { head, ref } = pyparams({ blueprint_path, action_name });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    graphs = unreal.BlueprintEditorLibrary.get_blueprint_event_graphs(bp)
    if not graphs:
        print('ERROR: No event graphs found')
    else:
        graph = graphs[0]
        # Add InputAction node via function graph API
        action_node_name = 'InpActEvt_' + ${ref}['action_name']
        unreal.BlueprintEditorLibrary.add_function_to_blueprint_graph(bp, action_node_name)
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'action': ${ref}['action_name']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_blueprint_function_node',
  'Add a function call node to a Blueprint\'s event graph',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    function_name: z.string().describe('Function name, e.g. "SetActorLocation", "PrintString"'),
    target: z.string().optional().describe('Target component name or "self" (defaults to "self")'),
    node_position: z.object({ x: z.number(), y: z.number() }).optional(),
  },
  async ({ blueprint_path, function_name, target, node_position }) => safe(async () => {
    const pos = node_position || { x: 200, y: 0 };
    const { head, ref } = pyparams({ blueprint_path, function_name, target: target || 'self' });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    graphs = unreal.BlueprintEditorLibrary.get_blueprint_event_graphs(bp)
    if not graphs:
        print('ERROR: No event graphs found')
    else:
        graph = graphs[0]
        unreal.BlueprintEditorLibrary.add_function_to_blueprint_graph(bp, ${ref}['function_name'])
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'function': ${ref}['function_name'], 'target': ${ref}['target']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'connect_blueprint_nodes',
  'Connect two nodes in a Blueprint event graph by their pin names',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    source_node_id: z.string().describe('Source node name/ID (from find_blueprint_nodes)'),
    source_pin: z.string().describe('Source pin name, e.g. "then", "ReturnValue"'),
    target_node_id: z.string().describe('Target node name/ID'),
    target_pin: z.string().describe('Target pin name, e.g. "execute", "NewLocation"'),
  },
  async ({ blueprint_path, source_node_id, source_pin, target_node_id, target_pin }) => safe(async () => {
    const { head, ref } = pyparams({ blueprint_path, source_node_id, source_pin, target_node_id, target_pin });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    graphs = unreal.BlueprintEditorLibrary.get_blueprint_event_graphs(bp)
    src_node = None
    tgt_node = None
    for graph in graphs:
        for node in graph.nodes:
            if node.get_name() == ${ref}['source_node_id']:
                src_node = node
            if node.get_name() == ${ref}['target_node_id']:
                tgt_node = node
    if src_node is None:
        print('ERROR: Source node not found: ' + ${ref}['source_node_id'])
    elif tgt_node is None:
        print('ERROR: Target node not found: ' + ${ref}['target_node_id'])
    else:
        src_pin = next((p for p in src_node.get_all_pins() if p.get_name() == ${ref}['source_pin']), None)
        tgt_pin = next((p for p in tgt_node.get_all_pins() if p.get_name() == ${ref}['target_pin']), None)
        if src_pin is None:
            print('ERROR: Source pin not found: ' + ${ref}['source_pin'])
        elif tgt_pin is None:
            print('ERROR: Target pin not found: ' + ${ref}['target_pin'])
        else:
            schema = src_node.get_graph().get_schema()
            can_connect = schema.can_create_connection(src_pin, tgt_pin)
            if can_connect.response == unreal.CanCreateConnectionResponse.DISALLOW:
                print('ERROR: Cannot connect pins: ' + str(can_connect.message))
            else:
                schema.try_create_connection(src_pin, tgt_pin)
                unreal.KismetEditorUtilities.compile_blueprint(bp)
                print({'success': True, 'from': ${ref}['source_node_id'] + '.' + ${ref}['source_pin'], 'to': ${ref}['target_node_id'] + '.' + ${ref}['target_pin']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_blueprint_get_self_component_reference',
  'Add a getter node for a component owned by the Blueprint actor',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    component_name: z.string().describe('Name of the component to create a getter for'),
    node_position: z.object({ x: z.number(), y: z.number() }).optional(),
  },
  async ({ blueprint_path, component_name, node_position }) => safe(async () => {
    const pos = node_position || { x: -200, y: 0 };
    const { head, ref } = pyparams({ blueprint_path, component_name });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    graphs = unreal.BlueprintEditorLibrary.get_blueprint_event_graphs(bp)
    if not graphs:
        print('ERROR: No event graphs found')
    else:
        graph = graphs[0]
        # Add a "Get <ComponentName>" variable getter node
        getter_name = 'Get' + ${ref}['component_name']
        unreal.BlueprintEditorLibrary.add_function_to_blueprint_graph(bp, getter_name)
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'component': ${ref}['component_name']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_blueprint_self_reference',
  'Add a "Get Self" reference node to a Blueprint\'s event graph',
  {
    blueprint_path: z.string().describe('Asset path to the Blueprint'),
    node_position: z.object({ x: z.number(), y: z.number() }).optional(),
  },
  async ({ blueprint_path, node_position }) => safe(async () => {
    const pos = node_position || { x: -200, y: 100 };
    const { head, ref } = pyparams({ blueprint_path });
    const script = `
import unreal
${head}
bp = unreal.load_asset(${ref}['blueprint_path'])
if bp is None:
    print('ERROR: Blueprint not found: ' + ${ref}['blueprint_path'])
else:
    graphs = unreal.BlueprintEditorLibrary.get_blueprint_event_graphs(bp)
    if not graphs:
        print('ERROR: No event graphs found')
    else:
        graph = graphs[0]
        unreal.BlueprintEditorLibrary.add_function_to_blueprint_graph(bp, 'Self')
        unreal.KismetEditorUtilities.compile_blueprint(bp)
        print({'success': True, 'node': 'SelfReference', 'graph': graph.get_name()})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── UMG / Widget Blueprint Tools ────────────────────────────────────────────

server.tool(
  'create_umg_widget_blueprint',
  'Create a new UMG Widget Blueprint with a Canvas Panel root',
  {
    widget_name: z.string().describe('Name of the new Widget Blueprint, e.g. "WBP_HUD"'),
    path: z.string().optional().default('/Game/UI').describe('Content browser folder path'),
    parent_class: z.string().optional().default('/Script/UMG.UserWidget').describe('Parent class path'),
  },
  async ({ widget_name, path, parent_class }) => safe(async () => {
    const { head, ref } = pyparams({ widget_name, path, parent_class });
    const script = `
import unreal
${head}
parent_cls = unreal.load_class(None, ${ref}['parent_class'])
if parent_cls is None:
    print('ERROR: Parent class not found: ' + ${ref}['parent_class'])
else:
    factory = unreal.WidgetBlueprintFactory()
    factory.set_editor_property('parent_class', parent_cls)
    asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
    widget_bp = asset_tools.create_asset(${ref}['widget_name'], ${ref}['path'], unreal.WidgetBlueprint, factory)
    if widget_bp is None:
        print('ERROR: Failed to create Widget Blueprint — asset may already exist')
    else:
        unreal.KismetEditorUtilities.compile_blueprint(widget_bp)
        print({'success': True, 'name': ${ref}['widget_name'], 'path': str(widget_bp.get_path_name())})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_text_block_to_widget',
  'Add a TextBlock widget to a Widget Blueprint\'s Canvas Panel',
  {
    widget_path: z.string().describe('Asset path to the Widget Blueprint'),
    text_block_name: z.string().describe('Name for the new TextBlock'),
    text: z.string().optional().default('Text').describe('Initial text content'),
    position: z.object({ x: z.number(), y: z.number() }).optional().default({ x: 0, y: 0 }),
    size: z.object({ width: z.number(), height: z.number() }).optional().default({ width: 200, height: 50 }),
    font_size: z.number().int().optional().default(24),
    color: z.object({ r: z.number(), g: z.number(), b: z.number(), a: z.number() }).optional().default({ r: 1, g: 1, b: 1, a: 1 }),
  },
  async ({ widget_path, text_block_name, text, position, size, font_size, color }) => safe(async () => {
    const pos = position || { x: 0, y: 0 };
    const sz = size || { width: 200, height: 50 };
    const col = color || { r: 1, g: 1, b: 1, a: 1 };
    const { head, ref } = pyparams({ widget_path, text_block_name, text: text || 'Text' });
    const script = `
import unreal
${head}
widget_bp = unreal.load_asset(${ref}['widget_path'])
if widget_bp is None:
    print('ERROR: Widget Blueprint not found: ' + ${ref}['widget_path'])
else:
    widget_tree = widget_bp.widget_tree
    if widget_tree is None:
        print('ERROR: Widget Blueprint has no WidgetTree')
    else:
        root = widget_tree.root_widget
        if root is None or not isinstance(root, unreal.CanvasPanel):
            print('ERROR: Root widget is not a CanvasPanel')
        else:
            text_block = widget_tree.construct_widget(unreal.TextBlock, unreal.Name(${ref}['text_block_name']))
            text_block.set_text(unreal.Text(${ref}['text']))
            font_info = text_block.get_editor_property('font')
            font_info.set_editor_property('size', ${font_size ?? 24})
            text_block.set_editor_property('font', font_info)
            color_val = unreal.LinearColor(${col.r}, ${col.g}, ${col.b}, ${col.a})
            text_block.set_editor_property('color_and_opacity', unreal.SlateColor(specified_color=color_val))
            slot = root.add_child_to_canvas(text_block)
            slot.set_editor_property('position', unreal.Vector2D(${pos.x}, ${pos.y}))
            slot.set_editor_property('size', unreal.Vector2D(${sz.width}, ${sz.height}))
            slot.set_editor_property('auto_size', False)
            unreal.KismetEditorUtilities.compile_blueprint(widget_bp)
            print({'success': True, 'text_block': ${ref}['text_block_name']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_button_to_widget',
  'Add a Button widget (with a text label) to a Widget Blueprint\'s Canvas Panel',
  {
    widget_path: z.string().describe('Asset path to the Widget Blueprint'),
    button_name: z.string().describe('Name for the new Button'),
    text: z.string().optional().default('Button').describe('Button label text'),
    position: z.object({ x: z.number(), y: z.number() }).optional().default({ x: 0, y: 0 }),
    size: z.object({ width: z.number(), height: z.number() }).optional().default({ width: 150, height: 50 }),
    font_size: z.number().int().optional().default(18),
    text_color: z.object({ r: z.number(), g: z.number(), b: z.number(), a: z.number() }).optional().default({ r: 0, g: 0, b: 0, a: 1 }),
    background_color: z.object({ r: z.number(), g: z.number(), b: z.number(), a: z.number() }).optional().default({ r: 0.2, g: 0.2, b: 0.2, a: 1 }),
  },
  async ({ widget_path, button_name, text, position, size, font_size, text_color, background_color }) => safe(async () => {
    const pos = position || { x: 0, y: 0 };
    const sz = size || { width: 150, height: 50 };
    const tc = text_color || { r: 0, g: 0, b: 0, a: 1 };
    const bc = background_color || { r: 0.2, g: 0.2, b: 0.2, a: 1 };
    const { head, ref } = pyparams({ widget_path, button_name, text: text || 'Button' });
    const script = `
import unreal
${head}
widget_bp = unreal.load_asset(${ref}['widget_path'])
if widget_bp is None:
    print('ERROR: Widget Blueprint not found: ' + ${ref}['widget_path'])
else:
    widget_tree = widget_bp.widget_tree
    root = widget_tree.root_widget if widget_tree else None
    if root is None or not isinstance(root, unreal.CanvasPanel):
        print('ERROR: Root widget is not a CanvasPanel')
    else:
        btn = widget_tree.construct_widget(unreal.Button, unreal.Name(${ref}['button_name']))
        bg_color = unreal.LinearColor(${bc.r}, ${bc.g}, ${bc.b}, ${bc.a})
        btn.set_editor_property('background_color', bg_color)
        label = widget_tree.construct_widget(unreal.TextBlock, unreal.Name(${ref}['button_name'] + '_Label'))
        label.set_text(unreal.Text(${ref}['text']))
        font_info = label.get_editor_property('font')
        font_info.set_editor_property('size', ${font_size ?? 18})
        label.set_editor_property('font', font_info)
        tc = unreal.LinearColor(${tc.r}, ${tc.g}, ${tc.b}, ${tc.a})
        label.set_editor_property('color_and_opacity', unreal.SlateColor(specified_color=tc))
        btn.add_child(label)
        slot = root.add_child_to_canvas(btn)
        slot.set_editor_property('position', unreal.Vector2D(${pos.x}, ${pos.y}))
        slot.set_editor_property('size', unreal.Vector2D(${sz.width}, ${sz.height}))
        slot.set_editor_property('auto_size', False)
        unreal.KismetEditorUtilities.compile_blueprint(widget_bp)
        print({'success': True, 'button': ${ref}['button_name']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'bind_widget_event',
  'Bind a widget component event (e.g. OnClicked) to a Blueprint function',
  {
    widget_path: z.string().describe('Asset path to the Widget Blueprint'),
    widget_component_name: z.string().describe('Name of the widget component (e.g. the Button name)'),
    event_name: z.string().describe('Event name to bind, e.g. "OnClicked", "OnHovered"'),
    function_name: z.string().optional().describe('Blueprint function name to bind to (auto-generated if omitted)'),
  },
  async ({ widget_path, widget_component_name, event_name, function_name }) => safe(async () => {
    const { head, ref } = pyparams({
      widget_path,
      widget_component_name,
      event_name,
      function_name: function_name || `On${widget_component_name}_${event_name}`,
    });
    const script = `
import unreal
${head}
widget_bp = unreal.load_asset(${ref}['widget_path'])
if widget_bp is None:
    print('ERROR: Widget Blueprint not found: ' + ${ref}['widget_path'])
else:
    fn_name = ${ref}['function_name']
    # Create the handler function graph if it doesn't exist
    existing_graphs = [g.get_name() for g in widget_bp.function_graphs]
    if fn_name not in existing_graphs:
        unreal.BlueprintEditorLibrary.add_new_function_graph(widget_bp, fn_name)
    unreal.KismetEditorUtilities.compile_blueprint(widget_bp)
    print({'success': True, 'bound_event': ${ref}['event_name'], 'component': ${ref}['widget_component_name'], 'function': fn_name})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'add_widget_to_viewport',
  'Add a Widget Blueprint to the player\'s viewport at runtime (adds to BeginPlay via Blueprint graph)',
  {
    widget_path: z.string().describe('Asset path to the Widget Blueprint, e.g. "/Game/UI/WBP_HUD"'),
    z_order: z.number().int().optional().default(0).describe('Z-order for the widget layer (higher = on top)'),
  },
  async ({ widget_path, z_order }) => safe(async () => {
    const { head, ref } = pyparams({ widget_path });
    const script = `
import unreal
${head}
widget_class = unreal.EditorAssetLibrary.load_blueprint_class(${ref}['widget_path'])
if widget_class is None:
    print('ERROR: Widget Blueprint not found: ' + ${ref}['widget_path'])
else:
    print({'success': True, 'widget_class': str(widget_class.get_path_name()), 'z_order': ${z_order ?? 0},
           'note': 'Call CreateWidget and AddToViewport in your game code or BeginPlay graph using this class path'})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_text_block_binding',
  'Create a member variable and getter function for data-binding a TextBlock in a Widget Blueprint',
  {
    widget_path: z.string().describe('Asset path to the Widget Blueprint'),
    text_block_name: z.string().describe('Name of the TextBlock widget to bind'),
    binding_property: z.string().describe('Name of the new Blueprint variable to bind to'),
    default_value: z.string().optional().default('').describe('Default string value for the binding variable'),
  },
  async ({ widget_path, text_block_name, binding_property, default_value }) => safe(async () => {
    const { head, ref } = pyparams({ widget_path, text_block_name, binding_property, default_value: default_value || '' });
    const script = `
import unreal
${head}
widget_bp = unreal.load_asset(${ref}['widget_path'])
if widget_bp is None:
    print('ERROR: Widget Blueprint not found: ' + ${ref}['widget_path'])
else:
    # Add a String member variable for the binding
    new_var = unreal.BPVariableDescription()
    new_var.var_name = unreal.Name(${ref}['binding_property'])
    new_var.default_value = ${ref}['default_value']
    widget_bp.new_variables.append(new_var)
    # Create a getter function for the binding
    getter_name = 'Get' + ${ref}['binding_property']
    unreal.BlueprintEditorLibrary.add_new_function_graph(widget_bp, getter_name)
    unreal.KismetEditorUtilities.compile_blueprint(widget_bp)
    print({'success': True, 'variable': ${ref}['binding_property'], 'getter': getter_name, 'text_block': ${ref}['text_block_name']})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Project / Input Tools ────────────────────────────────────────────────────

server.tool(
  'create_input_mapping',
  'Create an Action or Axis input mapping in the project\'s Input Settings',
  {
    action_name: z.string().describe('Name of the input action or axis, e.g. "Jump", "MoveForward"'),
    key: z.string().describe('Key name, e.g. "SpaceBar", "W", "LeftMouseButton", "Gamepad_FaceButton_Bottom"'),
    input_type: z.enum(['Action', 'Axis']).optional().default('Action').describe('"Action" for button presses, "Axis" for analog/continuous input'),
    scale: z.number().optional().default(1.0).describe('Axis scale (only used for Axis mappings, use -1.0 for inverse)'),
    shift: z.boolean().optional().default(false),
    ctrl: z.boolean().optional().default(false),
    alt: z.boolean().optional().default(false),
    cmd: z.boolean().optional().default(false),
  },
  async ({ action_name, key, input_type, scale, shift, ctrl, alt, cmd }) => safe(async () => {
    const { head, ref } = pyparams({ action_name, key });
    const isAxis = input_type === 'Axis';
    const script = `
import unreal
${head}
settings = unreal.get_default_object(unreal.InputSettings)
key_obj = unreal.Key(${ref}['key'])
if ${isAxis ? 'True' : 'False'}:
    mapping = unreal.InputAxisKeyMapping()
    mapping.axis_name = unreal.Name(${ref}['action_name'])
    mapping.key = key_obj
    mapping.scale = ${scale ?? 1.0}
    settings.add_axis_mapping(mapping, True)
else:
    mapping = unreal.InputActionKeyMapping()
    mapping.action_name = unreal.Name(${ref}['action_name'])
    mapping.key = key_obj
    mapping.shift = ${shift ? 'True' : 'False'}
    mapping.ctrl = ${ctrl ? 'True' : 'False'}
    mapping.alt = ${alt ? 'True' : 'False'}
    mapping.cmd = ${cmd ? 'True' : 'False'}
    settings.add_action_mapping(mapping, True)
settings.save_key_mappings()
unreal.EditorAssetLibrary.save_directory('/Game', False, False)
print({'success': True, 'action': ${ref}['action_name'], 'key': ${ref}['key'], 'type': '${input_type}'})
`.trim();
    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

// ─── Additional Actor Tools ───────────────────────────────────────────────────

server.tool(
  'rename_actor',
  'Rename an actor in the current level',
  {
    label: z.string().describe('Current actor label (as shown in Outliner)'),
    new_label: z.string().describe('New actor label'),
  },
  async ({ label, new_label }) => safe(async () => {
    const { head, ref } = pyparams({ label, new_label });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found: ' + ${ref}['label'])
else:
    target.set_actor_label(${ref}['new_label'])
    print({'success': True, 'old_label': ${ref}['label'], 'new_label': ${ref}['new_label']})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Rename failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'duplicate_actor',
  'Duplicate an actor in the current level',
  {
    label: z.string().describe('Label of the actor to duplicate'),
    offset: z.object({
      x: z.number().default(100),
      y: z.number().default(0),
      z: z.number().default(0),
    }).optional().describe('Location offset for the duplicate (cm). Defaults to 100 cm on X axis.'),
  },
  async ({ label, offset }) => safe(async () => {
    const off = offset || { x: 100, y: 0, z: 0 };
    const { head, ref } = pyparams({ label });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found: ' + ${ref}['label'])
else:
    dup = unreal.EditorLevelLibrary.duplicate_actor(target, None)
    if dup is None:
        print('ERROR: Failed to duplicate actor')
    else:
        loc = target.get_actor_location()
        dup.set_actor_location(unreal.Vector(loc.x + ${off.x}, loc.y + ${off.y}, loc.z + ${off.z}), False, False)
        print({'success': True, 'original': ${ref}['label'], 'duplicate': dup.get_actor_label()})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Duplicate failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'get_actor_bounds',
  'Get the bounding box of an actor (origin and extent)',
  {
    label: z.string().describe('Actor label'),
    only_colliding: z.boolean().optional().default(false).describe('Use only colliding components for bounds'),
  },
  async ({ label, only_colliding }) => safe(async () => {
    const { head, ref } = pyparams({ label });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found: ' + ${ref}['label'])
else:
    origin, extent = target.get_actor_bounds(${only_colliding ? 'True' : 'False'})
    print({
        'label': target.get_actor_label(),
        'origin': {'x': origin.x, 'y': origin.y, 'z': origin.z},
        'extent': {'x': extent.x, 'y': extent.y, 'z': extent.z},
        'size': {'x': extent.x * 2, 'y': extent.y * 2, 'z': extent.z * 2},
    })
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'set_actor_tag',
  'Add or remove a tag on an actor',
  {
    label: z.string().describe('Actor label'),
    tag: z.string().describe('Tag name'),
    action: z.enum(['add', 'remove']).describe('"add" to add the tag, "remove" to remove it'),
  },
  async ({ label, tag, action }) => safe(async () => {
    const { head, ref } = pyparams({ label, tag });
    const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found: ' + ${ref}['label'])
else:
    tag_name = unreal.Name(${ref}['tag'])
    tags = list(target.tags)
    if '${action}' == 'add':
        if tag_name not in tags:
            tags.append(tag_name)
    else:
        tags = [t for t in tags if t != tag_name]
    target.tags = tags
    print({'success': True, 'label': ${ref}['label'], 'tags': [str(t) for t in target.tags]})
`.trim();

    const result = await client.executePython(script);
    if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
    return ok(result.body);
  })
);

server.tool(
  'focus_viewport',
  'Focus the editor viewport on a named actor or a specific location',
  {
    label: z.string().optional().describe('Actor label to focus on (mutually exclusive with location)'),
    location: z.object({
      x: z.number(), y: z.number(), z: z.number(),
    }).optional().describe('World location to focus on'),
  },
  async ({ label, location }) => safe(async () => {
    if (!label && !location) return err('Provide either label or location');
    if (label) {
      const { head, ref } = pyparams({ label });
      const script = `
import unreal
${head}
actors = unreal.EditorLevelLibrary.get_all_level_actors()
target = next((a for a in actors if a.get_actor_label() == ${ref}['label']), None)
if target is None:
    print('ERROR: Actor not found: ' + ${ref}['label'])
else:
    unreal.EditorLevelLibrary.set_selected_level_actors([target])
    unreal.UnrealEditorSubsystem().get_level_viewport_client().focus_viewport_on_box(target.get_component_bounds(False)[1], target.get_component_bounds(False)[2], False)
    print({'success': True, 'focused_on': ${ref}['label']})
`.trim();
      const result = await client.executePython(script);
      if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
      return ok(result.body);
    } else {
      const script = `
import unreal
loc = unreal.Vector(${location.x}, ${location.y}, ${location.z})
unreal.UnrealEditorSubsystem().get_level_viewport_client().set_view_location(loc)
print({'success': True, 'location': {'x': ${location.x}, 'y': ${location.y}, 'z': ${location.z}}})
`.trim();
      const result = await client.executePython(script);
      if (result.status >= 400) return err(`Failed: ${JSON.stringify(result.body)}`);
      return ok(result.body);
    }
  })
);

// ─── Exports (for HTTP server wrapper) ────────────────────────────────────────

module.exports = { server, client, main: () => main() };

// ─── Start (stdio mode — only when run directly) ─────────────────────────────

async function main() {
  const transport = new StdioServerTransport();

  // Start health monitoring
  client.startHealthCheck();

  client.on('connected', (info) => {
    log.info('Connected to Unreal Engine', { info });
  });
  client.on('disconnected', () => {
    log.warn('Lost connection to Unreal Engine — will keep retrying');
  });
  client.on('reconnected', (info) => {
    log.info('Reconnected to Unreal Engine', { info });
  });

  await server.connect(transport);
  log.info('BIM-Bot Unreal server started (stdio)', {
    remoteControlUrl: client.baseUrl,
    bridgeUrl: client.bridgeUrl,
  });
}

// ─── Graceful Shutdown ────────────────────────────────────────────────────────

function shutdown(signal) {
  log.info(`Shutting down (${signal})`);
  client.shutdown();
  process.exit(0);
}

process.on('SIGINT', () => shutdown('SIGINT'));
process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('uncaughtException', (e) => {
  log.error('Uncaught exception', { error: e.message, stack: e.stack });
  client.shutdown();
  process.exit(1);
});

// Only start stdio transport when this file is the direct entry point
if (require.main === module) {
  main().catch((e) => {
    log.error('Fatal startup error', { error: e.message, stack: e.stack });
    process.exit(1);
  });
}
