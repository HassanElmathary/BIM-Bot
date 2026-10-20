# BIM-Bot rules

Copy this file into the root of your OpenCode project folder (e.g. `D:\BIM-Agent\`).
OpenCode reads `AGENTS.md` from the project root on every session. Edit the
**Project conventions** section for your own office standards.

## Safety
- Call `create_checkpoint` before any write; on failure call `rollback_to_checkpoint`.
- Never `sync_to_central` or `relinquish_all` unless the user asks in this message.
- Never `delete_elements`, `deep_purge` or `purge_unused` without listing what will change and getting a yes.
- Work on detached test copies, never a live central file, unless the user says otherwise.

## C# code execution (`send_code_to_revit`)
- Check the Revit year first (`get_project_info`). Revit 2024+ uses `new ElementId(long)` / `.Value`;
  2020–2023 use `new ElementId(int)` / `.IntegerValue`. Revit 2026+ has no `int` constructor.
- Do not open transactions; BIM-Bot wraps execution in its own. No nested transactions.
- Material parameters on nested family instances: iterate via `FamilyManager`.
- `FillPatternTarget` must match the usage context (Drafting vs Model).

## Parameters
- Read before write: `get_parameters` first, then `batch_modify_parameters`.

## Output
- Report element IDs and counts for every change.
- Prefer batch tools over per-element loops.

## Project conventions
<!-- Replace with your office's standards, for example: -->
- Shared parameters follow the QIC schema: `QIC_ABS`, `QIC_4D`, `QIC_5D` series.
