# Start CRM API in Mock Mode
# This script configures the API to use local mock data

# Stop any existing dotnet processes running the API
Write-Host "Checking for existing API processes..." -ForegroundColor Cyan
$existingProcesses = Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*dotnet*" }
if ($existingProcesses) {
    Write-Host "Stopping existing API process(es)..." -ForegroundColor Yellow
    Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "✓ Previous processes stopped" -ForegroundColor Green
}

Write-Host "Configuring CRM API for Mock Mode..." -ForegroundColor Cyan

# Update appsettings.Development.json to use mock mode
$devSettingsPath = Join-Path $PSScriptRoot "appsettings.Development.json"
$devSettings = Get-Content $devSettingsPath -Raw | ConvertFrom-Json
$devSettings.UseMockData = $true
$devSettings | ConvertTo-Json -Depth 10 | Set-Content $devSettingsPath

Write-Host "✓ Configuration updated: UseMockData = true" -ForegroundColor Green
Write-Host "Starting API in Mock Mode (using local mock data)..." -ForegroundColor Cyan
Write-Host "API will be available at: http://localhost:5051" -ForegroundColor Yellow
Write-Host "Swagger UI: http://localhost:5051/CRMApi/swagger" -ForegroundColor Yellow
Write-Host ""

# Start the application
dotnet run
