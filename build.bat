@echo off
chcp 936 >nul
cd /d "%~dp0"
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [错误] 未找到系统自带 C# 编译器 csc.exe
    pause
    exit /b 1
)
echo 正在编译（.NET Framework 4.x, WPF 现代化界面, 编译 src 目录全部源码）...

rem 生成响应文件：第一行编译选项，其余为 src 下全部 .cs 文件（递归，带引号防路径空格）
set "RSP=%TEMP%\onmyoji_build.rsp"
> "%RSP%" echo /nologo /target:winexe /codepage:65001 /out:"%~dp0阴阳师工具.exe" /win32icon:"%~dp0icon.ico" /win32manifest:"%~dp0app.manifest" /r:System.dll /r:System.Core.dll /r:System.Xaml.dll /r:WindowsBase.dll /r:PresentationCore.dll /r:PresentationFramework.dll /r:System.Windows.Forms.dll
for /f "delims=" %%f in ('dir /b /s "%~dp0src\*.cs"') do echo "%%f">> "%RSP%"

"%CSC%" @"%RSP%"
if errorlevel 1 (
    echo [编译失败] 请检查上方错误信息
    pause
    exit /b 1
)
echo [编译成功] 已生成 阴阳师工具.exe
pause
