@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Instale o SDK .NET 8 em https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
dotnet run --project "..\Tests\Tests.csproj" -c Release
if errorlevel 1 exit /b 1
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableWindowsTargeting=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo Falha na compilacao. Confira sua conexao com a internet para restaurar o pacote NAudio.
  pause
  exit /b 1
)
echo.
echo Executavel em bin\Release\net8.0-windows\win-x64\publish\AudioSignalInspector.exe
pause
