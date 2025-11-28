# Instructions to install ASP.NET Core Hosting Bundle
# Run this script as Administrator AFTER downloading the hosting bundle
# Working version 10:14AM 

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ASP.NET Core Hosting Bundle Installation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Step 1: Download the .NET 10 Hosting Bundle" -ForegroundColor Yellow
Write-Host "Visit: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor White
Write-Host "Download: 'Hosting Bundle' for Windows" -ForegroundColor White
Write-Host ""
Write-Host "Step 2: Run the installer" -ForegroundColor Yellow
Write-Host ""
Write-Host "Step 3: After installation, run this script to restart IIS" -ForegroundColor Yellow
Write-Host ""

$response = Read-Host "Have you installed the Hosting Bundle? (Y/N)"
if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host "Restarting IIS..." -ForegroundColor Green
    net stop was /y
    net start w3svc
    Write-Host ""
    Write-Host "IIS restarted successfully!" -ForegroundColor Green
    Write-Host "Your application should now work at: http://localhost/CRMApi" -ForegroundColor Cyan
} else {
    Write-Host "Please install the Hosting Bundle first, then run this script again." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray