@echo off
chcp 936 >nul
cd /d "%~dp0"
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [错误] 未找到系统自带 C# 编译器 csc.exe
    pause
    exit /b 1
)
echo 正在编译（.NET Framework 4.x, WPF 现代化版）...
"%CSC%" /nologo /target:winexe /codepage:65001 /out:"阴阳师工具.exe" /win32icon:"icon.ico" /win32manifest:"app.manifest" /r:System.dll /r:System.Core.dll /r:System.Xaml.dll /r:WindowsBase.dll /r:PresentationCore.dll /r:PresentationFramework.dll /r:System.Windows.Forms.dll "阴阳师工具.cs"
if errorlevel 1 (
    echo [编译失败] 请检查上方错误信息
    pause
    exit /b 1
)
echo [编译成功] 已生成 阴阳师工具.exe
pause