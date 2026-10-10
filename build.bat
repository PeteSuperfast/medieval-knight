@echo off
setlocal

rem Builds the Windows player to Builds\MedievalKnight.exe using Unity in batchmode.
rem Close the Unity Editor for this project first. Log: build.log
rem Override the editor location with:  set UNITY_EXE=C:\path\to\Unity.exe

rem %~dp0 ends with a backslash, which would escape the closing quote of -projectPath.
set "PROJ=%~dp0"
if "%PROJ:~-1%"=="\" set "PROJ=%PROJ:~0,-1%"

if not defined UNITY_EXE (
    for /f "tokens=2" %%v in ('findstr /b /c:"m_EditorVersion:" "%PROJ%\ProjectSettings\ProjectVersion.txt"') do set "UNITY_VERSION=%%v"
)
if not defined UNITY_EXE (
    if not defined UNITY_VERSION (
        echo [build] ERROR: could not read the editor version from ProjectSettings\ProjectVersion.txt
        exit /b 2
    )
)
if not defined UNITY_EXE set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe"

if not exist "%UNITY_EXE%" (
    echo [build] ERROR: Unity editor not found at "%UNITY_EXE%"
    echo [build] Install that version with Unity Hub, or set UNITY_EXE to the full path of Unity.exe.
    exit /b 2
)

echo [build] Unity:   %UNITY_EXE%
echo [build] Project: %PROJ%
echo [build] Log:     %PROJ%\build.log

"%UNITY_EXE%" -batchmode -quit -nographics -projectPath "%PROJ%" -executeMethod BuildScript.BuildWindows -logFile "%PROJ%\build.log"
set "RC=%ERRORLEVEL%"

if "%RC%"=="0" (
    echo [build] OK: %PROJ%\Builds\MedievalKnight.exe
) else (
    echo [build] FAILED with exit code %RC%. See build.log
)
exit /b %RC%
