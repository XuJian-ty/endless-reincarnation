@echo off
setlocal EnableExtensions EnableDelayedExpansion
title Endless Reincarnation - All Backends

set "PROJECT_ROOT=%~dp0"
if "%PROJECT_ROOT:~-1%"=="\" set "PROJECT_ROOT=%PROJECT_ROOT:~0,-1%"
set "DOCKER_EXE=%ProgramFiles%\Docker\Docker\resources\bin\docker.exe"
set "DOCKER_DESKTOP=%ProgramFiles%\Docker\Docker\Docker Desktop.exe"
set "CURL_EXE=%SystemRoot%\System32\curl.exe"

echo ============================================================
echo   Endless Reincarnation - Development Backends
echo ============================================================
echo.

if not exist "%DOCKER_EXE%" goto docker_not_installed

"%DOCKER_EXE%" info >nul 2>&1
if not errorlevel 1 goto docker_ready

if not exist "%DOCKER_DESKTOP%" goto docker_not_installed
echo [1/3] Starting Docker Desktop. Please wait...
start "" /min "%DOCKER_DESKTOP%"

set /a DOCKER_ATTEMPT=0
:wait_docker
set /a DOCKER_ATTEMPT+=1
if !DOCKER_ATTEMPT! GTR 60 goto docker_timeout
timeout /t 2 /nobreak >nul
"%DOCKER_EXE%" info >nul 2>&1
if errorlevel 1 goto wait_docker

:docker_ready
echo [1/3] Docker Desktop is ready.
echo [2/3] Building and starting MySQL, SocialBackend and OnlineDungeonServer...

set "COMPOSE_ROOT="
set "TEMP_DRIVE="
for %%D in (Z Y X W V U T S R) do (
    if not exist "%%D:\" (
        subst %%D: "%PROJECT_ROOT%" >nul 2>&1
        if not errorlevel 1 (
            set "TEMP_DRIVE=%%D:"
            set "COMPOSE_ROOT=%%D:"
            goto compose_drive_ready
        )
    )
)
goto drive_mapping_failed

:compose_drive_ready
pushd !COMPOSE_ROOT!\
"%DOCKER_EXE%" compose --project-directory !COMPOSE_ROOT!\ -f !COMPOSE_ROOT!\docker-compose.yml up -d --build
set "COMPOSE_EXIT=!ERRORLEVEL!"
popd
subst !TEMP_DRIVE! /D >nul 2>&1
if not "!COMPOSE_EXIT!"=="0" goto compose_failed

echo [3/3] Waiting for backend health checks...
set /a BACKEND_ATTEMPT=0
:wait_backends
set /a BACKEND_ATTEMPT+=1
if !BACKEND_ATTEMPT! GTR 120 goto backend_timeout

set "SOCIAL_READY=0"
set "DUNGEON_READY=0"
"%CURL_EXE%" --silent --fail --max-time 2 http://127.0.0.1:5076/api/health >nul 2>&1
if not errorlevel 1 set "SOCIAL_READY=1"
"%CURL_EXE%" --silent --fail --max-time 2 http://127.0.0.1:5086/api/health >nul 2>&1
if not errorlevel 1 set "DUNGEON_READY=1"
if "!SOCIAL_READY!!DUNGEON_READY!"=="11" goto backends_ready

timeout /t 1 /nobreak >nul
goto wait_backends

:backends_ready
echo.
echo ============================================================
echo   All backend services are ready
echo   SocialBackend:       http://127.0.0.1:5076
echo   OnlineDungeonServer: http://127.0.0.1:5086
echo   MySQL:               127.0.0.1:3306
echo ============================================================
echo.
echo The backends will keep running in Docker Desktop. You can now use Unity.
echo Closing this window will not stop the backends.
pause
exit /b 0

:docker_not_installed
echo [FAILED] Docker Desktop was not found. Install Docker Desktop first.
goto failed

:docker_timeout
echo [FAILED] Docker Desktop did not become ready within two minutes.
goto failed

:drive_mapping_failed
echo [FAILED] Could not create a temporary drive for the project path.
goto failed

:compose_failed
echo [FAILED] Docker Compose could not build or start the services. See the log above.
goto failed

:backend_timeout
echo [FAILED] The backend health checks did not pass within two minutes.
"%DOCKER_EXE%" compose -f "%PROJECT_ROOT%\docker-compose.yml" ps
goto failed

:failed
echo.
pause
exit /b 1
