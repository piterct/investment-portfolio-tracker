@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK nao encontrado.
  echo Instale o SDK do .NET 10 e execute este arquivo novamente.
  echo https://dotnet.microsoft.com/download/dotnet/10.0
  pause
  exit /b 1
)
echo Iniciando Controle de Carteira em http://localhost:5080
start "" http://localhost:5080
dotnet run --urls http://localhost:5080
pause
