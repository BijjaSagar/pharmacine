[Setup]
AppName=ClinicOS Pharmacy
AppVersion=1.0.0
DefaultDirName={autopf}\ClinicOS Pharmacy
DefaultGroupName=ClinicOS
OutputDir=Output
OutputBaseFilename=ClinicOS_Setup
Compression=lzma
SolidCompression=yes
SetupIconFile=compiler:SetupClassicIcon.ico

[Types]
Name: "full"; Description: "Full installation (Application + PostgreSQL)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "app"; Description: "ClinicOS Pharmacy Application"; Types: full custom; Flags: fixed
Name: "db"; Description: "PostgreSQL Database Engine"; Types: full

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"
Name: "backuptask"; Description: "Setup daily auto-backup task"; GroupDescription: "Database:"

[Files]
Source: "PharmacySystem.Desktop\bin\Release\net8.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Add PostgreSQL installer if available:
; Source: "postgresql-15-windows-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Components: db

[Icons]
Name: "{group}\ClinicOS Pharmacy"; Filename: "{app}\PharmacySystem.Desktop.exe"
Name: "{autodesktop}\ClinicOS Pharmacy"; Filename: "{app}\PharmacySystem.Desktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PharmacySystem.Desktop.exe"; Description: "Launch ClinicOS Pharmacy"; Flags: nowait postinstall skipifsilent

[Code]
var
  DataDirPage: TInputDirWizardPage;

procedure InitializeWizard;
begin
  DataDirPage := CreateInputDirPage(wpSelectDir,
    'Select Database Location', 'Where should the database files be stored?',
    'Select the folder where PostgreSQL database files will be kept. It is highly recommended to select a different drive (e.g., D:\ClinicOS_Data) for safety.',
    False, '');
  DataDirPage.Add('');
  DataDirPage.Values[0] := 'D:\ClinicOS_Data';
end;

function GetDataDir(Param: String): String;
begin
  Result := DataDirPage.Values[0];
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep = ssPostInstall) then
  begin
    // If we included the PostgreSQL installer, we would run it here:
    // Exec(ExpandConstant('{tmp}\postgresql-15-windows-x64.exe'), 
    //      '--mode unattended --unattendedmodeui none --datadir "' + GetDataDir('') + '" --superpassword "admin123"', 
    //      '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
    
    if WizardIsTaskSelected('backuptask') then
    begin
      // Create a scheduled task to backup DB daily
      Exec('schtasks', '/create /tn "ClinicOS_Backup" /tr "'''+ExpandConstant('{app}\backup.bat') +'''" /sc daily /st 23:00 /ru System', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;
