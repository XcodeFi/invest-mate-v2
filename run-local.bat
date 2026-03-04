@echo off
REM Investment Mate v2 - Simple Local Development Startup Script
REM This script starts both backend and frontend in separate command windows

echo 🚀 Starting Investment Mate v2 Local Development Environment
echo ===========================================================

REM Check if .NET is available
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ .NET SDK not found. Please install .NET 9 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Check if Node.js is available
node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Node.js not found. Please install Node.js from https://nodejs.org/
    pause
    exit /b 1
)

echo ✅ Prerequisites check passed
echo.

REM Build backend
echo 🔨 Building backend...
cd src\InvestmentApp.Api
dotnet clean
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo ❌ Backend build failed
    cd ..\..
    pause
    exit /b 1
)
cd ..\..
echo ✅ Backend built successfully
echo.

REM Install frontend dependencies
echo 🔨 Installing frontend dependencies...
cd frontend
call npm install
if %errorlevel% neq 0 (
    echo ❌ Frontend npm install failed
    cd ..
    pause
    exit /b 1
)
cd ..
echo ✅ Frontend dependencies installed
echo.

REM Start backend in new window
echo 🚀 Starting backend API...
start "Investment Mate v2 - Backend API" cmd /k "cd /d %~dp0src\InvestmentApp.Api && dotnet run --urls=http://localhost:5000"

REM Wait a moment for backend to start
timeout /t 5 /nobreak >nul

REM Start frontend in new window
echo 🚀 Starting frontend...
start "Investment Mate v2 - Frontend" cmd /k "cd /d %~dp0frontend && npx ng serve --port 4200"

echo.
echo 🎉 Investment Mate v2 is starting up!
echo ======================================
echo 🔗 Backend API will be available at: http://localhost:5000
echo 📖 API Docs will be available at:    http://localhost:5000/swagger
echo 🖥️  Frontend will be available at:    http://localhost:4200
echo.
echo ⚠️  Close the command windows to stop the services
echo 💡 Use Ctrl+C in each window to stop individual services
echo.
pause