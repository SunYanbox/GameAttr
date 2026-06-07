@echo off
cd /d "%~dp0"

echo Cleaning up previous HTML report...
rmdir /s /q CoverageReport 2>nul

echo Running tests and collecting coverage...
dotnet test "GameAttr.Tests.csproj" --collect:"XPlat Code Coverage"

echo Locating the latest coverage result folder...
for /f "delims=" %%i in ('dir /b /ad-h /od TestResults') do set "LATEST_RESULT=%%i"
if "%LATEST_RESULT%"=="" (
    echo No test results found.
    exit /b 1
)
set "COVERAGE_FILE=TestResults\%LATEST_RESULT%\coverage.cobertura.xml"
echo Using coverage file: %COVERAGE_FILE%

echo Generating HTML report from latest results only...
reportgenerator "-reports:%COVERAGE_FILE%" "-targetdir:CoverageReport" "-reporttypes:Html"

echo Generating coverage badges...
if exist "..\badges" rmdir /s /q "..\badges"
mkdir "..\badges"
reportgenerator "-reports:%COVERAGE_FILE%" "-targetdir:..\badges" "-reporttypes:Badges"
if errorlevel 1 exit /b %errorlevel%

echo Cleaning up unnecessary shields.io variants...
del /q "..\badges\badge_shieldsio_*.svg" 2>nul

echo Patching badge_combined.svg with animated metric labels...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0patch-badge.ps1"
if errorlevel 1 exit /b %errorlevel%
echo Coverage badges saved to ..\badges\

echo Opening coverage report...
start CoverageReport\index.html