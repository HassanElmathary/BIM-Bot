; Inno Setup Script for Revit MCP Installer
; Creates a distributable .exe that bundles Node.js, MCP server, and Revit plugin

#define MyAppName "Revit MCP"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "RevitMCP"
#define MyAppURL "https://github.com/YOUR_GITHUB_USERNAME/revit-mcp"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=RevitMCP-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "server"; Description: "MCP Server (Node.js)"; Types: full custom; Flags: fixed
Name: "plugin"; Description: "Revit Plugin"; Types: full custom; Flags: fixed
Name: "nodejs"; Description: "Portable Node.js Runtime"; Types: full custom; Flags: fixed

[Tasks]
; Auto-detect installed Revit versions
Name: "revit2020"; Description: "Revit 2020"; GroupDescription: "Install plugin for:"; Check: IsRevitInstalled('2020')
Name: "revit2021"; Description: "Revit 2021"; GroupDescription: "Install plugin for:"; Check: IsRevitInstalled('2021')
Name: "revit2022"; Description: "Revit 2022"; GroupDescription: "Install plugin for:"; Check: IsRevitInstalled('2022')
Name: "revit2023"; Description: "Revit 2023"; GroupDescription: "Install plugin for:"; Check: IsRevitInstalled('2023')
Name: "revit2024"; Description: "Revit 2024"; GroupDescription: "Install plugin for:"; Check: IsRevitInstalled('2024')
Name: "revit2025"; Description: "Revit 2025"; GroupDescription: "Install plugin for:"; Check: IsRevitInstalled('2025')
Name: "revit2026"; Description: "Revit 2026"; GroupDescription: "Install plugin for:"; Check: IsRevitInstalled('2026')

[Files]
; Node.js portable runtime
Source: "nodejs\*"; DestDir: "{app}\nodejs"; Flags: ignoreversion recursesubdirs; Components: nodejs

; MCP Server
Source: "..\revit-mcp-server\build\*"; DestDir: "{app}\server\build"; Flags: ignoreversion recursesubdirs; Components: server
Source: "..\revit-mcp-server\node_modules\*"; DestDir: "{app}\server\node_modules"; Flags: ignoreversion recursesubdirs; Components: server
Source: "..\revit-mcp-server\package.json"; DestDir: "{app}\server"; Flags: ignoreversion; Components: server

; Revit Plugin DLL (both framework builds)
Source: "..\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net48\*"; DestDir: "{app}\plugin\net48"; Flags: ignoreversion recursesubdirs; Components: plugin
Source: "..\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net8.0-windows\*"; DestDir: "{app}\plugin\net8"; Flags: ignoreversion recursesubdirs; Components: plugin

; Add-in manifest for each Revit version
Source: "..\revit-mcp-plugin\RevitMCPPlugin\RevitMCP.addin"; DestDir: "{app}\plugin"; Flags: ignoreversion; Components: plugin

[Run]
; Register the add-in for selected Revit versions after install
Filename: "{cmd}"; Parameters: "/c echo Installation complete"; Description: "Complete Setup"; Flags: runhidden

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c echo Uninstalling"; Flags: runhidden

[Code]
// Check if a specific Revit version is installed
function IsRevitInstalled(Year: string): Boolean;
var
  RevitPath: string;
begin
  RevitPath := ExpandConstant('{pf}\Autodesk\Revit ' + Year);
  Result := DirExists(RevitPath);
end;

// Get the Revit AddIns directory for a given year
function GetRevitAddInsDir(Year: string): string;
begin
  Result := ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\' + Year);
end;

// Get the correct plugin subfolder based on Revit year
function GetPluginSubfolder(YearInt: Integer): string;
begin
  if YearInt <= 2024 then
    Result := 'net48'
  else
    Result := 'net8';
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

  YearInt := StrToInt(Year);
  PluginFolder := GetPluginSubfolder(YearInt);

  // Create the .addin file pointing to the correct framework build
  AddinContent := '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
    '<RevitAddIns>' + #13#10 +
    '  <AddIn Type="Application">' + #13#10 +
    '    <Name>Revit MCP Plugin</Name>' + #13#10 +
    '    <Assembly>' + ExpandConstant('{app}') + '\plugin\' + PluginFolder + '\RevitMCPPlugin.dll</Assembly>' + #13#10 +
    '    <FullClassName>RevitMCPPlugin.Core.Application</FullClassName>' + #13#10 +
    '    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>' + #13#10 +
    '    <VendorId>RevitMCP</VendorId>' + #13#10 +
    '    <VendorDescription>AI-Powered Revit MCP Plugin</VendorDescription>' + #13#10 +
    '  </AddIn>' + #13#10 +
    '</RevitAddIns>';

  SaveStringToFile(AddInDir + '\RevitMCP.addin', AddinContent, False);
end;

procedure RemoveAddinForRevit(Year: string);
var
  AddinPath: string;
begin
  AddinPath := GetRevitAddInsDir(Year) + '\RevitMCP.addin';
  if FileExists(AddinPath) then
    DeleteFile(AddinPath);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Years: array[0..6] of string;
  Tasks: array[0..6] of string;
  i: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    Years[0] := '2020'; Tasks[0] := 'revit2020';
    Years[1] := '2021'; Tasks[1] := 'revit2021';
    Years[2] := '2022'; Tasks[2] := 'revit2022';
    Years[3] := '2023'; Tasks[3] := 'revit2023';
    Years[4] := '2024'; Tasks[4] := 'revit2024';
    Years[5] := '2025'; Tasks[5] := 'revit2025';
    Years[6] := '2026'; Tasks[6] := 'revit2026';

    for i := 0 to 6 do
    begin
      if IsTaskSelected(Tasks[i]) then
        InstallAddinForRevit(Years[i]);
    end;
  end;
end;

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
  end;
end;
