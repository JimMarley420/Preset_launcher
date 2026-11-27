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
english.InstallationBusy=Close PresetLauncher in every Windows session using this installation, then retry.
french.InstallationBusy=Fermez PresetLauncher dans chaque session Windows qui utilise cette installation, puis réessayez.
english.InstallationLockFailed=Setup could not lock the PresetLauncher installation directory.
french.InstallationLockFailed=Le programme d’installation n’a pas pu verrouiller le dossier de PresetLauncher.

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

[UninstallDelete]
Type: files; Name: "{app}\.presetlauncher-install.lock"

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1';
  InstallationKey = 'Software\PresetLauncher\Installation';
  InstallLockName = '.presetlauncher-install.lock';
  GenericRead = $80000000;
  OpenAlways = 4;
  OpenReparsePoint = $00200000;
  ReparsePointAttribute = $400;
  InvalidAttributes = $FFFFFFFF;
  SharingViolation = 32;

var
  InstallLock: THandle;

function OpenInstallLock(Name: String; Access, Share: Cardinal; Security: Integer;
  Disposition, Flags: Cardinal; Template: THandle): THandle;
  external 'CreateFileW@kernel32.dll stdcall';
function CloseInstallLock(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function InstallLockAttributes(Name: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

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
var
  Directory, LockPath: String;
  Attributes, ErrorCode: Cardinal;
begin
  Result := '';
  if Conflicts(HKCU32, not IsAdminInstallMode) or
     Conflicts(HKLM32, IsAdminInstallMode and not Is64BitInstallMode) then begin
    Result := CustomMessage('ArchitectureConflict');
    Exit;
  end;
  if IsWin64 and Conflicts(HKLM64, IsAdminInstallMode and Is64BitInstallMode) then begin
    Result := CustomMessage('ArchitectureConflict');
    Exit;
  end;
  if InstallLock <> 0 then Exit;

  Directory := ExpandConstant('{app}');
  LockPath := AddBackslash(Directory) + InstallLockName;
  if not ForceDirectories(Directory) then begin
    Result := CustomMessage('InstallationLockFailed');
    Exit;
  end;
  Attributes := InstallLockAttributes(LockPath);
  if (Attributes <> InvalidAttributes) and ((Attributes and ReparsePointAttribute) <> 0) then begin
    Result := CustomMessage('InstallationLockFailed');
    Exit;
  end;
  InstallLock := OpenInstallLock(LockPath, GenericRead, 0, 0, OpenAlways, OpenReparsePoint, 0);
  if InstallLock = THandle(-1) then begin
    ErrorCode := DLLGetLastError;
    InstallLock := 0;
    if ErrorCode = SharingViolation then
      Result := CustomMessage('InstallationBusy')
    else
      Result := CustomMessage('InstallationLockFailed');
    Log(Format('Unable to acquire application installation lock (Windows error %d).', [ErrorCode]));
  end;
end;

procedure ReleaseInstallLock;
begin
  if InstallLock <> 0 then begin
    CloseInstallLock(InstallLock);
    InstallLock := 0;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Keep interactive “Launch PresetLauncher” available once all writes finish. }
  if CurStep = ssPostInstall then ReleaseInstallLock;
end;

procedure DeinitializeSetup;
begin
  ReleaseInstallLock;
end;
