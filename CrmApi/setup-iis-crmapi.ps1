# Setup IIS for CRM API on Port 80 with /CrmAPI path
# This script must be run as Administrator

Import-Module WebAdministration

# Configuration
$appPoolName = "CrmApiPool"
$siteName = "Default Web Site"
$appName = "CrmAPI"
$physicalPath = "d:\_DELETE_\CRMUI\CrmApi\publish"

Write-Host "Setting up IIS for CRM API on port 80..." -ForegroundColor Cyan

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

# Create Application Pool if it doesn't exist
if (!(Test-Path "IIS:\AppPools\$appPoolName")) {
    Write-Host "Creating Application Pool: $appPoolName" -ForegroundColor Green
    New-WebAppPool -Name $appPoolName
    Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "managedRuntimeVersion" -Value ""
    Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "enable32BitAppOnWin64" -Value $false
} else {
    Write-Host "Application Pool '$appPoolName' already exists" -ForegroundColor Yellow
}

# Remove old standalone site if it exists
if (Test-Path "IIS:\Sites\CrmApi") {
    Write-Host "Removing old standalone site: CrmApi" -ForegroundColor Yellow
    Remove-Website -Name "CrmApi"
}

# Remove existing application if it exists
if (Test-Path "IIS:\Sites\$siteName\$appName") {
    Write-Host "Removing existing application: $appName" -ForegroundColor Yellow
    Remove-WebApplication -Name $appName -Site $siteName
}

# Ensure Default Web Site exists and is running
if (!(Test-Path "IIS:\Sites\$siteName")) {
    Write-Host "ERROR: Default Web Site does not exist!" -ForegroundColor Red
    Write-Host "Creating Default Web Site..." -ForegroundColor Yellow
    New-Website -Name $siteName -PhysicalPath "C:\inetpub\wwwroot" -Port 80
}

# Start Default Web Site if it's stopped
$site = Get-Website -Name $siteName
if ($site.State -ne "Started") {
    Write-Host "Starting Default Web Site..." -ForegroundColor Yellow
    Start-Website -Name $siteName
}

# Create the application under Default Web Site
Write-Host "Creating Application: /$appName under $siteName" -ForegroundColor Green
New-WebApplication -Name $appName -Site $siteName -PhysicalPath $physicalPath -ApplicationPool $appPoolName -Force

# Create logs directory
$logsPath = Join-Path $physicalPath "logs"
if (!(Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
    Write-Host "Created logs directory: $logsPath" -ForegroundColor Green
}

# Set permissions for IIS_IUSRS
Write-Host "Setting permissions for IIS_IUSRS..." -ForegroundColor Green
$acl = Get-Acl $physicalPath
$permission = "IIS_IUSRS", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl $physicalPath $acl

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "IIS Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Application Pool: $appPoolName" -ForegroundColor White
Write-Host "Site: $siteName" -ForegroundColor White
Write-Host "Application: /$appName" -ForegroundColor White
Write-Host "Physical Path: $physicalPath" -ForegroundColor White
Write-Host "URL: http://localhost/$appName" -ForegroundColor White
Write-Host "Swagger UI: http://localhost/$appName/swagger" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test the site
Write-Host "Testing the site..." -ForegroundColor Cyan
Start-Sleep -Seconds 3
try {
    $response = Invoke-WebRequest -Uri "http://localhost/$appName/swagger/v1/swagger.json" -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "Site is responding successfully!" -ForegroundColor Green
    }
} catch {
    Write-Host "Warning: Site may need a moment to start up" -ForegroundColor Yellow
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "You can now access your API at: http://localhost/$appName" -ForegroundColor Cyan
Write-Host "Swagger UI: http://localhost/$appName/swagger" -ForegroundColor Cyan
