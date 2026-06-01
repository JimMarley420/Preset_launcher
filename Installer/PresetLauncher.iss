; PresetLauncher - InnoSetup Installer Script
; Build output expected at: ..\publish\win-x64\
; dotnet publish -r win-x64 --self-contained -c Release -o ..\publish\win-x64 PresetLauncherWinUI3\PresetLauncherWinUI3.csproj

#define AppName "PresetLauncher"
#ifndef AppVersion
  #define AppVersion "1.2.5"
#endif
#define AppPublisher "BurningBox"
#define AppExeName "PresetLauncherWinUI3.exe"
#define AppURL "https://version.multichat.cloud/PresetLauncher"

[Setup]
AppId={{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes

OutputDir=Output
OutputBaseFilename=PresetLauncher_Setup_{#AppVersion}

SetupIconFile=..\PresetLauncherWinUI3\favicon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

WizardStyle=modern

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

MinVersion=10.0.17763

ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Languages]
Name: "french";  MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";                          Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}";    Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";                  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; \
  Description: "{cm:LaunchProgram,{#AppName}}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\{#AppName}"
