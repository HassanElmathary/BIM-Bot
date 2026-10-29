/**
 * What the AI (Claude Code / OpenCode) may do with BIM-Bot Unreal tools.
 *
 *   allow — runs without asking (read-only, or an editor-undoable scene/Blueprint edit)
 *   ask   — needs your approval each time (writes to disk, loses unsaved work, long-running, or reaches arbitrary engine internals)
 *   deny  — blocked entirely (runs arbitrary code with full access to your PC)
 *
 * Every tool registered in index.js must appear in exactly one list (checked by the tests).
 */

const POLICY = {
  allow: [
    // Read-only: inspect the project, level, assets and editor
    'ping_unreal',
    'get_project_info',
    'get_world_info',
    'get_actors',
    'find_actors_by_name',
    'get_actor_properties',
    'get_actor_bounds',
    'get_selected_actors',
    'search_assets',
    'get_asset_info',
    'get_blueprints',
    'find_blueprint_nodes',
    'get_remote_property',
    'capture_viewport',
    // Editor UI only, no content changes
    'select_actors_by_label',
    'focus_viewport',
    'show_notification',
    // Level edits (in memory, undoable with Ctrl+Z / undo until you save)
    'spawn_actor',
    'spawn_blueprint_actor',
    'duplicate_actor',
    'rename_actor',
    'set_actor_transform',
    'set_actor_visibility',
    'set_actor_property',
    'set_actor_tag',
    'set_static_mesh_material',
    'undo',
    // Blueprint / UMG authoring (in memory until saved)
    'create_blueprint',
    'compile_blueprint',
    'add_component_to_blueprint',
    'set_static_mesh_properties',
    'set_component_property',
    'set_physics_properties',
    'set_blueprint_property',
    'set_pawn_properties',
    'add_blueprint_variable',
    'add_blueprint_event_node',
    'add_blueprint_input_action_node',
    'add_blueprint_function_node',
    'connect_blueprint_nodes',
    'add_blueprint_get_self_component_reference',
    'add_blueprint_self_reference',
    'create_umg_widget_blueprint',
    'add_text_block_to_widget',
    'add_button_to_widget',
    'bind_widget_event',
    'add_widget_to_viewport',
    'set_text_block_binding',
    // Play-in-editor sessions
    'play_in_editor',
    'stop_play_in_editor',
  ],
  ask: [
    'delete_actor',          // removes actors from the level
    'save_level',            // writes .umap to disk, past the undo history
    'open_level',            // switches level, unsaved changes can be lost
    'import_asset',          // reads a file from disk into Content/
    'create_material',       // writes a material asset to Content/ (can overwrite one)
    'build_lighting',        // long-running, overwrites built lighting data
    'create_input_mapping',  // changes project-wide input settings (DefaultInput.ini)
    'call_remote_function',  // calls any UFunction on any object
    'set_remote_property',   // sets any property on any object path
  ],
  deny: [
    'execute_python',          // arbitrary Python: file system, network, processes
    'execute_console_command', // arbitrary console commands (quit, exec scripts, cvars)
  ],
};

/** Claude Code permission rules: mcp__<server>__<tool> */
function claudeRules(server) {
  const rule = (t) => `mcp__${server}__${t}`;
  return {
    allow: POLICY.allow.map(rule),
    ask: POLICY.ask.map(rule),
    deny: POLICY.deny.map(rule),
  };
}

/** OpenCode permission map: <server>_<tool> → "allow" | "ask" | "deny" */
function opencodeRules(server) {
  const map = {};
  for (const level of ['allow', 'ask', 'deny']) {
    for (const t of POLICY[level]) map[`${server}_${t}`] = level;
  }
  return map;
}

module.exports = { POLICY, claudeRules, opencodeRules };
