#define AppName "VoilaTile"

#include "installer.secrets.iss"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppId={#AppId}
AppPublisher={#CompanyName}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#CompanyName}
VersionInfoCopyright={#Copyright}
DefaultDirName={pf}\{#AppName}
DefaultGroupName={#AppName}
LicenseFile=License.txt
OutputDir=Output
OutputBaseFilename={#AppName}.Installer
Compression=lzma
SolidCompression=yes
CreateUninstallRegKey=yes
Uninstallable=yes
UsePreviousAppDir=yes
UninstallDisplayName={#AppName}
DisableProgramGroupPage=yes
SetupIconFile=Assets\icon-logo.ico
UninstallDisplayIcon={app}\VoilaTile.Configurator.exe
CloseApplications=yes
CloseApplicationsFilter=VoilaTile.Snapper.exe;VoilaTile.Configurator.exe
RestartApplications=no

[Files]
Source: "..\VoilaTile.Snapper\bin\Release\net8.0-windows\publish\VoilaTile.Snapper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\VoilaTile.Configurator\bin\Release\net8.0-windows\publish\VoilaTile.Configurator.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\VoílaTile Snapper";      Filename: "{app}\VoilaTile.Snapper.exe";
Name: "{group}\VoílaTile Configurator"; Filename: "{app}\VoilaTile.Configurator.exe";
Name: "{group}\Uninstall VoílaTile";    Filename: "{uninstallexe}";

[Tasks]
Name: "autostart"; Description: "Launch VoílaTile Snapper on Windows startup"; GroupDescription: "Post-install options"
Name: "launchconfigurator"; Description: "Launch VoílaTile Configurator now"; GroupDescription: "Post-install options"

[Run]
Filename: "{app}\VoilaTile.Configurator.exe"; Description: "Launch VoílaTile Configurator"; Flags: nowait postinstall skipifsilent; Tasks: launchconfigurator

[Code]

// ---------------------- Existing version lookup ----------------------

function GetInstalledVersion(): string;
var
  rootKey: Integer;
  subKey: string;
  displayVersion: string;
begin
  Result := '';

  subKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + '{#RawAppId}' + '}}_is1';

  // Try 64-bit HKLM, then 32-bit (WOW6432), then HKCU
  rootKey := HKEY_LOCAL_MACHINE;
  if RegQueryStringValue(rootKey, subKey, 'DisplayVersion', displayVersion) then
  begin
    Result := displayVersion;
    exit;
  end;

  rootKey := HKLM32;
  if RegQueryStringValue(rootKey, subKey, 'DisplayVersion', displayVersion) then
  begin
    Result := displayVersion;
    exit;
  end;

  rootKey := HKEY_CURRENT_USER;
  if RegQueryStringValue(rootKey, subKey, 'DisplayVersion', displayVersion) then
  begin
    Result := displayVersion;
    exit;
  end;
end;

// Helper: get nth part of version string
function GetNthDelimitedString(s, delim: string; index: Integer): string;
var
  i, startPos, delimPos, count, delimLen: Integer;
begin
  Result := '';
  startPos := 1;
  count := 1;
  delimLen := Length(delim);

  while startPos <= Length(s) do
  begin
    delimPos := Pos(delim, Copy(s, startPos, Length(s) - startPos + 1));
    if delimPos = 0 then
    begin
      if count = index then
        Result := Copy(s, startPos, Length(s) - startPos + 1);
      Exit;
    end;

    if count = index then
    begin
      Result := Copy(s, startPos, delimPos - 1);
      Exit;
    end;

    startPos := startPos + delimPos + delimLen - 1;
    Inc(count);
  end;
end;

// Compare version strings (basic semantic comparison)
function CompareVersions(v1, v2: string): Integer;
var
  i, n1, n2: Integer;
  p1, p2: string;
begin
  for i := 1 to 3 do
  begin
    p1 := GetNthDelimitedString(v1, '.', i);
    p2 := GetNthDelimitedString(v2, '.', i);

    try n1 := StrToInt(p1); except n1 := 0; end;
    try n2 := StrToInt(p2); except n2 := 0; end;

    if n1 > n2 then begin Result := 1; Exit; end
    else if n1 < n2 then begin Result := -1; Exit; end;
  end;

  Result := 0; // equal
end;

// ---------------------- Get the uninstaller of existing install ----------------------

function DequoteString(const S: string): string;
begin
  Result := S;
  if (Length(Result) >= 2) and (Result[1] = '"') and (Result[Length(Result)] = '"') then
    Result := Copy(Result, 2, Length(Result) - 2);
end;

function GetExistingUninstaller(out UninstExe, UninstParams: string): Boolean;
var
  rootKey: Integer;
  subKey, uninstallString: string;
  p: Integer;
begin
  Result := False;
  UninstExe := '';
  UninstParams := '';

  subKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + '{#RawAppId}' + '}}_is1';

  // Try HKLM 64 → HKLM 32 → HKCU
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, subKey, 'UninstallString', uninstallString) or
     RegQueryStringValue(HKLM32,          subKey, 'UninstallString', uninstallString) or
     RegQueryStringValue(HKEY_CURRENT_USER,subKey, 'UninstallString', uninstallString) then
  begin
    uninstallString := Trim(uninstallString);

    // Split exe and params (handles quoted exe path)
    if (uninstallString <> '') and (uninstallString[1] = '"') then
    begin
      p := Pos('" ', uninstallString);
      if p > 0 then
      begin
        UninstExe := DequoteString(Copy(uninstallString, 1, p-1));
        UninstParams := Trim(Copy(uninstallString, p+1, MaxInt));
      end
      else
        UninstExe := DequoteString(uninstallString);
    end
    else
    begin
      p := Pos(' ', uninstallString);
      if p > 0 then
      begin
        UninstExe := Copy(uninstallString, 1, p-1);
        UninstParams := Trim(Copy(uninstallString, p+1, MaxInt));
      end
      else
        UninstExe := uninstallString;
    end;

    // Ensure silent flags to avoid double UI
    if Pos('/SILENT', UpperCase(UninstParams)) = 0 then
      UninstParams := Trim(UninstParams + ' /SILENT');
    if Pos('/SUPPRESSMSGBOXES', UpperCase(UninstParams)) = 0 then
      UninstParams := Trim(UninstParams + ' /SUPPRESSMSGBOXES');

    Result := (UninstExe <> '');
  end;
end;

// ---------------------- Version check + uninstall flow ----------------------

function InitializeSetup(): Boolean;
var
  existingVersion: string;
  cmp: Integer;
  response: Integer;
  uninstExe, uninstParams: string;
  uninstallExitCode: Integer;
begin
  Result := True;

  existingVersion := GetInstalledVersion();

  if existingVersion = '' then
    exit; // fresh install

  cmp := CompareVersions('{#AppVersion}', existingVersion);

  if cmp = 0 then
  begin
    response := MsgBox('{#AppName} version ' + existingVersion + ' is already installed.' + #13#10 +
                       'Do you want to uninstall it before reinstalling?',
                       mbConfirmation, MB_YESNO or MB_DEFBUTTON2);
    if response <> IDYES then
    begin
      Result := False;
      exit;
    end;
  end
  else if cmp < 0 then
  begin
    MsgBox('A newer version (' + existingVersion + ') is already installed. Setup will now exit.', mbInformation, MB_OK);
    Result := False;
    exit;
  end
  else
  begin
    MsgBox('An older version (' + existingVersion + ') is installed. It will now be updated to version {#AppVersion}.', mbInformation, MB_OK);
  end;

  // Run the existing uninstaller — not {uninstallexe}
  if not GetExistingUninstaller(uninstExe, uninstParams) then
  begin
    MsgBox('Could not locate the previous uninstaller. Please uninstall manually.', mbError, MB_OK);
    Result := False;
    exit;
  end;

  if not Exec(uninstExe, uninstParams, '', SW_SHOW, ewWaitUntilTerminated, uninstallExitCode) then
  begin
    MsgBox('Uninstallation failed to start. Setup will now exit.', mbError, MB_OK);
    Result := False;
    exit;
  end;

  if uninstallExitCode <> 0 then
  begin
    MsgBox(Format('Uninstallation returned exit code %d. Setup will now exit.', [uninstallExitCode]), mbError, MB_OK);
    Result := False;
    exit;
  end;
end;

// ---------------------- Autostart handling ----------------------

procedure CurStepChanged(CurStep: TSetupStep);
var
  SnapperPath: string;
begin
  if CurStep = ssPostInstall then
  begin
    if IsTaskSelected('autostart') then
    begin
      SnapperPath := ExpandConstant('{app}\VoilaTile.Snapper.exe');
      RegWriteStringValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'VoilaTile.Snapper', SnapperPath);
    end;
  end;
end;

procedure DeinitializeUninstall();
begin
  RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'VoilaTile.Snapper');
end;

