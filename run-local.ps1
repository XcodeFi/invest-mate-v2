# Investment Mate v2 - Local Development Startup Script
# This script starts both the backend API and frontend simultaneously

param(
    [switch]$SkipBuild,
    [switch]$SkipFrontend,
    [switch]$SkipBackend
)

Write-Host "Starting Investment Mate v2 Local Development Environment" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Yellow

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Cyan

# Check if .NET SDK is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host ".NET SDK not found. Please install .NET 9 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

# Check if Node.js is installed
try {
    $nodeVersion = node --version
    Write-Host "Node.js found: $nodeVersion" -ForegroundColor Green
} catch {
    Write-Host "Node.js not found. Please install Node.js from https://nodejs.org/" -ForegroundColor Red
    exit 1
}

# Check ports availability
$backendPort = 5000
$frontendPort = 4200

if (-not $SkipBackend) {
    $connection = Test-NetConnection -ComputerName localhost -Port $backendPort -WarningAction SilentlyContinue
    if ($connection.TcpTestSucceeded) {
        Write-Host "Port $backendPort is already in use. Please free it or change the port." -ForegroundColor Red
        exit 1
    }
    Write-Host "Port $backendPort is available for backend" -ForegroundColor Green
}

if (-not $SkipFrontend) {
    $connection = Test-NetConnection -ComputerName localhost -Port $frontendPort -WarningAction SilentlyContinue
    if ($connection.TcpTestSucceeded) {
        Write-Host "Port $frontendPort is already in use. Please free it or change the port." -ForegroundColor Red
        exit 1
    }
    Write-Host "Port $frontendPort is available for frontend" -ForegroundColor Green
}

# Build backend if not skipped
if (-not $SkipBackend -and -not $SkipBuild) {
    Write-Host "Building backend..." -ForegroundColor Cyan
    Push-Location "src\InvestmentApp.Api"
    try {
        dotnet clean
        dotnet build --configuration Release
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Backend build failed" -ForegroundColor Red
            exit 1
        }
        Write-Host "Backend built successfully" -ForegroundColor Green
    } finally {
        Pop-Location
    }
}

# Build frontend if not skipped
if (-not $SkipFrontend -and -not $SkipBuild) {
    Write-Host "Building frontend..." -ForegroundColor Cyan
    Push-Location "frontend"
    try {
        npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Frontend npm install failed" -ForegroundColor Red
            exit 1
        }
        Write-Host "Frontend dependencies installed" -ForegroundColor Green
    } finally {
        Pop-Location
    }
}

# Start services
$jobs = @()

# Start backend
if (-not $SkipBackend) {
    Write-Host "Starting backend API..." -ForegroundColor Cyan
    $backendJob = Start-Job -ScriptBlock {
        param($projectPath)
        Set-Location $projectPath
        dotnet run --project "src\InvestmentApp.Api\InvestmentApp.Api.csproj" --urls="http://localhost:5000"
    } -ArgumentList $PSScriptRoot

    $jobs += $backendJob
    Write-Host "Backend job started (Job ID: $($backendJob.Id))" -ForegroundColor Green

    # Simple wait for backend
    Start-Sleep -Seconds 10
    Write-Host "Backend API: http://localhost:5000" -ForegroundColor Cyan
    Write-Host "Swagger UI: http://localhost:5000/swagger" -ForegroundColor Cyan
}

# Start frontend
if (-not $SkipFrontend) {
    Write-Host "Starting frontend..." -ForegroundColor Cyan
    $frontendJob = Start-Job -ScriptBlock {
        param($projectPath)
        Set-Location "$projectPath\frontend"
        npx ng serve --port 4200 --host 0.0.0.0
    } -ArgumentList $PSScriptRoot

    $jobs += $frontendJob
    Write-Host "Frontend job started (Job ID: $($frontendJob.Id))" -ForegroundColor Green

    # Simple wait for frontend
    Start-Sleep -Seconds 15
    Write-Host "Frontend: http://localhost:4200" -ForegroundColor Cyan
}

# Display status
Write-Host ""
Write-Host "Investment Mate v2 is running!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Yellow

if (-not $SkipBackend) {
    Write-Host "Backend API: http://localhost:5000" -ForegroundColor Cyan
    Write-Host "API Docs:    http://localhost:5000/swagger" -ForegroundColor Cyan
}

if (-not $SkipFrontend) {
    Write-Host "Frontend:    http://localhost:4200" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Press Ctrl+C to stop all services" -ForegroundColor Yellow
Write-Host "Use -SkipBuild to skip building, -SkipBackend or -SkipFrontend to skip specific services" -ForegroundColor Gray

# Wait for user input to stop
try {
    Write-Host ""
    Write-Host "Press any key to stop services..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
} finally {
    Write-Host ""
    Write-Host "Stopping services..." -ForegroundColor Yellow

    # Stop all jobs
    foreach ($job in $jobs) {
        Write-Host "Stopping job $($job.Id)..." -ForegroundColor Gray
        Stop-Job -Job $job -ErrorAction SilentlyContinue
        Remove-Job -Job $job -ErrorAction SilentlyContinue
    }

    Write-Host "All services stopped" -ForegroundColor Green
}