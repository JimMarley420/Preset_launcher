; PresetLauncher - InnoSetup Installer Script
; Build with build.ps1, which supplies the target values from architectures.json.

#ifndef TargetArchitecture
  #error Supply the architecture parameters using build.ps1
#endif
#if TargetArchitecture != "x64" && TargetArchitecture != "x86" && TargetArchitecture != "arm64"
  #error Unsupported target architecture
#endif

#ifdef RequirePinnedCompiler
  #if VER != EncodeVer(6,7,1)
    #error Expected Inno Setup 6.7.1
  #endif
#endif

#define AppName "PresetLauncher"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppPublisher "JimmyMarley"
#define AppExeName "PresetLauncherWinUI3.exe"
#define AppURL "https://github.com/JimMarley420/Preset_launcher"

[Setup]
AppId={{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases/latest

DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes

OutputDir=Output
OutputBaseFilename={#SetupName}
SetupMutex=PresetLauncherSetupMutex,Global\PresetLauncherSetupMutex

SetupIconFile=..\PresetLauncherWinUI3\favicon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

Compression=lzma2/fast
SolidCompression=yes
LZMAUseSeparateProcess=yes

WizardStyle=modern

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

MinVersion=10.0.17763

#if Mode64Architecture != ""
ArchitecturesInstallIn64BitMode={#Mode64Architecture}
#endif
ArchitecturesAllowed={#AllowedArchitecture}

[Languages]
Name: "french";  MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.ArchitectureConflict=Remove the existing PresetLauncher installation before switching architecture or installation scope. Uninstalling preserves your data and settings.
french.ArchitectureConflict=Désinstallez PresetLauncher avant de changer d’architecture ou de mode d’installation. La désinstallation conserve vos données et paramètres.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\{#PayloadRid}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Inno Setup recreates its uninstall key after processing [Registry].
Root: HKA; Subkey: "Software\PresetLauncher\Installation"; ValueType: string; ValueName: "PresetLauncherArchitecture"; ValueData: "{#TargetArchitecture}"; Flags: uninsdeletevalue uninsdeletekeyifempty

[Icons]
Name: "{group}\{#AppName}";                          Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}";    Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";                  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; \
  Description: "{cm:LaunchProgram,{#AppName}}"; \
  Flags: nowait postinstall skipifsilent

; [UninstallDelete] intentionnellement absent : les données utilisateur
; ({localappdata}\PresetLauncher\) sont conservées à la désinstallation.

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1';
  InstallationKey = 'Software\PresetLauncher\Installation';

function Conflicts(RootKey: Integer; SameScopeAndView: Boolean): Boolean;
var
  Existing: String;
begin
  Result := False;
  if RegKeyExists(RootKey, UninstallKey) then begin
    if not RegQueryStringValue(RootKey, InstallationKey, 'PresetLauncherArchitecture', Existing) then
      Existing := 'x64';
    Result := not SameScopeAndView or (Existing <> '{#TargetArchitecture}');
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if Conflicts(HKCU32, not IsAdminInstallMode) or
     Conflicts(HKLM32, IsAdminInstallMode and not Is64BitInstallMode) then begin
    Result := CustomMessage('ArchitectureConflict');
    Exit;
  end;
  if IsWin64 then
    if Conflicts(HKLM64, IsAdminInstallMode and Is64BitInstallMode) then
      Result := CustomMessage('ArchitectureConflict');
end;
