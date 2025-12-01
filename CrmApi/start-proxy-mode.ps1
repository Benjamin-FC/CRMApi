# Start CRM API in Proxy Mode
# This script configures the API to forward requests to the Azure endpoint

# Stop any existing dotnet processes running the API
Write-Host "Checking for existing API processes..." -ForegroundColor Cyan
$existingProcesses = Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*dotnet*" }
if ($existingProcesses) {
    Write-Host "Stopping existing API process(es)..." -ForegroundColor Yellow
    Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "✓ Previous processes stopped" -ForegroundColor Green
}

Write-Host "Configuring CRM API for Proxy Mode..." -ForegroundColor Cyan

# Update appsettings.Development.json to use proxy mode
$devSettingsPath = Join-Path $PSScriptRoot "appsettings.Development.json"
$devSettings = Get-Content $devSettingsPath -Raw | ConvertFrom-Json
$devSettings.UseMockData = $false
$devSettings | ConvertTo-Json -Depth 10 | Set-Content $devSettingsPath

Write-Host "✓ Configuration updated: UseMockData = false" -ForegroundColor Green
Write-Host "Starting API in Proxy Mode (forwarding to Azure)..." -ForegroundColor Cyan
Write-Host "API will be available at: http://localhost:5051" -ForegroundColor Yellow
Write-Host "Swagger UI: http://localhost:5051/CRMApi/swagger" -ForegroundColor Yellow
Write-Host ""

# Start the application
dotnet run
