; ============================================================
;  Revit MCP — Professional Installer (Inno Setup 6)
;  AI-Powered BIM Automation • 179 MCP Tools • Revit 2020–2026
;  by Hassan Ahmed Elmathary
; ============================================================

#define MyAppName      "Revit MCP"
#define MyAppVersion   "2.0.2"
#define MyAppPublisher "Hassan Ahmed Elmathary"
#define MyAppURL       "https://github.com/HassanElmathary/Revit-MCP"
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
DefaultDirName={autopf}\RevitMCP
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; License
LicenseFile=..\LICENSE
; Output
OutputDir=output
OutputBaseFilename=RevitMCP-Setup-{#MyAppVersion}
; Branding
SetupIconFile=assets\revitmcp.ico
WizardImageFile=assets\WizardImageFile.bmp
WizardSmallImageFile=assets\WizardSmallImageFile.bmp
UninstallDisplayIcon={app}\revitmcp.ico
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Appearance
WizardStyle=modern
WizardSizePercent=100
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
; Requirements
PrivilegesRequired=admin
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
WelcomeLabel2=This will install {#MyAppName} v{#MyAppVersion} on your computer.%n%n{#MyAppName} provides 179 AI-powered MCP tools for Autodesk Revit, enabling intelligent BIM automation through Claude Desktop, Cursor, Windsurf, and any MCP client.%n%nSupports Revit 2020–2026.
FinishedHeadingLabel=Installation Complete!
FinishedLabel={#MyAppName} has been successfully installed.%n%nNext Steps:%n  1. Open Revit → look for "Chat with me" in the Add-ins tab%n  2. Open Claude Desktop → Revit MCP tools are ready to use

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
; Auto-detect installed Revit versions — only show those found
Name: "revit2020"; Description: "Revit 2020 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2020')
Name: "revit2021"; Description: "Revit 2021 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2021')
Name: "revit2022"; Description: "Revit 2022 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2022')
Name: "revit2023"; Description: "Revit 2023 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2023')
Name: "revit2024"; Description: "Revit 2024 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2024')
Name: "revit2025"; Description: "Revit 2025 (.NET 8.0)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2025')
Name: "revit2026"; Description: "Revit 2026 (.NET 8.0)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2026')
; Additional options
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional options:"

[Files]
; Icon for uninstall entry
Source: "assets\revitmcp.ico"; DestDir: "{app}"; Flags: ignoreversion

; Node.js portable runtime
Source: "nodejs\*"; DestDir: "{app}\nodejs"; Flags: ignoreversion recursesubdirs; Components: nodejs

; MCP Server
Source: "..\revit-mcp-server\build\*"; DestDir: "{app}\server\build"; Flags: ignoreversion recursesubdirs; Components: server
Source: "..\revit-mcp-server\node_modules\*"; DestDir: "{app}\server\node_modules"; Flags: ignoreversion recursesubdirs; Components: server
Source: "..\revit-mcp-server\package.json"; DestDir: "{app}\server"; Flags: ignoreversion; Components: server

; Revit Plugin DLLs (both framework targets)
Source: "..\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net48\*"; DestDir: "{app}\plugin\net48"; Flags: ignoreversion recursesubdirs; Components: plugin
Source: "..\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net8.0-windows\*"; DestDir: "{app}\plugin\net8"; Flags: ignoreversion recursesubdirs; Components: plugin

; License
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu shortcuts
Name: "{group}\Start MCP Server"; Filename: "{app}\Start MCP Server.bat"; IconFilename: "{app}\revitmcp.ico"; Comment: "Start the Revit MCP Server"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; IconFilename: "{app}\revitmcp.ico"
Name: "{group}\Documentation"; Filename: "{#MyAppURL}"; IconFilename: "{app}\revitmcp.ico"
; Desktop shortcut (optional)
Name: "{autodesktop}\Revit MCP Server"; Filename: "{app}\Start MCP Server.bat"; IconFilename: "{app}\revitmcp.ico"; Comment: "Start the Revit MCP Server"; Tasks: desktopicon



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
  Result := ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\' + Year);
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

  YearInt := StrToInt(Year);
  PluginFolder := GetPluginSubfolder(YearInt);

  AddinContent := '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
    '<RevitAddIns>' + #13#10 +
    '  <AddIn Type="Application">' + #13#10 +
    '    <Name>Revit MCP Plugin</Name>' + #13#10 +
    '    <Assembly>' + ExpandConstant('{app}') + '\plugin\' + PluginFolder + '\RevitMCPPlugin.dll</Assembly>' + #13#10 +
    '    <FullClassName>RevitMCPPlugin.Core.Application</FullClassName>' + #13#10 +
    '    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>' + #13#10 +
    '    <VendorId>HassanElmathary</VendorId>' + #13#10 +
    '    <VendorDescription>AI-Powered Revit Plugin by Hassan Ahmed Elmathary</VendorDescription>' + #13#10 +
    '  </AddIn>' + #13#10 +
    '</RevitAddIns>';

  SaveStringToFile(AddInDir + '\RevitMCP.addin', AddinContent, False);
  Log('Installed .addin for Revit ' + Year + ' (' + PluginFolder + ')');
end;

procedure RemoveAddinForRevit(Year: string);
var
  AddinPath: string;
  PluginDir: string;
begin
  AddinPath := GetRevitAddInsDir(Year) + '\RevitMCP.addin';
  if FileExists(AddinPath) then
    DeleteFile(AddinPath);
  // Also remove plugin directory from addins
  PluginDir := GetRevitAddInsDir(Year) + '\RevitMCP';
  if DirExists(PluginDir) then
    DelTree(PluginDir, True, True, True);
end;

// ── Claude Desktop Configuration ────────────────────────────

procedure ConfigureClaudeDesktop();
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
      if Pos('"revit-mcp"', ExistingStr) > 0 then
      begin
        Log('Claude Desktop config already contains revit-mcp entry — skipping');
        Exit;
      end;

      // Inject into existing mcpServers block
      if Pos('"mcpServers"', ExistingStr) > 0 then
      begin
        StringChangeEx(ExistingStr, '"mcpServers": {',
          '"mcpServers": {' + #13#10 +
          '    "revit-mcp": {' + #13#10 +
          '      "command": "' + NodeExe + '",' + #13#10 +
          '      "args": ["' + ServerJs + '"],' + #13#10 +
          '      "env": {}' + #13#10 +
          '    },', True);
        SaveStringToFile(ClaudeConfig, AnsiString(ExistingStr), False);
        Log('Merged revit-mcp into existing Claude Desktop config');
        Exit;
      end;
    end;
  end;

  // Create new config
  ForceDirectories(ClaudeDir);
  ConfigContent := '{' + #13#10 +
    '  "mcpServers": {' + #13#10 +
    '    "revit-mcp": {' + #13#10 +
    '      "command": "' + NodeExe + '",' + #13#10 +
    '      "args": ["' + ServerJs + '"],' + #13#10 +
    '      "env": {}' + #13#10 +
    '    }' + #13#10 +
    '  }' + #13#10 +
    '}';
  SaveStringToFile(ClaudeConfig, AnsiString(ConfigContent), False);
  Log('Created Claude Desktop config with revit-mcp');
end;

procedure RemoveClaudeDesktopConfig();
var
  ClaudeConfig: string;
begin
  ClaudeConfig := ExpandConstant('{userappdata}\Claude\claude_desktop_config.json');
  if FileExists(ClaudeConfig) then
    Log('Claude Desktop config found — revit-mcp entry left for manual cleanup');
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
    'title Revit MCP Server v{#MyAppVersion}' + #13#10 +
    'echo.' + #13#10 +
    'echo   ======================================' + #13#10 +
    'echo     Revit MCP Server v{#MyAppVersion}' + #13#10 +
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
    '    "revit-mcp": {' + #13#10 +
    '      "command": "' + NodeExe + '",' + #13#10 +
    '      "args": ["' + ServerJs + '"],' + #13#10 +
    '      "env": {}' + #13#10 +
    '    }' + #13#10 +
    '  }' + #13#10 +
    '}';
  SaveStringToFile(ExpandConstant('{app}\mcp-config.json'), ConfigContent, False);
end;

// ── Post-Install Hook ───────────────────────────────────────

procedure CurStepChanged(CurStep: TSetupStep);
var
  Years: array[0..6] of string;
  Tasks: array[0..6] of string;
  i: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Install .addin files for selected Revit versions
    Years[0] := '2020'; Tasks[0] := 'revit2020';
    Years[1] := '2021'; Tasks[1] := 'revit2021';
    Years[2] := '2022'; Tasks[2] := 'revit2022';
    Years[3] := '2023'; Tasks[3] := 'revit2023';
    Years[4] := '2024'; Tasks[4] := 'revit2024';
    Years[5] := '2025'; Tasks[5] := 'revit2025';
    Years[6] := '2026'; Tasks[6] := 'revit2026';

    for i := 0 to 6 do
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
  Years: array[0..6] of string;
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

    for i := 0 to 6 do
      RemoveAddinForRevit(Years[i]);

    RemoveClaudeDesktopConfig();
  end;
end;
