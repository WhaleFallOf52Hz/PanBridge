#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef PayloadDir
  #error PayloadDir is required
#endif

[Setup]
AppId={{6CBC63AC-80A3-470A-88B6-9FCBC0BA4F58}
AppName=交云桥 · PanBridge
AppVersion={#AppVersion}
AppVerName=交云桥 · PanBridge {#AppVersion}
AppPublisher=PanBridge contributors
AppPublisherURL=https://github.com/WhaleFallOf52Hz/PanBridge
AppSupportURL=https://github.com/WhaleFallOf52Hz/PanBridge/issues
DefaultDirName={localappdata}\Programs\PanBridge
DefaultGroupName=交云桥
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=PanBridge-{#AppVersion}-Setup-x64
SetupIconFile={#RepositoryDir}\src\PanBridge\Assets\PanBridge.ico
UninstallDisplayIcon={app}\PanBridge.exe
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\PanBridge.Desktop
CloseApplications=no
RestartApplications=no
ChangesEnvironment=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "startup"; Description: "Windows 登录后在托盘运行交云桥"; GroupDescription: "启动选项："
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked
Name: "winfsp"; Description: "安装挂载组件 WinFsp（仅缺失时安装，需要管理员权限）"; GroupDescription: "组件："; Check: not WinFspInstalled

[Files]
#include PayloadFiles
Source: "{#PayloadDir}\package-files.sha256"; DestName: "current-files.sha256"; Flags: dontcopy
Source: "{#RepositoryDir}\installer\legacy-v0.3.1.sha256"; Flags: dontcopy

[Icons]
Name: "{group}\交云桥"; Filename: "{app}\PanBridge.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\交云桥"; Filename: "{app}\PanBridge.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PanBridge"; ValueData: """{app}\PanBridge.exe"" --startup"; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "PanBridge"; Tasks: not startup; Flags: deletevalue

[Run]
Filename: "{app}\PanBridge.exe"; Description: "启动交云桥"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
var PreviousFiles, CurrentFiles: TArrayOfString;

function GetFileAttributesW(Path: String): LongWord;
external 'GetFileAttributesW@kernel32.dll stdcall';

function SafePackagePath(RelativePath: String): Boolean;
var Part, AbsolutePath, BasePath: String;
begin
  Result := False;
  if (RelativePath = '') or (Pos('..', RelativePath) > 0) or (Pos(':', RelativePath) > 0) then Exit;
  if (RelativePath[1] = '/') or (RelativePath[1] = '\') then Exit;
  StringChangeEx(RelativePath, '/', '\', True);
  Part := Lowercase(RelativePath);
  if (Pos('data\', Part) = 1) or (Pos('cache\', Part) = 1) or (Pos('browser-profile\', Part) = 1) then Exit;
  BasePath := ExpandConstant('{app}');
  AbsolutePath := AddBackslash(BasePath) + RelativePath;
  // Never follow a directory junction or symbolic link during cleanup/reuse.
  while CompareText(AbsolutePath, BasePath) <> 0 do begin
    if (GetFileAttributesW(AbsolutePath) and $400) <> 0 then
      if FileExists(AbsolutePath) or DirExists(AbsolutePath) then Exit;
    AbsolutePath := ExtractFileDir(AbsolutePath);
    if Length(AbsolutePath) < Length(BasePath) then Exit;
  end;
  Result := (GetFileAttributesW(BasePath) and $400) = 0;
end;

function ShouldInstallFile(RelativePath, Hash: String): Boolean;
var Path: String;
begin
  Result := True;
  if not SafePackagePath(RelativePath) then Exit;
  StringChangeEx(RelativePath, '/', '\', True);
  Path := AddBackslash(ExpandConstant('{app}')) + RelativePath;
  if FileExists(Path) then begin
    try
      Result := CompareText(GetSHA256OfFile(Path), Hash) <> 0;
      if not Result then Log('Reusing unchanged component: ' + RelativePath);
    except
      Result := True;
    end;
  end;
end;

function StillPackaged(RelativePath: String): Boolean;
var I: Integer;
begin
  Result := False;
  for I := 0 to GetArrayLength(CurrentFiles) - 1 do
    if CompareText(Copy(CurrentFiles[I], 67, Length(CurrentFiles[I])), RelativePath) = 0 then begin
      Result := True; Exit;
    end;
end;

procedure RemoveObsoletePackageFiles;
var I: Integer; RelativePath, Hash, Path, ParentPath, BasePath: String;
begin
  BasePath := ExpandConstant('{app}');
  for I := 0 to GetArrayLength(PreviousFiles) - 1 do begin
    Hash := Copy(PreviousFiles[I], 1, 64);
    RelativePath := Copy(PreviousFiles[I], 67, Length(PreviousFiles[I]));
    if (Length(Hash) = 64) and SafePackagePath(RelativePath) and not StillPackaged(RelativePath) then begin
      StringChangeEx(RelativePath, '/', '\', True);
      Path := AddBackslash(BasePath) + RelativePath;
      if FileExists(Path) then begin
        try
          // Preserve modified/unknown files and every user's data/cache path.
          if CompareText(GetSHA256OfFile(Path), Hash) = 0 then
            if DeleteFile(Path) then begin
              Log('Removed obsolete package file: ' + RelativePath);
              ParentPath := ExtractFileDir(Path);
              while CompareText(ParentPath, BasePath) <> 0 do begin
                if not RemoveDir(ParentPath) then Break;
                ParentPath := ExtractFileDir(ParentPath);
              end;
            end;
        except
          Log('Keeping inaccessible obsolete file: ' + RelativePath);
        end;
      end;
    end;
  end;
end;

function WinFspInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM64, 'SOFTWARE\WinFsp') or RegKeyExists(HKLM32, 'SOFTWARE\WinFsp');
end;

function RequestSafeExit(AppExe: String): Boolean;
var ExitCode, Attempt: Integer;
begin
  Result := True;
  if not CheckForMutexes('Local\PanBridge.Desktop') then Exit;
  if FileExists(AppExe) then
    Exec(AppExe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  for Attempt := 1 to 100 do begin
    if not CheckForMutexes('Local\PanBridge.Desktop') then Exit;
    Sleep(200);
  end;
  SuppressibleMsgBox('交云桥仍在运行。请保存云盘文件并从托盘安全退出，等待上传结束后重试。安装程序不会强制结束挂载进程。', mbInformation, MB_OK, IDOK);
  Result := False;
end;

function InitializeSetup: Boolean;
var PreviousDir, RegisteredDir: String;
begin
  PreviousDir := ExpandConstant('{localappdata}\Programs\PanBridge');
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{6CBC63AC-80A3-470A-88B6-9FCBC0BA4F58}_is1', 'InstallLocation', RegisteredDir) then PreviousDir := RegisteredDir;
  Result := RequestSafeExit(AddBackslash(PreviousDir) + 'PanBridge.exe');
end;

function InitializeUninstall: Boolean;
begin
  Result := RequestSafeExit(ExpandConstant('{app}\PanBridge.exe'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command: String;
begin
  // Preserve data/ and the shared driver. Remove only a startup entry pointing at this installation.
  if (CurUninstallStep = usPostUninstall) and RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PanBridge', Command) then
    if CompareText(Command, '"' + ExpandConstant('{app}\PanBridge.exe') + '" --startup') = 0 then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PanBridge');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var ExitCode: Integer;
begin
  if CurStep = ssInstall then begin
    ExtractTemporaryFile('current-files.sha256');
    if not LoadStringsFromFile(ExpandConstant('{tmp}\current-files.sha256'), CurrentFiles) then
      RaiseException('Package manifest could not be loaded.');
    if not LoadStringsFromFile(ExpandConstant('{app}\package-files.sha256'), PreviousFiles) then begin
      ExtractTemporaryFile('legacy-v0.3.1.sha256');
      LoadStringsFromFile(ExpandConstant('{tmp}\legacy-v0.3.1.sha256'), PreviousFiles);
    end;
  end;
  if CurStep = ssPostInstall then RemoveObsoletePackageFiles;
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('winfsp') and not WinFspInstalled then begin
    WizardForm.StatusLabel.Caption := '正在安装 WinFsp 挂载组件…';
    if not ShellExec('runas', ExpandConstant('{sys}\msiexec.exe'), '/i "' + ExpandConstant('{app}\runtime\winfsp.msi') + '" /qn /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      SuppressibleMsgBox('挂载组件安装已取消。稍后可在交云桥的偏好设置中重试。', mbInformation, MB_OK, IDOK)
    else if ExitCode = 3010 then
      SuppressibleMsgBox('WinFsp 已安装。请重启 Windows 后连接盘符。', mbInformation, MB_OK, IDOK)
    else if ExitCode <> 0 then
      SuppressibleMsgBox('WinFsp 未完成安装。稍后可在交云桥的偏好设置中重试。', mbInformation, MB_OK, IDOK);
  end;
end;
