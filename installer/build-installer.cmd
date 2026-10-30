@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [CurlBoxSetup] 没有找到 .NET Framework 4.x 编译器。
  exit /b 1
)

call ..\build.cmd
if errorlevel 1 exit /b 1

if not exist "..\..\CurlBox.exe" (
  echo [CurlBoxSetup] 未找到 ..\..\CurlBox.exe，请先成功编译主程序。
  exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"..\..\CurlBoxSetup.exe" ^
  /win32icon:"..\app.ico" /win32manifest:"installer.manifest" ^
  /resource:"..\..\CurlBox.exe",CurlBoxPayload.exe ^
  /resource:"..\..\CurlBox.exe.config",CurlBoxPayload.config ^
  /resource:"..\..\THIRD_PARTY_NOTICES.txt",CurlBoxPayload.notices ^
  Installer.cs InstallerInfo.cs ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll

if errorlevel 1 (
  echo [CurlBoxSetup] 安装器编译失败。
  exit /b 1
)

echo [CurlBoxSetup] 已生成 ..\..\CurlBoxSetup.exe
