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
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartApplications=no

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
english.CloseRunning=PresetLauncher is running. Save your changes before continuing. Setup will try to close it and may force it to exit if it does not respond. Unsaved changes may be lost. Continue?
french.CloseRunning=PresetLauncher est ouvert. Enregistrez vos modifications avant de continuer. Le programme d’installation essaiera de le fermer et pourra forcer son arrêt s’il ne répond pas. Les modifications non enregistrées peuvent être perdues. Continuer ?
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

[InstallDelete]
Type: files; Name: "{app}\PresetLauncher.Update.exe"

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
  RmForceShutdown = 1;
  RmSessionKeyBytes = 66; { 33 UTF-16 characters including terminator }

var
  InstallLock: THandle;

function OpenInstallLock(Name: String; Access, Share: Cardinal; Security: Integer;
  Disposition, Flags: Cardinal; Template: THandle): THandle;
  external 'CreateFileW@kernel32.dll stdcall';
function CloseInstallLock(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function InstallLockAttributes(Name: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

{ Inno Setup 6 is a 32-bit process, including for 64-bit installation modes.
  GMEM_FIXED buffers keep the native LPCWSTR array and session key valid. }
function AllocateBuffer(Flags, Bytes: Cardinal): THandle;
  external 'GlobalAlloc@kernel32.dll stdcall';
function FreeBuffer(Buffer: THandle): THandle;
  external 'GlobalFree@kernel32.dll stdcall';
function CopyWideString(Buffer: THandle; Value: String): THandle;
  external 'lstrcpyW@kernel32.dll stdcall';
function StartRestartSession(var Session: Cardinal; Flags: Cardinal; Key: THandle): Cardinal;
  external 'RmStartSession@Rstrtmgr.dll stdcall';
function RegisterRestartFile(Session, Count: Cardinal; var FilePointer: THandle;
  AppCount: Cardinal; Apps: THandle; ServiceCount: Cardinal; Services: THandle): Cardinal;
  external 'RmRegisterResources@Rstrtmgr.dll stdcall';
function ShutdownRestartSession(Session, Flags: Cardinal; Callback: THandle): Cardinal;
  external 'RmShutdown@Rstrtmgr.dll stdcall';
function EndRestartSession(Session: Cardinal): Cardinal;
  external 'RmEndSession@Rstrtmgr.dll stdcall';

function HasSetupSwitch(Value: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Value) = 0 then begin
      Result := True;
      Exit;
    end;
end;

procedure CloseInstalledLauncher(LauncherPath: String; AllowForce: Boolean);
var
  Session, ErrorCode, Flags: Cardinal;
  Key, FilePointer: THandle;
begin
  Key := AllocateBuffer($40, RmSessionKeyBytes);
  if Key = 0 then Exit;
  try
    ErrorCode := StartRestartSession(Session, 0, Key);
    if ErrorCode <> 0 then begin
      Log(Format('Restart Manager session failed (Windows error %d).', [ErrorCode]));
      Exit;
    end;
    try
      FilePointer := AllocateBuffer(0, (Length(LauncherPath) + 1) * 2);
      if FilePointer = 0 then Exit;
      try
        CopyWideString(FilePointer, LauncherPath);
        ErrorCode := RegisterRestartFile(Session, 1, FilePointer, 0, 0, 0, 0);
        if ErrorCode <> 0 then begin
          Log(Format('Restart Manager registration failed (Windows error %d).', [ErrorCode]));
          Exit;
        end;
        Flags := 0;
        if AllowForce then Flags := RmForceShutdown;
        ErrorCode := ShutdownRestartSession(Session, Flags, 0);
        Log(Format('Restart Manager launcher shutdown returned %d.', [ErrorCode]));
      finally
        FreeBuffer(FilePointer);
      end;
    finally
      EndRestartSession(Session);
    end;
  finally
    FreeBuffer(Key);
  end;
end;

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

procedure ReleaseInstallLock;
begin
  if InstallLock <> 0 then begin
    CloseInstallLock(InstallLock);
    InstallLock := 0;
  end;
end;

function AcquireInstallLock(var Busy: Boolean): String;
var
  Directory, LockPath: String;
  Attributes, ErrorCode: Cardinal;
begin
  Result := '';
  Busy := False;
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
    Busy := ErrorCode = SharingViolation;
    if Busy then
      Result := CustomMessage('InstallationBusy')
    else
      Result := CustomMessage('InstallationLockFailed');
    Log(Format('Unable to acquire application installation lock (Windows error %d).', [ErrorCode]));
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Busy, AllowForce: Boolean;
  LauncherPath: String;
begin
  { The wizard may return to an earlier page and select another directory. }
  ReleaseInstallLock;
  if Conflicts(HKCU32, not IsAdminInstallMode) or
     Conflicts(HKLM32, IsAdminInstallMode and not Is64BitInstallMode) then begin
    Result := CustomMessage('ArchitectureConflict');
    Exit;
  end;
  if IsWin64 and Conflicts(HKLM64, IsAdminInstallMode and Is64BitInstallMode) then begin
    Result := CustomMessage('ArchitectureConflict');
    Exit;
  end;

  Result := AcquireInstallLock(Busy);
  if not Busy then Exit;
  { Honor disabled application closure, including the existing unattended opt-out. }
  if not RmSessionStarted or HasSetupSwitch('/NOCLOSEAPPLICATIONS') then Exit;
  LauncherPath := ExpandConstant('{app}\{#AppExeName}');
  if not FileExists(LauncherPath) then Exit;

  AllowForce := not HasSetupSwitch('/NOFORCECLOSEAPPLICATIONS') and
    (not WizardSilent or HasSetupSwitch('/FORCECLOSEAPPLICATIONS'));
  if not WizardSilent then
    if MsgBox(CustomMessage('CloseRunning'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) <> IDYES then Exit;

  { PrepareToInstall runs before Inno's normal Restart Manager scan.
    Close only users of the installed executable, then require the exclusive
    lease again. If closure failed or another launch won the race, stop. }
  try
    CloseInstalledLauncher(LauncherPath, AllowForce);
  except
    Log('Restart Manager launcher shutdown failed: ' + GetExceptionMessage);
    Exit;
  end;
  Result := AcquireInstallLock(Busy);
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
