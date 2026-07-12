#ifndef MyAppVersion
#define MyAppVersion "0.0.0-dev"
#endif

[Setup]
AppId={{CFC2672E-E79F-4601-902B-1C5822235D0A}}
AppName=Video Analysis Lab
AppVersion={#MyAppVersion}
AppPublisher=Canonn
AppPublisherURL=https://github.com/canonn-science/VideoAnalysis
DefaultDirName={autopf}\VideoAnalysisLab
DefaultGroupName=Video Analysis Lab
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=VideoAnalysisLab-{#MyAppVersion}-win-x64-Setup
SetupIconFile=..\src\VideoAnalysis.App\Assets\canonn.ico
UninstallDisplayIcon={app}\VideoAnalysis.App.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Video Analysis Lab"; Filename: "{app}\VideoAnalysis.App.exe"
Name: "{commondesktop}\Video Analysis Lab"; Filename: "{app}\VideoAnalysis.App.exe"; Tasks: desktopicon
Name: "{group}\Uninstall Video Analysis Lab"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\VideoAnalysis.App.exe"; Description: "Launch Video Analysis Lab"; Flags: nowait postinstall skipifsilent
