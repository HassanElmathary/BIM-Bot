; ============================================================
;  BIM-Bot — Professional Installer (Inno Setup 6)
;  AI-Powered BIM Automation • 267 MCP Tools • Revit 2020–2027 + Navisworks Manage + Unreal Engine
;  One plugin build per Revit version (plugin\R2020 .. plugin\R2027), each
;  compiled against that year's API. Do not point two years at one folder —
;  the ElementId/ForgeTypeId APIs differ across the range.
;  by Hassan Ahmed Elmathary
; ============================================================

#define MyAppName      "BIM-Bot"
#define MyAppVersion   "2.7.6"
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
; Never inherit the previous install dir across scopes. Without this, a
; per-user update (/CURRENTUSER) reuses the old Program Files path from a
; machine-wide install and Windows shows a UAC admin-password prompt just to
; write files. Per-user updates must default to the user profile instead.
UsePreviousAppDir=no
; License
LicenseFile=..\LICENSE
; Output
OutputDir=output
; One installer for Revit, Navisworks and Unreal. The in-plugin updater takes
; the asset name from version.json, so the name carries no version.
OutputBaseFilename=BIM-Bot
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
; Requirements — per-user by default so updates launched from inside Revit
; never hit a UAC admin-password prompt. Addins then go to the user's
; %APPDATA% Revit folder. Users WITH admin rights can still pick
; "Install for all users" in the install-mode dialog when they want a
; machine-wide install.
PrivilegesRequired=lowest
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
WelcomeLabel2=This will install {#MyAppName} v{#MyAppVersion} on your computer.%n%n{#MyAppName} provides 267 AI-powered MCP tools for Autodesk Revit, Navisworks Manage and Unreal Engine, enabling intelligent BIM automation through Claude Desktop, ChatGPT, OpenCode, Cursor, Windsurf, and any MCP client.%n%nSupports Revit 2020–2027, Navisworks Manage 2020–2027 and Unreal Engine 5.
FinishedHeadingLabel=Installation Complete!
FinishedLabel={#MyAppName} has been successfully installed.%n%nNext Steps:%n  1. Open Revit → look for the "BIM-Bot" tab in the ribbon%n  2. Open Claude Desktop or ChatGPT Desktop → BIM-Bot tools are ready to use

[Types]
Name: "full"; Description: "Full installation (recommended)"
Name: "server"; Description: "MCP Server only (no Revit plugin)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "server"; Description: "MCP Server (Node.js) — 209 AI tools for BIM automation"; Types: full server custom; Flags: fixed
Name: "nodejs"; Description: "Portable Node.js Runtime (v20 LTS)"; Types: full server custom; Flags: fixed
Name: "plugin"; Description: "Revit Plugin — connects Revit to the MCP Server"; Types: full custom
Name: "unreal"; Description: "Unreal Engine MCP Server (BIM-Bot-Unreal)"; Types: full custom
Name: "claude"; Description: "Auto-connect Claude Desktop, ChatGPT Desktop, OpenCode & other MCP clients"; Types: full custom

[Tasks]
; Auto-detect installed Revit versions — only show those found.
Name: "revit2020"; Description: "Revit 2020 (.NET 4.7)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2020')
Name: "revit2021"; Description: "Revit 2021 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2021')
Name: "revit2022"; Description: "Revit 2022 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2022')
Name: "revit2023"; Description: "Revit 2023 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2023')
Name: "revit2024"; Description: "Revit 2024 (.NET 4.8)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2024')
Name: "revit2025"; Description: "Revit 2025 (.NET 8.0)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2025')
Name: "revit2026"; Description: "Revit 2026 (.NET 8.0)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2026')
Name: "revit2027"; Description: "Revit 2027 (.NET 8.0)"; GroupDescription: "Deploy Revit plugin to:"; Components: plugin; Check: IsRevitInstalled('2027')
Name: "navis2020"; Description: "Navisworks Manage 2020"; GroupDescription: "Deploy Navisworks plugin to:"; Components: plugin; Check: IsNavisInstalled('2020')
Name: "navis2021"; Description: "Navisworks Manage 2021"; GroupDescription: "Deploy Navisworks plugin to:"; Components: plugin; Check: IsNavisInstalled('2021')
Name: "navis2022"; Description: "Navisworks Manage 2022"; GroupDescription: "Deploy Navisworks plugin to:"; Components: plugin; Check: IsNavisInstalled('2022')
Name: "navis2023"; Description: "Navisworks Manage 2023"; GroupDescription: "Deploy Navisworks plugin to:"; Components: plugin; Check: IsNavisInstalled('2023')
Name: "navis2024"; Description: "Navisworks Manage 2024"; GroupDescription: "Deploy Navisworks plugin to:"; Components: plugin; Check: IsNavisInstalled('2024')
Name: "navis2025"; Description: "Navisworks Manage 2025"; GroupDescription: "Deploy Navisworks plugin to:"; Components: plugin; Check: IsNavisInstalled('2025')
Name: "navis2026"; Description: "Navisworks Manage 2026"; GroupDescription: "Deploy Navisworks plugin to:"; Components: plugin; Check: IsNavisInstalled('2026')
Name: "navis2027"; Description: "Navisworks Manage 2027"; GroupDescription: "Deploy Navisworks plugin to:"; Components: plugin; Check: IsNavisInstalled('2027')
; Additional options
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional options:"

[Files]
; Icon for uninstall entry
Source: "assets\bimbot.ico"; DestDir: "{app}"; Flags: ignoreversion

; Node.js portable runtime
; uninsrestartdelete: Revit locks BIMBotPlugin.dll while open and a running
; "Start MCP Server" holds node.exe/server files. Without this flag a locked
; file silently survives uninstall and the whole folder on C: is left behind;
; with it Windows deletes the file on the next reboot instead.
Source: "nodejs\*"; DestDir: "{app}\nodejs"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: nodejs

; MCP Server
Source: "..\revit-mcp-server\build\*"; DestDir: "{app}\server\build"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: server
Source: "..\revit-mcp-server\node_modules\*"; DestDir: "{app}\server\node_modules"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: server
Source: "..\revit-mcp-server\package.json"; DestDir: "{app}\server"; Flags: ignoreversion; Components: server
Source: "..\revit-mcp-server\scripts\configure-claude.cjs"; DestDir: "{app}\server\scripts"; Flags: ignoreversion; Components: server
Source: "..\revit-mcp-server\scripts\probe-revit.cjs"; DestDir: "{app}\server\scripts"; Flags: ignoreversion; Components: server
; OpenCode defaults (tool scoping + the BIM-Bot agent) read by
; configure-claude.cjs and the plugin's self-heal, plus project samples.
Source: "..\revit-mcp-server\opencode\*"; DestDir: "{app}\server\opencode"; Flags: ignoreversion; Components: server
Source: "..\installer\Install-Prerequisites.ps1"; DestDir: "{app}\server\scripts"; Flags: ignoreversion; Components: server

; Revit Plugin DLLs — one build per Revit version
; uninsrestartdelete for the same Revit-file-lock reason as above.
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\R2020\Release\net47\*"; DestDir: "{app}\plugin\R2020"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: plugin; Tasks: revit2020
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\R2021\Release\net48\*"; DestDir: "{app}\plugin\R2021"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: plugin; Tasks: revit2021
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\R2022\Release\net48\*"; DestDir: "{app}\plugin\R2022"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: plugin; Tasks: revit2022
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\R2023\Release\net48\*"; DestDir: "{app}\plugin\R2023"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: plugin; Tasks: revit2023
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\R2024\Release\net48\*"; DestDir: "{app}\plugin\R2024"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: plugin; Tasks: revit2024
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\R2025\Release\net8.0-windows\*"; DestDir: "{app}\plugin\R2025"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: plugin; Tasks: revit2025
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\R2026\Release\net8.0-windows\*"; DestDir: "{app}\plugin\R2026"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: plugin; Tasks: revit2026
Source: "..\revit-mcp-plugin\BIMBotPlugin\bin\R2027\Release\net10.0-windows\*"; DestDir: "{app}\plugin\R2027"; Flags: ignoreversion recursesubdirs uninsrestartdelete; Components: plugin; Tasks: revit2027
; Navisworks plugin — one net48 DLL PER Manage year (2020–2027). Each year's API is
; strong-named with its own version (2020 = 17 … 2027 = 24) and Navisworks only redirects
; within its own major version, so a DLL built for one year does not load in another.
; Only the DLL ships: Navisworks provides Newtonsoft.Json itself.
Source: "..\navis-mcp-plugin\BIMBotNavisPlugin\bin\N2020\Release\net48\BIMBotNavisPlugin.dll"; DestDir: "{app}\plugin-navis\N2020"; Flags: ignoreversion uninsrestartdelete; Components: plugin; Tasks: navis2020
Source: "..\navis-mcp-plugin\BIMBotNavisPlugin\bin\N2021\Release\net48\BIMBotNavisPlugin.dll"; DestDir: "{app}\plugin-navis\N2021"; Flags: ignoreversion uninsrestartdelete; Components: plugin; Tasks: navis2021
Source: "..\navis-mcp-plugin\BIMBotNavisPlugin\bin\N2022\Release\net48\BIMBotNavisPlugin.dll"; DestDir: "{app}\plugin-navis\N2022"; Flags: ignoreversion uninsrestartdelete; Components: plugin; Tasks: navis2022
Source: "..\navis-mcp-plugin\BIMBotNavisPlugin\bin\N2023\Release\net48\BIMBotNavisPlugin.dll"; DestDir: "{app}\plugin-navis\N2023"; Flags: ignoreversion uninsrestartdelete; Components: plugin; Tasks: navis2023
Source: "..\navis-mcp-plugin\BIMBotNavisPlugin\bin\N2024\Release\net48\BIMBotNavisPlugin.dll"; DestDir: "{app}\plugin-navis\N2024"; Flags: ignoreversion uninsrestartdelete; Components: plugin; Tasks: navis2024
Source: "..\navis-mcp-plugin\BIMBotNavisPlugin\bin\N2025\Release\net48\BIMBotNavisPlugin.dll"; DestDir: "{app}\plugin-navis\N2025"; Flags: ignoreversion uninsrestartdelete; Components: plugin; Tasks: navis2025
Source: "..\navis-mcp-plugin\BIMBotNavisPlugin\bin\N2026\Release\net48\BIMBotNavisPlugin.dll"; DestDir: "{app}\plugin-navis\N2026"; Flags: ignoreversion uninsrestartdelete; Components: plugin; Tasks: navis2026
Source: "..\navis-mcp-plugin\BIMBotNavisPlugin\bin\N2027\Release\net48\BIMBotNavisPlugin.dll"; DestDir: "{app}\plugin-navis\N2027"; Flags: ignoreversion uninsrestartdelete; Components: plugin; Tasks: navis2027

; Unreal Engine MCP server — a single-file Node executable built by
; unreal-mcp-server\build\build-exe.js. Its own --install copies it to
; %LOCALAPPDATA%\Programs\BIM-Bot Unreal and registers it (see RegisterUnreal).
Source: "..\unreal-mcp-server\dist\BIM-Bot-Unreal.exe"; DestDir: "{app}\unreal"; Flags: ignoreversion uninsrestartdelete; Components: unreal

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
; Installed trees (tracked by [Files], but a locked file aborts the whole
; tree delete — filesandordirs retries as much as possible).
Type: filesandordirs; Name: "{app}\server"
Type: filesandordirs; Name: "{app}\nodejs"
Type: filesandordirs; Name: "{app}\plugin"
Type: filesandordirs; Name: "{app}\plugin-navis"
Type: filesandordirs; Name: "{app}\unreal"
; Files GENERATED at install time by [Code] (CreateLauncherScript /
; CreateMcpConfigReference). Inno only auto-deletes files listed in [Files],
; so without these entries they — and therefore {app} itself — survive every
; uninstall and the folder on C: is left behind. This was the main residue.
Type: files; Name: "{app}\Start MCP Server.bat"
Type: files; Name: "{app}\mcp-config.json"
Type: files; Name: "{app}\bimbot.ico"
Type: files; Name: "{app}\LICENSE"
; Remove the app folder itself when empty. If Revit/node still holds a lock,
; uninsrestartdelete on [Files] schedules the locked file for reboot removal.
Type: dirifempty; Name: "{app}"

[Code]
// ── Revit Detection ─────────────────────────────────────────

function IsRevitInstalled(Year: string): Boolean;
var
  i: Integer;
  Root: string;
begin
  // Default location first — the common case.
  if DirExists(ExpandConstant('{pf}\Autodesk\Revit ' + Year)) then
  begin
    Result := True;
    Exit;
  end;

  // Revit can be installed anywhere; a second drive is common on workstations
  // that keep C: small, and the registry layout differs by release (2025+ no
  // longer writes an "Autodesk Revit <year>" key), so probing the filesystem is
  // the only detection that holds across 2020-2027. Getting this wrong hid the
  // checkbox entirely: no plugin was deployed and the BIM-Bot tab never
  // appeared, with nothing in the UI to explain why.
  // Ord('C')..Ord('Z') — Pascal Script has no Char loop variable.
  for i := 67 to 90 do
  begin
    Root := Chr(i) + ':\';
    if DirExists(Root + 'Program Files\Autodesk\Revit ' + Year) then
    begin
      Result := True;
      Exit;
    end;
    if DirExists(Root + 'Autodesk\Revit ' + Year) then
    begin
      Result := True;
      Exit;
    end;
  end;

  Result := False;
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
  // Every Revit version gets its own build — the APIs differ across the range.
  Result := 'R' + IntToStr(YearInt);
end;

// ── Navisworks Detection & Deployment ─────────────────────────
//
// One net48 DLL per Manage year (plugin-navis\N<year>\BIMBotNavisPlugin.dll),
// each built against that year's API. Navisworks only loads a .NET plugin from a subfolder of
// <Manage install>\Plugins\ whose name matches the assembly name:
//   Plugins\BIMBotNavisPlugin\BIMBotNavisPlugin.dll
// Both earlier 2.7.2 layouts were silently ignored — Plugins\BIMBot\ (folder
// name != assembly name) and a bare DLL in Plugins\ (root is not scanned) —
// so the navis_* MCP tools had nothing to talk to. Both are cleaned up here.
// That folder lives under Program Files, so a non-admin install retries the
// copy/delete elevated (one UAC prompt) instead of silently skipping it.

var
  // Plugins dirs (semicolon-separated) whose Navisworks plugin copy/delete
  // needs admin rights. They are handled with a single elevated call so the
  // user only ever sees one UAC prompt per install/uninstall.
  NavisCleanupDirs: string;
  // Install copies that need admin rights: 'PluginsDir|SourceDll;' pairs, because
  // every year has its own DLL.
  NavisCopyQueue: string;

const
  NavisPluginFolder = 'BIMBotNavisPlugin';

// A Manage install is a dir that actually holds Roamer.exe — a bare
// "Navisworks Manage <year>\Plugins" left behind by an older BIM-Bot
// deploy must not count as an installed Navisworks.
function GetNavisInstallDir(Year: string): string;
var
  i: Integer;
  Dir: string;
begin
  Result := '';
  Dir := ExpandConstant('{pf}\Autodesk\Navisworks Manage ' + Year);
  if FileExists(Dir + '\Roamer.exe') then
  begin
    Result := Dir;
    Exit;
  end;
  for i := 67 to 90 do
  begin
    Dir := Chr(i) + ':\Program Files\Autodesk\Navisworks Manage ' + Year;
    if FileExists(Dir + '\Roamer.exe') then
    begin
      Result := Dir;
      Exit;
    end;
  end;
end;

function IsNavisInstalled(Year: string): Boolean;
begin
  Result := GetNavisInstallDir(Year) <> '';
end;

// PowerShell fragment removing the dead layouts from one Plugins dir.
function NavisLegacyCleanupPs(PluginsDir: string): string;
begin
  Result := 'Remove-Item -LiteralPath ''' + PluginsDir + '\BIMBotNavisPlugin.dll'' -Force -ErrorAction SilentlyContinue; ' +
    'Remove-Item -LiteralPath ''' + PluginsDir + '\BIMBot'' -Recurse -Force -ErrorAction SilentlyContinue; ';
end;

// Run one elevated PowerShell that, for every 'PluginsDir|SourceDll' pair in
// Queue (semicolon-separated), creates Plugins\BIMBotNavisPlugin, copies that
// year's DLL into it and removes the dead layouts. Returns True when the DLL
// exists in every target afterwards.
function InstallNavisPluginElevated(Queue: string): Boolean;
var
  Params, Entry, Dir, Src, Target: string;
  ResultCode: Integer;
  p, bar: Integer;
  Rest: string;
begin
  Result := True;
  Params := '-NoProfile -NonInteractive -Command "';
  Rest := Queue;
  while Rest <> '' do
  begin
    p := Pos(';', Rest);
    if p = 0 then begin Entry := Rest; Rest := ''; end
    else begin Entry := Copy(Rest, 1, p - 1); Rest := Copy(Rest, p + 1, Length(Rest)); end;
    bar := Pos('|', Entry);
    if bar = 0 then Continue;
    Dir := Copy(Entry, 1, bar - 1);
    Src := Copy(Entry, bar + 1, Length(Entry));
    Target := Dir + '\' + NavisPluginFolder;
    Params := Params + 'New-Item -ItemType Directory -Force ''' + Target + ''' | Out-Null; ' +
      'Copy-Item ''' + Src + ''' ''' + Target + '\BIMBotNavisPlugin.dll'' -Force; ' +
      NavisLegacyCleanupPs(Dir);
  end;
  Params := Params + '"';
  // runas = UAC prompt. If the user declines, ShellExec returns False and we
  // fall through to the manual-copy hint below.
  if not ShellExec('runas', 'powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('Navisworks: elevated copy declined or failed to launch');
    Result := False;
    Exit;
  end;
  Rest := Queue;
  while Rest <> '' do
  begin
    p := Pos(';', Rest);
    if p = 0 then begin Entry := Rest; Rest := ''; end
    else begin Entry := Copy(Rest, 1, p - 1); Rest := Copy(Rest, p + 1, Length(Rest)); end;
    bar := Pos('|', Entry);
    if bar = 0 then Continue;
    Target := Copy(Entry, 1, bar - 1) + '\' + NavisPluginFolder + '\BIMBotNavisPlugin.dll';
    if FileExists(Target) then
      Log('Installed Navisworks plugin (elevated): ' + Target)
    else
    begin
      Log('Navisworks: elevated copy did not produce ' + Target + ' — copy BIMBotNavisPlugin.dll there by hand');
      Result := False;
    end;
  end;
end;

// The hand-made ApplicationPlugins bundle from 2.7.0 had an invalid
// PackageContents.xml (never loaded) and a stale DLL. Remove it so it can
// never double-load alongside the Plugins\BIMBotNavisPlugin copy.
procedure RemoveNavisLegacyBundle();
var
  Bundle: string;
begin
  Bundle := ExpandConstant('{commonappdata}\Autodesk\ApplicationPlugins\BIMBotNavis.bundle');
  if DirExists(Bundle) then
  begin
    DelTree(Bundle, True, True, True);
    Log('Navisworks: removed legacy bundle ' + Bundle);
  end;
end;

procedure InstallNavisPlugin(Year: string);
var
  PluginsDir, TargetDir: string;
  SrcDll: string;
begin
  PluginsDir := GetNavisInstallDir(Year) + '\Plugins';
  TargetDir := PluginsDir + '\' + NavisPluginFolder;
  SrcDll := ExpandConstant('{app}\plugin-navis\N' + Year + '\BIMBotNavisPlugin.dll');
  if not FileExists(SrcDll) then
  begin
    Log('Navisworks ' + Year + ': plugin DLL missing from install (' + SrcDll + ') — skipped');
    Exit;
  end;
  // Fast path: we can already write there (admin install).
  if ForceDirectories(TargetDir) then
  begin
    if FileCopy(SrcDll, TargetDir + '\BIMBotNavisPlugin.dll', False) then
    begin
      Log('Installed Navisworks plugin for Manage ' + Year + ' (' + TargetDir + ')');
      DeleteFile(PluginsDir + '\BIMBotNavisPlugin.dll');
      if DirExists(PluginsDir + '\BIMBot') then DelTree(PluginsDir + '\BIMBot', True, True, True);
      Exit;
    end;
  end;
  // Slow path: queue for the single elevated copy (one UAC prompt for all
  // years — see the end of CurStepChanged's Navisworks block).
  if Pos(PluginsDir + '|', NavisCopyQueue) = 0 then
    NavisCopyQueue := NavisCopyQueue + PluginsDir + '|' + SrcDll + ';';
  Log('Navisworks ' + Year + ': queued elevated copy into ' + TargetDir);
end;

procedure RemoveNavisPlugin(Year: string);
var
  i: Integer;
  Dir: string;
begin
  // Remove from every plausible location: default path and other drives.
  // Covers the current Plugins\BIMBotNavisPlugin layout and both dead
  // 2.7.2 layouts (bare DLL in Plugins\, Plugins\BIMBot\).
  for i := 0 to 1 do
  begin
    if i = 0 then Dir := ExpandConstant('{pf}\Autodesk\Navisworks Manage ' + Year + '\Plugins')
    else Dir := ExpandConstant('{sd}\Program Files\Autodesk\Navisworks Manage ' + Year + '\Plugins');
    if DirExists(Dir + '\' + NavisPluginFolder) then DelTree(Dir + '\' + NavisPluginFolder, True, True, True);
    if FileExists(Dir + '\BIMBotNavisPlugin.dll') then DeleteFile(Dir + '\BIMBotNavisPlugin.dll');
    if DirExists(Dir + '\BIMBot') then DelTree(Dir + '\BIMBot', True, True, True);
    // Anything left behind needs admin rights: queue it for the single
    // elevated cleanup at the end of the uninstall step.
    if DirExists(Dir + '\' + NavisPluginFolder) or FileExists(Dir + '\BIMBotNavisPlugin.dll')
       or DirExists(Dir + '\BIMBot') then
    begin
      if Pos(Dir + ';', NavisCleanupDirs) = 0 then
        NavisCleanupDirs := NavisCleanupDirs + Dir + ';';
      Log('Navisworks: queued elevated removal under ' + Dir);
    end;
  end;
end;

// One elevated PowerShell removing every queued Navisworks plugin copy.
// Single UAC prompt for the whole uninstall. Never touches the Plugins
// folder itself.
procedure RemoveNavisPluginElevated();
var
  Params, Dir, Rest: string;
  ResultCode: Integer;
  p: Integer;
begin
  if NavisCleanupDirs = '' then Exit;
  Params := '-NoProfile -NonInteractive -Command "';
  Rest := NavisCleanupDirs;
  while Rest <> '' do
  begin
    p := Pos(';', Rest);
    if p = 0 then begin Dir := Rest; Rest := ''; end
    else begin Dir := Copy(Rest, 1, p - 1); Rest := Copy(Rest, p + 1, Length(Rest)); end;
    if Dir = '' then Continue;
    Params := Params + 'Remove-Item -LiteralPath ''' + Dir + '\' + NavisPluginFolder + ''' -Recurse -Force -ErrorAction SilentlyContinue; ' +
      NavisLegacyCleanupPs(Dir);
  end;
  Params := Params + '"';
  if ShellExec('runas', 'powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('Navisworks: elevated removal attempted for queued plugin dirs')
  else
    Log('Navisworks: elevated removal declined — plugin copies may remain under Program Files');
  NavisCleanupDirs := '';
end;

procedure RemoveNavisPluginEverywhere(Year: string);
var
  UsersRoot: string;
  FR: TFindRec;
begin
  RemoveNavisPlugin(Year);
  UsersRoot := ExpandConstant('{sd}\Users');
  if FindFirst(UsersRoot + '\*', FR) then
  try
    repeat
      if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0)
         and (FR.Name <> '.') and (FR.Name <> '..') then
      begin
        // Legacy per-user bundle location (never scanned by Navisworks, but
        // clean it anyway so no dead copy lingers).
        if DirExists(UsersRoot + '\' + FR.Name +
          '\AppData\Roaming\Autodesk\Navisworks Manage ' + Year + '\Plugins\BIMBot') then
          DelTree(UsersRoot + '\' + FR.Name +
            '\AppData\Roaming\Autodesk\Navisworks Manage ' + Year + '\Plugins\BIMBot', True, True, True);
      end;
    until not FindNext(FR);
  finally
    FindClose(FR);
  end;
end;

// ── Addin File Management ───────────────────────────────────

// Delete every BIM-Bot / legacy RevitMCP trace inside one Addins dir:
// both manifest names and all three plugin-folder names ever deployed
// (BIMBot ← Inno Setup/current, BIMBotPlugin ← WPF + NonAdmin installers,
//  RevitMCP ← legacy product name).
procedure RemoveAddinFilesInDir(Dir: string);
begin
  if FileExists(Dir + '\BIMBot.addin') then
    DeleteFile(Dir + '\BIMBot.addin');
  if FileExists(Dir + '\RevitMCP.addin') then
    DeleteFile(Dir + '\RevitMCP.addin');
  if DirExists(Dir + '\BIMBot') then
    DelTree(Dir + '\BIMBot', True, True, True);
  if DirExists(Dir + '\BIMBotPlugin') then
    DelTree(Dir + '\BIMBotPlugin', True, True, True);
  if DirExists(Dir + '\RevitMCP') then
    DelTree(Dir + '\RevitMCP', True, True, True);
end;

procedure RemoveAddinAt(Dir: string);
begin
  RemoveAddinFilesInDir(Dir);
end;

// A manifest left behind in the other install scope shadows the one we are
// about to write — same ClientId, but pointing at an older BIMBotPlugin.dll.
procedure RemoveShadowAddins(Year: string);
var
  UsersRoot: string;
  FR: TFindRec;
begin
  if IsAdminInstallMode then
  begin
    UsersRoot := ExpandConstant('{sd}\Users');
    if FindFirst(UsersRoot + '\*', FR) then
    try
      repeat
        if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0)
           and (FR.Name <> '.') and (FR.Name <> '..') then
          RemoveAddinAt(UsersRoot + '\' + FR.Name +
            '\AppData\Roaming\Autodesk\Revit\Addins\' + Year);
      until not FindNext(FR);
    finally
      FindClose(FR);
    end;
  end
  else
    RemoveAddinAt(ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\' + Year));
end;

procedure InstallAddinForRevit(Year: string);
var
  AddInDir: string;
  AddinContent: string;
  PluginFolder: string;
  YearInt: Integer;
begin
  AddInDir := GetRevitAddInsDir(Year);
  ForceDirectories(AddInDir);
  RemoveShadowAddins(Year);

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

// Uninstall-time: remove the manifest + copied DLLs from BOTH scopes.
// The old version only cleaned GetRevitAddInsDir(Year) — the scope of the
// uninstaller — so a per-user uninstall left the machine-wide manifest
// behind (and vice versa), and Revit kept loading a DLL that points at a
// deleted folder. Clean both scopes, always.
procedure RemoveAddinEverywhere(Year: string);
var
  UsersRoot: string;
  FR: TFindRec;
begin
  RemoveAddinFilesInDir(ExpandConstant('{userappdata}\Autodesk\Revit\Addins\' + Year));
  RemoveAddinFilesInDir(ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\' + Year));
  // Machine-wide uninstall: also sweep every real user profile, so a
  // per-user install from another account does not survive.
  if IsAdminInstallMode then
  begin
    UsersRoot := ExpandConstant('{sd}\Users');
    if FindFirst(UsersRoot + '\*', FR) then
    try
      repeat
        if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0)
           and (FR.Name <> '.') and (FR.Name <> '..') then
          RemoveAddinFilesInDir(UsersRoot + '\' + FR.Name +
            '\AppData\Roaming\Autodesk\Revit\Addins\' + Year);
      until not FindNext(FR);
    finally
      FindClose(FR);
    end;
  end;
end;

procedure RemoveAddinForRevit(Year: string);
begin
  RemoveAddinEverywhere(Year);
end;

// ── Claude Configuration ────────────────────────────────────
//
// Primary path: run configure-claude.cjs with the bundled Node runtime.
// It does a real JSON parse/merge, validates that configured paths still
// exist, and REPAIRS stale entries (the old Pascal string-injection
// skipped whenever a "BIM-Bot" key was present, so a broken entry from a
// previous install location was never fixed).
// Fallback: legacy Pascal string injection if Node execution fails.

// Last-resort path, used only when the bundled Node runtime could not run
// configure-claude.cjs at all.
//
// It deliberately does NOT edit an existing config. The previous version did,
// by string substitution on an AnsiString round-trip, and that had two ways to
// destroy a working setup: a config whose "mcpServers" key was spelled with
// different spacing fell through to the "create new config" branch and
// overwrote every other MCP server the user had; and any non-ASCII byte in the
// file (a profile path with accented or Arabic characters, say) was mangled by
// the Ansi conversion, leaving JSON that Claude reports as corrupt. Writing a
// fresh file only when none exists cannot do either. Anything more subtle is
// the plugin's job: ClaudeConfigService parses the JSON properly and re-runs
// on every Revit start.
procedure ConfigureClaudeDesktopFallback();
var
  ClaudeDir: string;
  ClaudeConfig: string;
  Lines: TArrayOfString;
  NodeExe: string;
  ServerJs: string;
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
    Log('Claude Desktop config already exists — leaving it alone. ' +
        'Revit will repair or add the BIM-Bot entry on next start ' +
        '(BIM-Bot ribbon → Connect Claude does it on demand).');
    Exit;
  end;

  ForceDirectories(ClaudeDir);

  SetArrayLength(Lines, 9);
  Lines[0] := '{';
  Lines[1] := '  "mcpServers": {';
  Lines[2] := '    "BIM-Bot": {';
  Lines[3] := '      "command": "' + NodeExe + '",';
  Lines[4] := '      "args": ["' + ServerJs + '"],';
  Lines[5] := '      "env": {}';
  Lines[6] := '    }';
  Lines[7] := '  }';
  Lines[8] := '}';

  // UTF-8, no BOM. The old AnsiString write corrupted any path with non-ASCII
  // characters, and a BOM makes Claude Desktop reject the file outright.
  if SaveStringsToUTF8FileWithoutBOM(ClaudeConfig, Lines, False) then
    Log('Created Claude Desktop config with BIM-Bot')
  else
    Log('Failed to create Claude Desktop config at ' + ClaudeConfig);
end;

procedure ConfigureClaudeDesktop();
var
  NodeExe: string;
  Script: string;
  Params: string;
  ResultCode: Integer;
begin
  NodeExe := ExpandConstant('{app}\nodejs\node.exe');
  Script := ExpandConstant('{app}\server\scripts\configure-claude.cjs');
  Params := '"' + Script + '"';

  // In an elevated (admin) install, this process runs as whichever account
  // answered the UAC prompt — which is NOT necessarily the person installing.
  // Configuring only that profile is why a standard-user install could finish
  // "successfully" with no MCP client connected. Scan every real profile
  // instead; the script only touches profiles that already have a client.
  if IsAdminInstallMode then
    Params := Params + ' --all-users';

  if FileExists(NodeExe) and FileExists(Script) then
  begin
    if Exec(NodeExe, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
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

// ── MCP Client Config Cleanup (uninstall) ───────────────────
//
// Install wrote a "BIM-Bot" entry into up to 9 client configs per profile
// (see configure-claude.cjs: Claude Desktop (+MS Store), Claude Code,
// Cursor, Windsurf, Gemini CLI, VS Code, VS Code Insiders, ChatGPT Desktop, OpenCode). The old
// uninstaller only logged "left for manual cleanup", leaving every client
// pointing at a deleted node.exe/index.js — Claude then shows a dead server
// on every start. Removal preserves all other servers in each file and
// backs the file up to *.bimbot-backup first.
//
// Primary path: configure-claude.cjs --remove (real JSON parse, same client
// list as install). Runs while {app} files still exist.
// Fallback: generated PowerShell script with the same semantics, for when
// the bundled Node runtime is already gone.

procedure RemoveMcpConfigsViaNode();
var
  NodeExe: string;
  Script: string;
  Params: string;
  ResultCode: Integer;
begin
  // Runs during usUninstall, while {app} files are still present.
  NodeExe := ExpandConstant('{app}\nodejs\node.exe');
  Script := ExpandConstant('{app}\server\scripts\configure-claude.cjs');
  if (not FileExists(NodeExe)) or (not FileExists(Script)) then
  begin
    Log('Bundled node/configure script gone — PowerShell fallback will clean MCP configs');
    Exit;
  end;
  Params := '"' + Script + '" --remove';
  if IsAdminInstallMode then
    Params := Params + ' --all-users';
  if Exec(NodeExe, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('configure-claude.cjs --remove exited with code ' + IntToStr(ResultCode))
  else
    Log('Failed to execute node for --remove — PowerShell fallback will clean MCP configs');
end;

procedure RemoveMcpConfigsFallback();
var
  Lines: TArrayOfString;
  PsPath: string;
  Params: string;
  ResultCode: Integer;
begin
  SetArrayLength(Lines, 64);
  Lines[0] := 'param([switch]$AllUsers)';
  Lines[1] := '$ErrorActionPreference = ''SilentlyContinue''';
  Lines[2] := 'function Get-BimBotHomes {';
  Lines[3] := '  $list = @()';
  Lines[4] := '  if ($env:USERPROFILE) { $list += $env:USERPROFILE }';
  Lines[5] := '  if ($AllUsers -and $env:USERPROFILE) {';
  Lines[6] := '    $root = Split-Path $env:USERPROFILE';
  Lines[7] := '    Get-ChildItem $root -Directory | ForEach-Object {';
  Lines[8] := '      $p = $_.FullName';
  Lines[9] := '      if (($list -notcontains $p) -and (Test-Path (Join-Path $p ''AppData\Roaming''))) { $list += $p }';
  Lines[10] := '    }';
  Lines[11] := '  }';
  Lines[12] := '  return $list | Select-Object -Unique';
  Lines[13] := '}';
  Lines[14] := 'function Remove-BimBotEntry($File, $Key) {';
  Lines[15] := '  if (-not (Test-Path $File)) { return $false }';
  Lines[16] := '  try { $raw = [System.IO.File]::ReadAllText($File) } catch { return $false }';
  Lines[17] := '  if ($raw.Length -gt 0 -and $raw[0] -eq [char]0xFEFF) { $raw = $raw.Substring(1) }';
  Lines[18] := '  try { $cfg = $raw | ConvertFrom-Json } catch { return $false }';
  Lines[19] := '  $section = $cfg.$Key';
  Lines[20] := '  if (-not $section) { return $false }';
  Lines[21] := '  if ($section -isnot [PSCustomObject]) { return $false }';
  Lines[22] := '  if (-not $section.PSObject.Properties[''BIM-Bot'']) { return $false }';
  Lines[23] := '  $section.PSObject.Properties.Remove(''BIM-Bot'')';
  Lines[24] := '  try {';
  Lines[25] := '    Copy-Item $File ($File + ''.bimbot-backup'') -Force';
  Lines[26] := '    $utf8 = New-Object System.Text.UTF8Encoding $false';
  Lines[27] := '    [System.IO.File]::WriteAllText($File, ($cfg | ConvertTo-Json -Depth 10), $utf8)';
  Lines[28] := '    return $true';
  Lines[29] := '  } catch { return $false }';
  Lines[30] := '}';
  Lines[31] := 'Get-CimInstance Win32_Process -Filter ''Name=''''node.exe'''''' | ForEach-Object {';
  Lines[32] := '  $cmd = $_.CommandLine';
  Lines[33] := '  if ($cmd -like ''*BIMBot*'' -or $cmd -like ''*bim-bot*'' -or $cmd -like ''*BIM-Bot*'') {';
  Lines[34] := '    try { Stop-Process -Id $_.ProcessId -Force } catch { }';
  Lines[35] := '  }';
  Lines[36] := '}';
  Lines[37] := 'foreach ($prof in Get-BimBotHomes) {';
  Lines[38] := '  $roam = Join-Path $prof ''AppData\Roaming''';
  Lines[39] := '  Remove-BimBotEntry (Join-Path $roam ''Claude\claude_desktop_config.json'') ''mcpServers'' | Out-Null';
  Lines[40] := '  Remove-BimBotEntry (Join-Path $prof ''.claude.json'') ''mcpServers'' | Out-Null';
  Lines[41] := '  Remove-BimBotEntry (Join-Path $prof ''.cursor\mcp.json'') ''mcpServers'' | Out-Null';
  Lines[42] := '  Remove-BimBotEntry (Join-Path $prof ''.codeium\windsurf\mcp_config.json'') ''mcpServers'' | Out-Null';
  Lines[43] := '  Remove-BimBotEntry (Join-Path $prof ''.gemini\settings.json'') ''mcpServers'' | Out-Null';
  Lines[44] := '  Remove-BimBotEntry (Join-Path $roam ''Code\User\mcp.json'') ''servers'' | Out-Null';
  Lines[45] := '  Remove-BimBotEntry (Join-Path $roam ''Code - Insiders\User\mcp.json'') ''servers'' | Out-Null';
  Lines[46] := '  $pkgs = Join-Path $prof ''AppData\Local\Packages''';
  Lines[47] := '  Get-ChildItem $pkgs -Directory | ForEach-Object {';
  Lines[48] := '    if ($_.Name -like ''Claude_*'' -or $_.Name -like ''AnthropicClaude*'') {';
  Lines[49] := '      Remove-BimBotEntry (Join-Path $_.FullName ''LocalCache\Roaming\Claude\claude_desktop_config.json'') ''mcpServers'' | Out-Null';
  Lines[50] := '    }';
  Lines[51] := '  }';
  // ChatGPT Desktop / Codex: TOML, so drop the [mcp_servers.BIM-Bot] table
  // (and sub-tables) line-wise, keeping every other line as-is.
  Lines[52] := '  $toml = Join-Path $prof ''.codex\config.toml''';
  Lines[53] := '  if (Test-Path $toml) {';
  Lines[54] := '    $own = $false; $changed = $false; $out = @()';
  Lines[55] := '    foreach ($l in [System.IO.File]::ReadAllLines($toml)) {';
  Lines[56] := '      if ($l -match ''^\s*\[\[?\s*mcp_servers\s*\.\s*[\x22\x27]?BIM-Bot[\x22\x27]?\s*[\.\]]'') { $own = $true } elseif ($l -match ''^\s*\[\[?\s*[A-Za-z0-9_\x22\x27-]'') { $own = $false }';
  Lines[57] := '      if ($own) { $changed = $true } else { $out += $l }';
  Lines[58] := '    }';
  Lines[59] := '    if ($changed) { Copy-Item $toml ($toml + ''.bimbot-backup'') -Force; [System.IO.File]::WriteAllLines($toml, [string[]]$out, (New-Object System.Text.UTF8Encoding $false)) }';
  Lines[60] := '  }';
  // OpenCode keeps servers under "mcp" in ~/.config/opencode/opencode.json.
  Lines[61] := '  Remove-BimBotEntry (Join-Path $prof ''.config\opencode\opencode.json'') ''mcp'' | Out-Null';
  Lines[62] := '}';
  Lines[63] := '';

  PsPath := ExpandConstant('{tmp}\bimbot-uninstall-cleanup.ps1');
  if not SaveStringsToFile(PsPath, Lines, False) then
  begin
    Log('Could not write uninstall cleanup script — MCP configs left for manual cleanup');
    Exit;
  end;
  Params := '-NoProfile -ExecutionPolicy Bypass -File "' + PsPath + '"';
  if IsAdminInstallMode then
    Params := Params + ' -AllUsers';
  Exec('powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Log('MCP config PowerShell fallback exited with code ' + IntToStr(ResultCode));
  DeleteFile(PsPath);
end;

// Runtime data the plugin writes while Revit runs (logs, settings, license,
// ProjectData, auth tokens, theme) plus the server handshake/cache files.
// Without this, %APPDATA%\BIMBot and %LOCALAPPDATA%\BIMBot survive every
// uninstall. This also covers the install.ps1 per-user app layout, which
// lives at %LOCALAPPDATA%\BIMBot.
procedure RemoveRuntimeDataDirs();
var
  UsersRoot: string;
  FR: TFindRec;
  Profile: string;
begin
  if DirExists(ExpandConstant('{userappdata}\BIMBot')) then
    DelTree(ExpandConstant('{userappdata}\BIMBot'), True, True, True);
  if DirExists(ExpandConstant('{localappdata}\BIMBot')) then
    DelTree(ExpandConstant('{localappdata}\BIMBot'), True, True, True);
  // Machine-wide uninstall: sweep other users too.
  if IsAdminInstallMode then
  begin
    UsersRoot := ExpandConstant('{sd}\Users');
    if FindFirst(UsersRoot + '\*', FR) then
    try
      repeat
        if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0)
           and (FR.Name <> '.') and (FR.Name <> '..') then
        begin
          Profile := UsersRoot + '\' + FR.Name;
          if DirExists(Profile + '\AppData\Roaming\BIMBot') then
            DelTree(Profile + '\AppData\Roaming\BIMBot', True, True, True);
          if DirExists(Profile + '\AppData\Local\BIMBot') then
            DelTree(Profile + '\AppData\Local\BIMBot', True, True, True);
        end;
      until not FindNext(FR);
    finally
      FindClose(FR);
    end;
  end;
end;

// Legacy product folder from the RevitMCP era (admin scope only).
procedure RemoveLegacyAppDirs();
begin
  if IsAdminInstallMode then
  begin
    if DirExists(ExpandConstant('{pf}\RevitMCP')) then
      DelTree(ExpandConstant('{pf}\RevitMCP'), True, True, True);
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
  RevitCount: Integer;
begin
  Result := True;
  // Free file locks BEFORE any deletion: "Start MCP Server" consoles run
  // node from {app} and Revit holds the plugin DLL open. Without this the
  // locked files (and their folders) silently survive uninstall.
  Exec('taskkill.exe', '/F /FI "WINDOWTITLE eq BIM-Bot*"', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
  // Count running Revit instances via the PowerShell exit code (0 = none).
  if Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -Command "exit (@(Get-Process Revit -ErrorAction SilentlyContinue).Count)"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    RevitCount := ResultCode
  else
    RevitCount := 0;
  if RevitCount > 0 then
  begin
    if MsgBox('Revit is still running and locks the BIM-Bot plugin files.' + #13#10 + #13#10 +
      'Please close Revit first for a complete uninstall.' + #13#10 + #13#10 +
      'YES = continue now (locked files are removed on reboot)' + #13#10 +
      'NO = cancel so you can close Revit first (recommended)',
      mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
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

function GetUninstallStringForScope(AdminScope: Boolean): string;
var
  UninstallStr: string;
begin
  Result := '';
  if AdminScope then
  begin
    if RegQueryStringValue(HKLM, UninstallRegKey, 'UninstallString', UninstallStr) then
      Result := RemoveQuotes(UninstallStr);
  end
  else
  begin
    if RegQueryStringValue(HKCU, UninstallRegKey, 'UninstallString', UninstallStr) then
      Result := RemoveQuotes(UninstallStr);
  end;
end;

function GetUninstallString(): string;
begin
  // Scope-aware: per-user installs only see HKCU, admin installs only HKLM.
  // The old version checked HKLM first from a per-user (/CURRENTUSER) setup
  // and Exec'd the machine-wide uninstaller, which is exactly what raised
  // the Windows admin-password (UAC) prompt on every update.
  Result := GetUninstallStringForScope(IsAdminInstallMode);
end;

function GetInstalledVersion(): string;
var
  Version: string;
begin
  Result := 'unknown';
  if IsAdminInstallMode then
  begin
    if RegQueryStringValue(HKLM, UninstallRegKey, 'DisplayVersion', Version) and (Version <> '') then
      Result := Version;
  end
  else
  begin
    if RegQueryStringValue(HKCU, UninstallRegKey, 'DisplayVersion', Version) and (Version <> '') then
      Result := Version;
  end;
end;

function HasAdminInstall(): Boolean;
var
  Dummy: string;
begin
  Result := RegQueryStringValue(HKLM, UninstallRegKey, 'UninstallString', Dummy);
end;

function InitializeSetup(): Boolean;
var
  UninstallStr: string;
  InstalledVersion: string;
  ResultCode: Integer;
  Choice: Integer;
begin
  Result := True;

  // Per-user update (/CURRENTUSER): never touch the machine-wide install.
  // Exec'ing the HKLM uninstaller from here is what forced the UAC
  // admin-password prompt. The per-user .addin shadows the old one via
  // RemoveShadowAddins, so the update succeeds with no elevation.
  // The orphaned Program Files copy can be removed later, once, by someone
  // with admin rights — it is not required for the update to work.
  if (not IsAdminInstallMode) and HasAdminInstall() then
  begin
    Log('Machine-wide install detected; per-user update continues without uninstalling it (no UAC).');
    MsgBox(
      'An older machine-wide BIM-Bot install was found.' + #13#10 + #13#10 +
      'This update will install v{#MyAppVersion} for the current user only — no admin password needed.' + #13#10 + #13#10 +
      'The old Program Files copy will simply be ignored. You can remove it later with admin rights if you wish.',
      mbInformation, MB_OK);
    Exit;
  end;

  UninstallStr := GetUninstallString();
  if UninstallStr = '' then
    Exit; // Not installed in this scope — proceed with fresh install

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

// ── Unreal Engine MCP server ────────────────────────────────
//
// BIM-Bot-Unreal.exe registers itself: --install copies it to
// %LOCALAPPDATA%\Programs\BIM-Bot Unreal and writes the BIM-Bot-Unreal entry
// (plus tool permissions) into Claude Desktop, Claude Code and OpenCode;
// --uninstall removes them. stdin comes from nul: with a console stdin the
// exe asks which Unreal projects to set up and then waits for Enter, which
// would hang setup on a hidden window. It runs as the original user so an
// elevated install registers the person installing, not the admin account.

function UnrealCmdParams(Flag: string): string;
begin
  Result := '/c ""' + ExpandConstant('{app}\unreal\BIM-Bot-Unreal.exe') + '" ' + Flag + ' <nul >nul 2>&1"';
end;

procedure RegisterUnreal();
var
  ResultCode: Integer;
begin
  if not FileExists(ExpandConstant('{app}\unreal\BIM-Bot-Unreal.exe')) then
  begin
    Log('Unreal: BIM-Bot-Unreal.exe missing — not registered');
    Exit;
  end;
  if ExecAsOriginalUser(ExpandConstant('{cmd}'), UnrealCmdParams('--install'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('Unreal: BIM-Bot-Unreal.exe --install exited with code ' + IntToStr(ResultCode))
  else
    Log('Unreal: failed to run BIM-Bot-Unreal.exe --install');
end;

// Turns Remote Control, Python and Editor Scripting on by default in every
// installed engine, so any Unreal project connects without its own setup.
// The engines live in Program Files, hence 'runas' (one UAC prompt unless
// setup already runs elevated). Declining only leaves per-project setup.
procedure SetupUnrealEngines();
var
  ResultCode: Integer;
begin
  if not FileExists(ExpandConstant('{app}\unreal\BIM-Bot-Unreal.exe')) then
    Exit;
  if ShellExec('runas', ExpandConstant('{app}\unreal\BIM-Bot-Unreal.exe'), '--setup-engines --elevated', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('Unreal: BIM-Bot-Unreal.exe --setup-engines exited with code ' + IntToStr(ResultCode))
  else
    Log('Unreal: --setup-engines not run (UAC declined?) — run BIM-Bot-Unreal.exe --setup-engines later');
end;

procedure UnregisterUnreal();
var
  ResultCode: Integer;
begin
  if not FileExists(ExpandConstant('{app}\unreal\BIM-Bot-Unreal.exe')) then
    Exit;
  if Exec(ExpandConstant('{cmd}'), UnrealCmdParams('--uninstall'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('Unreal: BIM-Bot-Unreal.exe --uninstall exited with code ' + IntToStr(ResultCode))
  else
    Log('Unreal: failed to run BIM-Bot-Unreal.exe --uninstall');
  // The copy --install made; a client still running it is removed on reboot.
  if not DelTree(ExpandConstant('{localappdata}\Programs\BIM-Bot Unreal'), True, True, True) then
    Log('Unreal: could not remove {localappdata}\Programs\BIM-Bot Unreal (in use?)');
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
    // Install .addin files for selected Revit versions (2020–2027).
    // Each manifest points at plugin\R<year>, built against that year's API.
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

    // Deploy each selected Manage year's own plugin DLL (built against that year's API).
    if WizardIsTaskSelected('navis2020') then InstallNavisPlugin('2020');
    if WizardIsTaskSelected('navis2021') then InstallNavisPlugin('2021');
    if WizardIsTaskSelected('navis2022') then InstallNavisPlugin('2022');
    if WizardIsTaskSelected('navis2023') then InstallNavisPlugin('2023');
    if WizardIsTaskSelected('navis2024') then InstallNavisPlugin('2024');
    if WizardIsTaskSelected('navis2025') then InstallNavisPlugin('2025');
    if WizardIsTaskSelected('navis2026') then InstallNavisPlugin('2026');
    if WizardIsTaskSelected('navis2027') then InstallNavisPlugin('2027');

    // Flush any Navisworks copies that needed admin rights: one elevated
    // PowerShell (single UAC prompt) handles every queued year at once.
    if NavisCopyQueue <> '' then
    begin
      if not InstallNavisPluginElevated(NavisCopyQueue) then
        Log('Navisworks: some plugin copies still missing — copy BIMBotNavisPlugin.dll from {app}\plugin-navis\N<year> by hand');
      NavisCopyQueue := '';
    end;
    RemoveNavisLegacyBundle();

    // Auto-configure Claude Desktop
    if WizardIsComponentSelected('claude') then
      ConfigureClaudeDesktop();

    // Register the Unreal MCP server with the same clients
    if WizardIsComponentSelected('unreal') and WizardIsComponentSelected('claude') then
      RegisterUnreal();
    if WizardIsComponentSelected('unreal') then
      SetupUnrealEngines();

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
    // {app} files still present here: run the Node remover first, then
    // remove every Revit manifest/DLL copy in both scopes.
    RemoveMcpConfigsViaNode();
    UnregisterUnreal();

    Years[0] := '2020';
    Years[1] := '2021';
    Years[2] := '2022';
    Years[3] := '2023';
    Years[4] := '2024';
    Years[5] := '2025';
    Years[6] := '2026';
    Years[7] := '2027';

    for i := 0 to 7 do
      RemoveAddinEverywhere(Years[i]);

    RemoveNavisPluginEverywhere('2020');
    RemoveNavisPluginEverywhere('2021');
    RemoveNavisPluginEverywhere('2022');
    RemoveNavisPluginEverywhere('2023');
    RemoveNavisPluginEverywhere('2024');
    RemoveNavisPluginEverywhere('2025');
    RemoveNavisPluginEverywhere('2026');
    RemoveNavisPluginEverywhere('2027');
    RemoveNavisLegacyBundle();

    // Flush anything the non-elevated uninstall could not delete (Program
    // Files copies): one UAC prompt for all of it.
    RemoveNavisPluginElevated();

    RemoveLegacyAppDirs();
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    // {app} files are gone here: PowerShell fallback (also stops leftover
    // BIM-Bot node servers), then runtime data dirs. Covers the case where
    // the bundled Node runtime was already missing.
    RemoveMcpConfigsFallback();
    RemoveRuntimeDataDirs();
  end;
end;
