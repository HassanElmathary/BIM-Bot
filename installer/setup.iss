; ============================================================
;  BIM-Bot — Professional Installer (Inno Setup 6)
;  AI-Powered BIM Automation • 179 MCP Tools • Revit 2020–2027
;  by Hassan Ahmed Elmathary
; ============================================================

#define MyAppName      "BIM-Bot"
#define MyAppVersion   "2.1.0"
#define MyAppPublisher "Hassan Ahmed Elmathary"
#define MyAppURL       "https://github.com/HassanElmathary/BIM-Bot"
#define MyAppExeName   "Start MCP Server.bat"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\BIMBot
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; License
LicenseFile=..\LICENSE
; Output
OutputDir=output
OutputBaseFilename=BIMBot-Setup-{#MyAppVersion}
; Branding
SetupIconFile=assets\bimbot.ico
WizardImageFile=assets\WizardImageFile.bmp
WizardSmallImageFile=assets\WizardSmallImageFile.bmp
UninstallDisplayIcon={app}\bimbot.ico
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Appearance
WizardStyle=modern
WizardSizePercent=100
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
; Requirements — admin by default, but allow per-user install (no admin
; rights needed; addins then go to the user's %APPDATA% Revit folder)
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesInstallIn64BitMode=x64
; Info shown in Add/Remove Programs
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=AI-Powered BIM Automation for Autodesk Revit
VersionInfoCopyright=Copyright (c) 2026 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
; Uninstall
UninstallDisplayName={#MyAppName} v{#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to {#MyAppName}
WelcomeLabel2=This will install {#MyAppName} v{#MyAppVersion} on your computer.%n%n{#MyAppName} provides 179 AI-powered MCP tools for Autodesk Revit, enabling intelligent BIM automation through Claude Desktop, Cursor, Windsurf, and any MCP client.%n%nSupports Revit 2020–2027.
FinishedHeadingLabel=Installation Complete!
FinishedLabel={#MyAppName} has been successfully installed.%n%nNext Steps:%n  1. Open Revit → look for the "BIM-Bot" tab in the ribbon%n  2. Open Claude Desktop → BIM-Bot tools are ready to use

[Types]
Name: "full"; Description: "Full installation (recommended)"
Name: "server"; Description: "MCP Server only (no Revit plugin)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "server"; Description: "MCP Server (Node.js) — 179 AI tools for BIM automation"; Types: full server custom; Flags: fixed
Name: "nodejs"; Description: "Portable Node.js Runtime (v20 LTS)"; Types: full server custom; Flags: fixed
Name: "plugin"; Description: "Revit Plugin — connects Revit to the MCP Server"; Types: full custom
Name: "claude"; Description: "Auto-configure Claude Desktop"; Types: full custom

[Tasks]
; Auto-detect installed Revit versions — only show those found.
Name: "revit2020"; Description: "Revit 2020 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2020')
Name: "revit2021"; Description: "Revit 2021 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2021')
Name: "revit2022"; Description: "Revit 2022 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2022')
Name: "revit2023"; Description: "Revit 2023 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2023')
Name: "revit2024"; Description: "Revit 2024 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2024')
Name: "revit2025"; Description: "Revit 2025 (.NET 8.0)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2025')
Name: "revit2026"; Description: "Revit 2026 (.NET 8.0)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2026')
Name: "revit2027"; Description: "Revit 2027 (.NET 8.0)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2027')
; Additional options
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional options:"

[Files]
; Icon for uninstall entry
Source: "assets\bimbot.ico"; DestDir: "{app}"; Flags: ignoreversion

; Node.js portable runtime
Source: "nodejs\*"; DestDir: "{app}\nodejs"; Flags: ignoreversion recursesubdirs; Components: nodejs

; MCP Server
Source: "..\revit-mcp-server\build\*"; DestDir: "{app}\server\build"; Flags: ignoreversion recursesubdirs; Components: server
Source: "..\revit-mcp-server\node_modules\*"; DestDir: "{app}\server\node_modules"; Flags: ignoreversion recursesubdirs; Components: server
Source: "..\revit-mcp-server\package.json"; DestDir: "{app}\server"; Flags: ignoreversion; Components: server
Source: "..\revit-mcp-server\scripts\configure-claude.cjs"; DestDir: "{app}\server\scripts"; Flags: ignoreversion; Components: server

; Revit Plugin DLLs (both framework targets)
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\Release\net48\*"; DestDir: "{app}\plugin\net48"; Flags: ignoreversion recursesubdirs; Components: plugin
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\Release\net8.0-windows\*"; DestDir: "{app}\plugin\net8"; Flags: ignoreversion recursesubdirs; Components: plugin

; License
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu shortcuts
Name: "{group}\Start MCP Server"; Filename: "{app}\Start MCP Server.bat"; IconFilename: "{app}\bimbot.ico"; Comment: "Start the BIM-Bot Server"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; IconFilename: "{app}\bimbot.ico"
Name: "{group}\Documentation"; Filename: "{#MyAppURL}"; IconFilename: "{app}\bimbot.ico"
; Desktop shortcut (optional)
Name: "{autodesktop}\BIM-Bot Server"; Filename: "{app}\Start MCP Server.bat"; IconFilename: "{app}\bimbot.ico"; Comment: "Start the BIM-Bot Server"; Tasks: desktopicon



[Run]
Filename: "{app}\Start MCP Server.bat"; Description: "Start MCP Server now"; Flags: nowait postinstall skipifsilent unchecked shellexec
Filename: "{#MyAppURL}"; Description: "Visit documentation"; Flags: nowait postinstall skipifsilent unchecked shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{app}\server"
Type: filesandordirs; Name: "{app}\nodejs"
Type: filesandordirs; Name: "{app}\plugin"

[Code]
// ── Revit Detection ─────────────────────────────────────────

function IsRevitInstalled(Year: string): Boolean;
var
  RevitPath: string;
begin
  RevitPath := ExpandConstant('{pf}\Autodesk\Revit ' + Year);
  Result := DirExists(RevitPath);
end;

function GetRevitAddInsDir(Year: string): string;
begin
  // Machine-wide addins need admin; per-user installs use %APPDATA%
  if IsAdminInstallMode then
    Result := ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\' + Year)
  else
    Result := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\' + Year);
end;

function GetPluginSubfolder(YearInt: Integer): string;
begin
  if YearInt <= 2024 then
    Result := 'net48'
  else
    Result := 'net8';
end;

// ── Addin File Management ───────────────────────────────────

procedure InstallAddinForRevit(Year: string);
var
  AddInDir: string;
  AddinContent: string;
  PluginFolder: string;
  YearInt: Integer;
begin
  AddInDir := GetRevitAddInsDir(Year);
  ForceDirectories(AddInDir);

  // Legacy cleanup: remove old RevitMCP files
  if FileExists(AddInDir + '\RevitMCP.addin') then
    DeleteFile(AddInDir + '\RevitMCP.addin');
  if DirExists(AddInDir + '\RevitMCP') then
    DelTree(AddInDir + '\RevitMCP', True, True, True);

  YearInt := StrToInt(Year);
  PluginFolder := GetPluginSubfolder(YearInt);

  AddinContent := '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
    '<RevitAddIns>' + #13#10 +
    '  <AddIn Type="Application">' + #13#10 +
    '    <Name>BIM-Bot Plugin</Name>' + #13#10 +
    '    <Assembly>' + ExpandConstant('{app}') + '\plugin\' + PluginFolder + '\BIMBotPlugin.dll</Assembly>' + #13#10 +
    '    <FullClassName>BIMBotPlugin.Core.Application</FullClassName>' + #13#10 +
    '    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>' + #13#10 +
    '    <VendorId>HassanElmathary</VendorId>' + #13#10 +
    '    <VendorDescription>AI-Powered BIM-Bot Plugin by Hassan Ahmed Elmathary</VendorDescription>' + #13#10 +
    '  </AddIn>' + #13#10 +
    '</RevitAddIns>';

  SaveStringToFile(AddInDir + '\BIMBot.addin', AddinContent, False);
  Log('Installed .addin for Revit ' + Year + ' (' + PluginFolder + ')');
end;

procedure RemoveAddinForRevit(Year: string);
var
  AddinPath: string;
  PluginDir: string;
begin
  AddinPath := GetRevitAddInsDir(Year) + '\BIMBot.addin';
  if FileExists(AddinPath) then
    DeleteFile(AddinPath);
  // Also remove plugin directory from addins
  PluginDir := GetRevitAddInsDir(Year) + '\BIMBot';
  if DirExists(PluginDir) then
    DelTree(PluginDir, True, True, True);
  // Legacy cleanup: remove old RevitMCP files too
  AddinPath := GetRevitAddInsDir(Year) + '\RevitMCP.addin';
  if FileExists(AddinPath) then
    DeleteFile(AddinPath);
  PluginDir := GetRevitAddInsDir(Year) + '\RevitMCP';
  if DirExists(PluginDir) then
    DelTree(PluginDir, True, True, True);
end;

// ── Claude Configuration ────────────────────────────────────
//
// Primary path: run configure-claude.cjs with the bundled Node runtime.
// It does a real JSON parse/merge, validates that configured paths still
// exist, and REPAIRS stale entries (the old Pascal string-injection
// skipped whenever a "BIM-Bot" key was present, so a broken entry from a
// previous install location was never fixed).
// Fallback: legacy Pascal string injection if Node execution fails.

procedure ConfigureClaudeDesktopFallback();
var
  ClaudeDir: string;
  ClaudeConfig: string;
  ConfigContent: string;
  NodeExe: string;
  ServerJs: string;
  ExistingStr: string;
  ExistingAnsi: AnsiString;
begin
  ClaudeDir := ExpandConstant('{userappdata}\Claude');
  ClaudeConfig := ClaudeDir + '\claude_desktop_config.json';
  NodeExe := ExpandConstant('{app}\nodejs\node.exe');
  ServerJs := ExpandConstant('{app}\server\build\index.js');

  // Escape backslashes for JSON
  StringChangeEx(NodeExe, '\', '\\', True);
  StringChangeEx(ServerJs, '\', '\\', True);

  if FileExists(ClaudeConfig) then
  begin
    if LoadStringFromFile(ClaudeConfig, ExistingAnsi) then
    begin
      ExistingStr := String(ExistingAnsi);

      // Skip if already configured
      if Pos('"BIM-Bot"', ExistingStr) > 0 then
      begin
        Log('Claude Desktop config already contains BIM-Bot entry — skipping');
        Exit;
      end;

      // Inject into existing mcpServers block
      if Pos('"mcpServers"', ExistingStr) > 0 then
      begin
        StringChangeEx(ExistingStr, '"mcpServers": {',
          '"mcpServers": {' + #13#10 +
          '    "BIM-Bot": {' + #13#10 +
          '      "command": "' + NodeExe + '",' + #13#10 +
          '      "args": ["' + ServerJs + '"],' + #13#10 +
          '      "env": {}' + #13#10 +
          '    },', True);
        SaveStringToFile(ClaudeConfig, AnsiString(ExistingStr), False);
        Log('Merged BIM-Bot into existing Claude Desktop config');
        Exit;
      end;
    end;
  end;

  // Create new config
  ForceDirectories(ClaudeDir);
  ConfigContent := '{' + #13#10 +
    '  "mcpServers": {' + #13#10 +
    '    "BIM-Bot": {' + #13#10 +
    '      "command": "' + NodeExe + '",' + #13#10 +
    '      "args": ["' + ServerJs + '"],' + #13#10 +
    '      "env": {}' + #13#10 +
    '    }' + #13#10 +
    '  }' + #13#10 +
    '}';
  SaveStringToFile(ClaudeConfig, AnsiString(ConfigContent), False);
  Log('Created Claude Desktop config with BIM-Bot');
end;

procedure ConfigureClaudeDesktop();
var
  NodeExe: string;
  Script: string;
  ResultCode: Integer;
begin
  NodeExe := ExpandConstant('{app}\nodejs\node.exe');
  Script := ExpandConstant('{app}\server\scripts\configure-claude.cjs');

  if FileExists(NodeExe) and FileExists(Script) then
  begin
    if Exec(NodeExe, '"' + Script + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if ResultCode = 0 then
      begin
        Log('Claude configured via configure-claude.cjs');
        Exit;
      end;
      Log('configure-claude.cjs exited with code ' + IntToStr(ResultCode) + ' — using fallback');
    end
    else
      Log('Failed to execute node for configure-claude.cjs — using fallback');
  end
  else
    Log('Bundled node or configure script missing — using fallback');

  ConfigureClaudeDesktopFallback();
end;

procedure RemoveClaudeDesktopConfig();
var
  ClaudeConfig: string;
begin
  ClaudeConfig := ExpandConstant('{userappdata}\Claude\claude_desktop_config.json');
  if FileExists(ClaudeConfig) then
    Log('Claude Desktop config found — BIM-Bot entry left for manual cleanup');
end;

// ── Start MCP Server Batch File ─────────────────────────────

procedure CreateLauncherScript();
var
  BatContent: string;
  NodeExe: string;
  ServerJs: string;
begin
  NodeExe := ExpandConstant('{app}\nodejs\node.exe');
  ServerJs := ExpandConstant('{app}\server\build\index.js');

  BatContent := '@echo off' + #13#10 +
    'title BIM-Bot Server v{#MyAppVersion}' + #13#10 +
    'echo.' + #13#10 +
    'echo   ======================================' + #13#10 +
    'echo     BIM-Bot Server v{#MyAppVersion}' + #13#10 +
    'echo     by Hassan Ahmed Elmathary' + #13#10 +
    'echo   ======================================' + #13#10 +
    'echo.' + #13#10 +
    'echo   Starting MCP Server...' + #13#10 +
    'echo   Press Ctrl+C to stop.' + #13#10 +
    'echo.' + #13#10 +
    '"' + NodeExe + '" "' + ServerJs + '"' + #13#10 +
    'pause';

  SaveStringToFile(ExpandConstant('{app}\Start MCP Server.bat'), BatContent, False);
end;

// ── MCP Config Reference File ───────────────────────────────

procedure CreateMcpConfigReference();
var
  ConfigContent: string;
  NodeExe: string;
  ServerJs: string;
begin
  NodeExe := ExpandConstant('{app}\nodejs\node.exe');
  ServerJs := ExpandConstant('{app}\server\build\index.js');
  StringChangeEx(NodeExe, '\', '\\', True);
  StringChangeEx(ServerJs, '\', '\\', True);

  ConfigContent := '{' + #13#10 +
    '  "mcpServers": {' + #13#10 +
    '    "BIM-Bot": {' + #13#10 +
    '      "command": "' + NodeExe + '",' + #13#10 +
    '      "args": ["' + ServerJs + '"],' + #13#10 +
    '      "env": {}' + #13#10 +
    '    }' + #13#10 +
    '  }' + #13#10 +
    '}';
  SaveStringToFile(ExpandConstant('{app}\mcp-config.json'), ConfigContent, False);
end;

// ── Existing Installation Detection ─────────────────────────

const
  UninstallRegKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}_is1';

function GetUninstallString(): string;
var
  UninstallStr: string;
begin
  Result := '';
  if not RegQueryStringValue(HKLM, UninstallRegKey, 'UninstallString', UninstallStr) then
    RegQueryStringValue(HKCU, UninstallRegKey, 'UninstallString', UninstallStr);
  Result := RemoveQuotes(UninstallStr);
end;

function GetInstalledVersion(): string;
var
  Version: string;
begin
  Result := 'unknown';
  if not RegQueryStringValue(HKLM, UninstallRegKey, 'DisplayVersion', Version) then
    RegQueryStringValue(HKCU, UninstallRegKey, 'DisplayVersion', Version);
  if Version <> '' then
    Result := Version;
end;

function InitializeSetup(): Boolean;
var
  UninstallStr: string;
  InstalledVersion: string;
  ResultCode: Integer;
  Choice: Integer;
begin
  Result := True;

  UninstallStr := GetUninstallString();
  if UninstallStr = '' then
    Exit; // Not installed — proceed with fresh install

  InstalledVersion := GetInstalledVersion();

  Choice := MsgBox(
    'BIM-Bot v' + InstalledVersion + ' is already installed.' + #13#10 + #13#10 +
    'What would you like to do?' + #13#10 + #13#10 +
    '    YES  =  Repair (reinstall v{#MyAppVersion})' + #13#10 +
    '    NO   =  Uninstall' + #13#10 +
    '    CANCEL =  Exit',
    mbConfirmation, MB_YESNOCANCEL);

  case Choice of
    IDYES:
    begin
      // Repair — silently remove old version, then continue with fresh install
      Exec(UninstallStr, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Result := True;
    end;
    IDNO:
    begin
      // Uninstall — run uninstaller with UI and exit setup
      Exec(UninstallStr, '', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
      Result := False;
    end;
    IDCANCEL:
    begin
      Result := False;
    end;
  end;
end;

// ── Post-Install Hook ───────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
var
  Years: array[0..7] of string;
  Tasks: array[0..7] of string;
  i: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Install .addin files for selected Revit versions (2020–2027)
    Years[0] := '2020'; Tasks[0] := 'revit2020';
    Years[1] := '2021'; Tasks[1] := 'revit2021';
    Years[2] := '2022'; Tasks[2] := 'revit2022';
    Years[3] := '2023'; Tasks[3] := 'revit2023';
    Years[4] := '2024'; Tasks[4] := 'revit2024';
    Years[5] := '2025'; Tasks[5] := 'revit2025';
    Years[6] := '2026'; Tasks[6] := 'revit2026';
    Years[7] := '2027'; Tasks[7] := 'revit2027';

    for i := 0 to 7 do
    begin
      if WizardIsTaskSelected(Tasks[i]) then
        InstallAddinForRevit(Years[i]);
    end;

    // Auto-configure Claude Desktop
    if WizardIsComponentSelected('claude') then
      ConfigureClaudeDesktop();

    // Create launcher script
    CreateLauncherScript();

    // Create MCP config reference
    CreateMcpConfigReference();
  end;
end;

// ── Uninstall Hook ──────────────────────────────────────────

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Years: array[0..7] of string;
  i: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Years[0] := '2020';
    Years[1] := '2021';
    Years[2] := '2022';
    Years[3] := '2023';
    Years[4] := '2024';
    Years[5] := '2025';
    Years[6] := '2026';
    Years[7] := '2027';

    for i := 0 to 7 do
      RemoveAddinForRevit(Years[i]);

    RemoveClaudeDesktopConfig();
  end;
end;
