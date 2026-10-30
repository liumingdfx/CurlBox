@echo off
setlocal
chcp 65001
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [CurlBox] 没有找到 .NET Framework 4.x 编译器。
  pause
  exit /b 1
)


"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"..\CurlBox.exe" ^
  /win32icon:"app.ico" /win32manifest:"app.manifest" ^
  Program.cs MainForm.cs RequestBuilder.cs RequestStore.cs RequestDialogs.cs RequestLibraryView.cs AutoTokenService.cs AutoTokenDialog.cs HtmlPreview.cs Theme.cs CurlEngine.cs CurlOptionData.cs NativeMethods.cs AssemblyInfo.cs ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll

if errorlevel 1 (
  echo [CurlBox] 编译失败。
  pause
  exit /b 1
)

copy /y "CurlBox.exe.config" "..\CurlBox.exe.config"
