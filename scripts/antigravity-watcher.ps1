<#
.SYNOPSIS
    Antigravity watcher — transparent JSON-RPC proxy to BIM-Bot.
    Polls %APPDATA%\BIMBot\antigravity\request.json every 500ms.
    Forwards ANY method+params directly to Revit TCP on localhost:8080.
#>

$bridgeDir = Join-Path $env:APPDATA "BIMBot\antigravity"
$requestFile = Join-Path $bridgeDir "request.json"

# ── All tools keyword map (every tool in CommandExecutor.cs) ──
$toolAliases = @{
    # ===== READING =====
    "current view info"      = "get_current_view_info"
    "view info"              = "get_current_view_info"
    "active view"            = "get_current_view_info"
    "current view elements"  = "get_current_view_elements"
    "view elements"          = "get_current_view_elements"
    "selected elements"      = "get_selected_elements"
    "selection"              = "get_selected_elements"
    "what is selected"       = "get_selected_elements"
    "elements"               = "get_elements"
    "parameters"             = "get_parameters"
    "project info"           = "get_project_info"
    "project information"    = "get_project_info"
    "views"                  = "get_views"
    "sheets"                 = "get_sheets"
    "levels"                 = "get_levels"
    "grids"                  = "get_grids"
    "rooms"                  = "get_rooms"
    "families"               = "get_families"
    "family types"           = "get_available_family_types"
    "available families"     = "get_available_family_types"
    "family info"            = "get_family_info"
    "schedules"              = "get_schedules"
    "linked models"          = "get_linked_models"
    "links"                  = "get_linked_models"
    "warnings"               = "get_warnings"
    "export elements"        = "export_elements"

    # ===== OPEN / CLOSE VIEWS =====
    "open view"              = "open_view"
    "switch view"            = "open_view"
    "activate view"          = "open_view"
    "close view"             = "close_view"

    # ===== CREATING =====
    "create wall"            = "create_wall"
    "add wall"               = "create_wall"
    "make wall"              = "create_wall"
    "draw wall"              = "create_wall"
    "create floor"           = "create_floor"
    "add floor"              = "create_floor"
    "create ceiling"         = "create_ceiling"
    "add ceiling"            = "create_ceiling"
    "create roof"            = "create_roof"
    "add roof"               = "create_roof"
    "create level"           = "create_level"
    "add level"              = "create_level"
    "create grid"            = "create_grid"
    "add grid"               = "create_grid"
    "create room"            = "create_room"
    "add room"               = "create_room"
    "place room"             = "create_room"
    "create sheet"           = "create_sheet"
    "add sheet"              = "create_sheet"
    "create view"            = "create_view"
    "3d view"                = "create_view"
    "new view"               = "create_view"
    "create schedule"        = "create_schedule"
    "add schedule"           = "create_schedule"
    "create tag"             = "create_tag"
    "add tag"                = "create_tag"
    "create dimension"       = "create_dimension"
    "add dimension"          = "create_dimension"
    "create text"            = "create_text_note"
    "add text"               = "create_text_note"
    "text note"              = "create_text_note"
    "create line"            = "create_line_based_element"
    "add beam"               = "create_line_based_element"
    "create beam"            = "create_line_based_element"
    "create brace"           = "create_line_based_element"
    "create point"           = "create_point_based_element"
    "place door"             = "create_point_based_element"
    "add door"               = "create_point_based_element"
    "place window"           = "create_point_based_element"
    "add window"             = "create_point_based_element"
    "place furniture"        = "create_point_based_element"
    "add furniture"          = "create_point_based_element"

    # ===== EDITING =====
    "modify element"         = "modify_element"
    "modify parameter"       = "modify_element"
    "set parameter"          = "modify_element"
    "change parameter"       = "modify_element"
    "edit parameter"         = "modify_element"
    "update parameter"       = "modify_element"
    "add value"              = "modify_element"
    "set value"              = "modify_element"
    "move element"           = "move_element"
    "move"                   = "move_element"
    "rotate element"         = "rotate_element"
    "rotate"                 = "rotate_element"
    "copy element"           = "copy_element"
    "copy"                   = "copy_element"
    "delete"                 = "delete_elements"
    "remove element"         = "delete_elements"
    "mirror"                 = "mirror_element"
    "align"                  = "align_elements"
    "group"                  = "group_elements"
    "change type"            = "change_type"
    "swap type"              = "change_type"
    "set workset"            = "set_workset"
    "move to workset"        = "set_workset"
    "color elements"         = "color_elements"
    "color by parameter"     = "color_by_parameter"
    "batch modify"           = "batch_modify_parameters"
    "batch update"           = "batch_modify_parameters"

    # ===== DOCUMENTATION =====
    "place view on sheet"    = "place_view_on_sheet"
    "place views on sheet"   = "place_views_on_sheet"
    "create viewport"        = "create_viewport"
    "add viewport"           = "create_viewport"
    "export schedule"        = "export_schedule"
    "create legend"          = "create_legend"
    "add legend"             = "create_legend"
    "generate legend"        = "generate_legend"
    "add revision"           = "add_revision"
    "print sheets"           = "print_sheets"
    "print"                  = "print_sheets"
    "export dwg"             = "export_dwg"
    "export cad"             = "export_to_cad"
    "tag all"                = "tag_all_in_view"
    "auto tag"               = "tag_all_in_view"

    # ===== QA/QC =====
    "check warnings"         = "check_warnings"
    "isolate warnings"       = "isolate_warnings"
    "audit"                  = "audit_model"
    "audit model"            = "audit_model"
    "room compliance"        = "check_room_compliance"
    "naming conventions"     = "check_naming_conventions"
    "check naming"           = "check_naming_conventions"
    "find duplicates"        = "find_duplicates"
    "duplicates"             = "find_duplicates"
    "purge"                  = "purge_unused"
    "purge unused"           = "purge_unused"
    "deep purge"             = "deep_purge"
    "purge cad"              = "purge_cads"
    "check links"            = "check_links_status"
    "validate parameters"    = "validate_parameters"

    # ===== ADVANCED =====
    "send code"              = "send_code_to_revit"
    "execute code"           = "execute_code"
    "run code"               = "send_code_to_revit"
    "run csharp"             = "send_code_to_revit"
    "ai filter"              = "ai_element_filter"
    "reset view"             = "reset_view"
    "select"                 = "select_elements"
    "select elements"        = "select_elements"
    "model statistics"       = "get_model_statistics"
    "stats"                  = "get_model_statistics"
    "statistics"             = "get_model_statistics"

    # ===== PROJECT SETTINGS =====
    "object styles"          = "modify_object_styles"
    "modify object styles"   = "modify_object_styles"
    "set phase"              = "set_phase"
    "phases"                 = "get_phases"
    "get phases"             = "get_phases"
    "materials"              = "get_materials"
    "get materials"          = "get_materials"
    "set material"           = "set_material"
    "apply material"         = "set_material"
    "view properties"        = "set_view_properties"
    "set view properties"    = "set_view_properties"
    "override element"       = "override_element_in_view"
    "element override"       = "override_element_in_view"
    "visibility graphics"    = "set_visibility_graphics"
    "vg overrides"           = "set_visibility_graphics"
    "line styles"            = "get_line_styles"
    "set line style"         = "set_line_style"

    # ===== POWER TOOLS — Geometry =====
    "auto join"              = "auto_join_elements"
    "join elements"          = "auto_join_elements"
    "reassign level"         = "reassign_level"
    "change level"           = "reassign_level"
    "batch thickness"        = "batch_modify_thickness"
    "modify thickness"       = "batch_modify_thickness"
    "room to floor"          = "room_to_floor"
    "room finishes"          = "create_room_finishes"
    "extend element"         = "extend_shrink_element"
    "shrink element"         = "extend_shrink_element"

    # ===== POWER TOOLS — Data & Parameters =====
    "find replace names"     = "find_replace_names"
    "rename views"           = "bulk_rename_views"
    "bulk rename"            = "bulk_rename_views"
    "case convert"           = "parameter_case_convert"
    "parameter case"         = "parameter_case_convert"
    "transfer parameter"     = "bulk_parameter_transfer"
    "copy parameter"         = "copy_parameter_value"
    "renumber"               = "renumber_elements"
    "auto renumber"          = "auto_renumber"
    "create parameter"       = "create_project_parameter"
    "add parameter"          = "create_project_parameter"
    "project parameter"      = "create_project_parameter"
    "shared parameter"       = "add_shared_parameter"
    "add shared parameter"   = "add_shared_parameter"
    "batch set parameter"    = "batch_set_parameter"
    "batch set"              = "batch_set_parameter"
    "import csv"             = "import_data_from_csv"
    "import data"            = "import_data_from_csv"

    # ===== POWER TOOLS — Views & Documentation =====
    "batch create sheets"    = "batch_create_sheets"
    "batch sheets"           = "batch_create_sheets"
    "align viewports"        = "align_viewports"
    "duplicate sheets"       = "duplicate_sheets"
    "duplicate view"         = "duplicate_view"
    "auto section box"       = "auto_section_box"
    "section box"            = "auto_section_box"
    "copy view filters"      = "copy_view_filters"
    "copy filters"           = "copy_view_filters"
    "create elevation"       = "create_elevation_views"
    "elevation views"        = "create_elevation_views"
    "create section"         = "create_section_views"
    "section views"          = "create_section_views"
    "create callout"         = "create_callout_views"
    "callout views"          = "create_callout_views"
    "view filter"            = "create_view_filter"
    "create view filter"     = "create_view_filter"
    "apply view template"    = "apply_view_template"
    "view template"          = "apply_view_template"
    "crop region sync"       = "crop_region_sync"

    # ===== POWER TOOLS — Project Cleanup =====
    "delete empty groups"    = "delete_empty_groups"
    "empty groups"           = "delete_empty_groups"
    "find cad"               = "find_cad_imports"
    "cad imports"            = "find_cad_imports"
    "cad to lines"           = "cad_to_lines"
    "delete unused families" = "delete_unused_families"
    "unused families"        = "delete_unused_families"
    "resolve warnings"       = "resolve_warnings"
    "fix warnings"           = "resolve_warnings"

    # ===== POWER TOOLS — Selection & Filtering =====
    "select by parameter"    = "select_by_parameter"
    "select by filter"       = "select_by_filter"
    "select by workset"      = "select_by_workset"
    "filter selection"       = "filter_selection"
    "category to workset"    = "category_to_workset"
    "inverse selection"      = "inverse_selection"
    "invert selection"       = "inverse_selection"
    "copy from linked"       = "copy_from_linked"
    "wall floor sync"        = "wall_floor_sync"
    "snap beams"             = "snap_beams_to_columns"
    "convert category"       = "convert_category"

    # ===== EXPORT TOOLS =====
    "export manager"         = "export_manager"
    "batch export"           = "export_manager"
    "export pdf"             = "export_to_pdf"
    "export to pdf"          = "export_to_pdf"
    "export images"          = "export_to_images"
    "export to images"       = "export_to_images"
    "export ifc"             = "export_to_ifc"
    "export to ifc"          = "export_to_ifc"
    "export dgn"             = "export_to_dgn"
    "export to dgn"          = "export_to_dgn"
    "export to dwg"          = "export_to_dwg"
    "export dwf"             = "export_to_dwf"
    "export to dwf"          = "export_to_dwf"
    "export nwc"             = "export_to_nwc"
    "export navisworks"      = "export_to_nwc"
    "export to nwc"          = "export_to_nwc"
    "export schedule data"   = "export_schedule_data"
    "export params csv"      = "export_parameters_to_csv"
    "export parameters"      = "export_parameters_to_csv"
    "import params csv"      = "import_parameters_from_csv"
    "import parameters"      = "import_parameters_from_csv"

    # ===== FAMILY MANAGEMENT =====
    "manage families"        = "manage_families"
    "family management"      = "manage_families"

    # ===== PROJECT DATA =====
    "save project data"      = "save_project_data"
    "load project data"      = "load_project_data"
    "list project data"      = "list_project_data"
    "delete project data"    = "delete_project_data"
    "save snapshot"          = "save_snapshot"
    "model snapshot"         = "save_snapshot"

    # ===== ADDITIONAL QUERIES =====
    "worksets"               = "get_worksets"
    "get worksets"           = "get_worksets"
    "areas"                  = "get_areas"
    "get areas"              = "get_areas"
    "design options"         = "get_design_options"
    "get design options"     = "get_design_options"

    # ===== INTEGRATIONS =====
    "integration status"     = "get_integration_status"
    "export to excel"        = "export_to_excel_integration"
    "export to notion"       = "export_to_notion_integration"
    "export to google"       = "export_to_google_sheets_integration"
}

function Send-RevitCommand {
    param([string]$Method, [string]$Params = "{}")

    $request = @{
        jsonrpc = "2.0"
        method  = $Method
        params  = ($Params | ConvertFrom-Json)
        id      = 1
    } | ConvertTo-Json -Depth 10 -Compress

    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $client.SendTimeout = 30000
        $client.ReceiveTimeout = 30000

        try {
            $connectTask = $client.ConnectAsync("127.0.0.1", 8080)
            $connected = $connectTask.Wait(5000)
            if (-not $connected) {
                $client.Dispose()
                return "Connection timed out. Is BIM-Bot service started? (Start it from Revit > MCP tab > Start Service)"
            }
        }
        catch {
            $client.Dispose()
            $inner = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message }
            return "Cannot connect to BIM-Bot on port 8080. Start the MCP service in Revit first. ($inner)"
        }

        $stream = $client.GetStream()
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($request)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()

        Start-Sleep -Milliseconds 200

        $buffer = New-Object byte[] 65536
        $response = [System.Text.StringBuilder]::new()
        $stream.ReadTimeout = 30000

        do {
            $count = $stream.Read($buffer, 0, $buffer.Length)
            if ($count -gt 0) {
                [void]$response.Append([System.Text.Encoding]::UTF8.GetString($buffer, 0, $count))
            }
            Start-Sleep -Milliseconds 100
        } while ($stream.DataAvailable)

        $stream.Dispose()
        $client.Dispose()
        return $response.ToString()
    }
    catch {
        return "Error calling Revit: $_"
    }
}

function Resolve-ToolFromMessage {
    param([string]$Message)

    $lower = $Message.ToLower().Trim()

    # 1. Direct tool name match (e.g. "get_levels", "create_wall")
    if ($lower -match "^[a-z_]+$") {
        return @{ Method = $lower; Params = "{}" }
    }

    # 2. "open 3d view" / "show 3d view" special case
    if ($lower -match "(open|show|create)\s+3d\s*view") {
        return @{ Method = "create_view"; Params = '{"viewType":"3D"}' }
    }

    # 3. Keyword alias matching — longest match first
    $sortedAliases = $toolAliases.GetEnumerator() | Sort-Object { $_.Key.Length } -Descending
    foreach ($alias in $sortedAliases) {
        if ($lower -match [regex]::Escape($alias.Key)) {
            # Try to extract simple params from common patterns
            $method = $alias.Value
            $params = "{}"

            # "get elements Walls" → category param
            if ($method -eq "get_elements" -and $lower -match "elements?\s+(\w+)") {
                $cat = (Get-Culture).TextInfo.ToTitleCase($Matches[1])
                $params = "{`"category`":`"$cat`",`"limit`":20}"
            }
            # "get views FloorPlan" → viewType param
            elseif ($method -eq "get_views" -and $lower -match "views?\s+(\w+)") {
                $params = "{`"viewType`":`"$($Matches[1])`"}"
            }

            return @{ Method = $method; Params = $params }
        }
    }

    # 4. No match — return $null (will be handled by caller)
    return $null
}

Write-Host ""
Write-Host "  ╔═══════════════════════════════════════════════╗" -ForegroundColor DarkMagenta
Write-Host "  ║   Antigravity Watcher — Full Authority Mode   ║" -ForegroundColor DarkMagenta
Write-Host "  ╠═══════════════════════════════════════════════╣" -ForegroundColor DarkMagenta
Write-Host "  ║  All 70 BIM-Bot tools available             ║" -ForegroundColor DarkMagenta
Write-Host "  ║  Transparent JSON-RPC proxy to localhost:8080  ║" -ForegroundColor DarkMagenta
Write-Host "  ╚═══════════════════════════════════════════════╝" -ForegroundColor DarkMagenta
Write-Host ""
Write-Host "  Watching: $requestFile" -ForegroundColor DarkGray
Write-Host "  Press Ctrl+C to stop" -ForegroundColor DarkGray
Write-Host ""

while ($true) {
    if (Test-Path $requestFile) {
        try {
            $raw = Get-Content $requestFile -Raw
            $req = $raw | ConvertFrom-Json
            $id = $req.id
            $responseFile = Join-Path $bridgeDir "response_$id.json"

            $responseText = ""

            # ── Mode 1: Structured command (method + params provided) ──
            if ($req.method) {
                $method = $req.method
                $params = if ($req.params) { $req.params | ConvertTo-Json -Depth 10 -Compress } else { "{}" }
                Write-Host "  [$id] TOOL: $method" -ForegroundColor Yellow
                $responseText = Send-RevitCommand -Method $method -Params $params
            }
            # ── Mode 2: Natural language message — resolve to tool ──
            elseif ($req.message) {
                $msg = $req.message
                Write-Host "  [$id] MSG: $msg" -ForegroundColor Cyan

                $resolved = Resolve-ToolFromMessage -Message $msg
                if ($resolved) {
                    Write-Host "       → $($resolved.Method)" -ForegroundColor DarkYellow
                    $responseText = Send-RevitCommand -Method $resolved.Method -Params $resolved.Params
                }
                else {
                    # No tool matched — forward to ai_chat so Revit's AI handles it
                    Write-Host "       → ai_chat (no direct match)" -ForegroundColor DarkGray
                    $aiParams = @{ message = $msg } | ConvertTo-Json -Depth 5 -Compress
                    $responseText = Send-RevitCommand -Method "ai_chat" -Params $aiParams
                }
            }
            else {
                $responseText = "Invalid request: no 'method' or 'message' field found."
            }

            # ── Parse JSON-RPC response → extract human-readable text ──
            $displayText = $responseText
            try {
                $jsonResp = $responseText | ConvertFrom-Json
                if ($jsonResp.result) {
                    if ($jsonResp.result.message) {
                        $displayText = $jsonResp.result.message
                    }
                    elseif ($jsonResp.result -is [string]) {
                        $displayText = $jsonResp.result
                    }
                    else {
                        # Format result object nicely
                        $displayText = $jsonResp.result | ConvertTo-Json -Depth 5
                    }
                }
                elseif ($jsonResp.error) {
                    $displayText = "❌ Error: $($jsonResp.error.message)"
                }
            }
            catch {
                # Not JSON — use raw text as-is
            }

            # ── Write response ──
            $response = @{ text = $displayText } | ConvertTo-Json -Depth 10
            [System.IO.File]::WriteAllText($responseFile, $response)

            # Clean up request
            Remove-Item $requestFile -Force -ErrorAction SilentlyContinue

            Write-Host "  [$id] Response sent ✓" -ForegroundColor Green
            Write-Host ""
        }
        catch {
            Write-Host "  Error: $_" -ForegroundColor Red
        }
    }
    Start-Sleep -Milliseconds 500
}

