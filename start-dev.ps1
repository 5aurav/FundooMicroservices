# Fundoo Microservices - Development Startup Script
# Starts all 4 services in separate windows (User, Notes, Notification, Gateway)
# PostgreSQL must be running. Kafka is optional (disabled by default).

Write-Host "Starting Fundoo Microservices..." -ForegroundColor Cyan

$root = $PSScriptRoot

function Start-Service($name, $projectPath, $profile = "https") {
    $fullPath = Join-Path $root $projectPath
    Write-Host "  Starting $name on profile '$profile'..." -ForegroundColor Green
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$fullPath'; dotnet run --launch-profile $profile"
}

# Start services - each in its own terminal window
Start-Service "Notification API  (port 7003)" "src/Services/Notification/Fundoo.Notification.Api"
Start-Sleep -Seconds 2

Start-Service "User API          (port 7001)" "src/Services/User/Fundoo.User.Api"
Start-Sleep -Seconds 2

Start-Service "Notes API         (port 7002)" "src/Services/Notes/Fundoo.Notes.Api"
Start-Sleep -Seconds 2

Start-Service "API Gateway       (port 7000)" "src/ApiGateway/Fundoo.ApiGateway"

Write-Host ""
Write-Host "All services starting. Wait a few seconds for startup, then open:" -ForegroundColor Yellow
Write-Host "  Gateway + Swagger:    https://localhost:7000/swagger" -ForegroundColor White
Write-Host "  User API Swagger:     https://localhost:7001/swagger" -ForegroundColor White
Write-Host "  Notes API Swagger:    https://localhost:7002/swagger" -ForegroundColor White
Write-Host "  Notification Swagger: https://localhost:7003/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Health checks:" -ForegroundColor Yellow
Write-Host "  Gateway: https://localhost:7000/health" -ForegroundColor White
Write-Host "  User:    https://localhost:7001/health/ready" -ForegroundColor White
Write-Host "  Notes:   https://localhost:7002/health/ready" -ForegroundColor White
Write-Host "  Notif:   https://localhost:7003/health/ready" -ForegroundColor White
