@echo off
setlocal

set CONTAINER_NAME=seq
set SEQ_PORT=5341
set SEQ_IMAGE=datalust/seq

echo.
echo Checking if Docker is installed...
docker --version >nul 2>&1
if errorlevel 1 (
    echo Docker is not installed or not in PATH.
    echo Please install Docker Desktop and try again.
    goto :end
)

echo.
echo Starting Seq container...

REM Check if container already exists
docker ps -a --format "{{.Names}}" | findstr /i "^%CONTAINER_NAME%$" >nul
if %errorlevel%==0 (
    echo Container '%CONTAINER_NAME%' already exists.
    echo Starting existing container...
    docker start %CONTAINER_NAME%
) else (
    echo Creating new Seq container...
	docker run --name %CONTAINER_NAME% -d -e ACCEPT_EULA=Y -e SEQ_FIRSTRUN_NOAUTHENTICATION=true -p %SEQ_PORT%:80 %SEQ_IMAGE%
)

echo.
echo Seq should now be running at: http://localhost:%SEQ_PORT%
echo You can log structured events from your Web API using Serilog.Sinks.Seq.
echo.

:end
pause