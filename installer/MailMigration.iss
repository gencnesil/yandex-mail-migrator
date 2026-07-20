#define MyAppName "Mail Migration Desktop"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Mail Migration Desktop"
#define MyAppExeName "MailMigrationDesktop.exe"

[Setup]
AppId={{A3E5D37C-0FBB-4AD2-9ECB-820391B78F5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Mail Migration Desktop
DefaultGroupName={#MyAppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=MailMigrationDesktop-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
WizardStyle=modern

[Files]
Source: "..\artifacts\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek simgeler:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} uygulamasını çalıştır"; Flags: nowait postinstall skipifsilent
