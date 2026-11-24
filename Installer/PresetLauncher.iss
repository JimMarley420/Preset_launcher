; PresetLauncher - InnoSetup Installer Script
; Build output expected at: ..\publish\win-x64\
; dotnet publish -r win-x64 --self-contained -c Release -o ..\publish\win-x64 PresetLauncherWinUI3\PresetLauncherWinUI3.csproj

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

; [UninstallDelete] intentionnellement absent : les données utilisateur
; ({localappdata}\PresetLauncher\) sont conservées à la désinstallation.
