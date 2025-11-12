# OrderFlow servislerini durdurma scripti

Write-Host "OrderFlow servislerini durduruluyor..." -ForegroundColor Yellow

# Tüm OrderFlow process'lerini bul ve durdur
$processes = Get-Process | Where-Object {
    $_.ProcessName -like "*OrderFlow*" -or 
    $_.Path -like "*OrderFlow*"
}

if ($processes) {
    foreach ($proc in $processes) {
        Write-Host "Durduruluyor: $($proc.ProcessName) (ID: $($proc.Id))" -ForegroundColor Red
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Tüm servisler durduruldu!" -ForegroundColor Green
} else {
    Write-Host "Çalışan OrderFlow servisi bulunamadı." -ForegroundColor Cyan
}

# Process kontrolü - TEK PROCESS GARANTİSİ
Write-Host "`nProcess kontrolü (TEK PROCESS GARANTİSİ):" -ForegroundColor Yellow
$paymentProcesses = Get-Process | Where-Object {
    $_.ProcessName -like "*PaymentService*" -or 
    $_.Path -like "*PaymentService*"
}
if ($paymentProcesses) {
    Write-Host "UYARI: PaymentService process'leri bulundu:" -ForegroundColor Red
    $paymentProcesses | Format-Table ProcessName, Id, StartTime -AutoSize
} else {
    Write-Host "PaymentService process'i yok - TEMİZ" -ForegroundColor Green
}

# Port kontrolü
Write-Host "`nPort kontrolü:" -ForegroundColor Yellow
$ports = @(5131, 5034)
foreach ($port in $ports) {
    $connections = netstat -ano | findstr ":$port" | findstr "LISTENING"
    if ($connections) {
        Write-Host "Port $port hala kullanımda!" -ForegroundColor Red
        $connections | ForEach-Object {
            Write-Host "  $_" -ForegroundColor Gray
        }
    } else {
        Write-Host "Port $port boş" -ForegroundColor Green
    }
}

# RabbitMQ temizliği (opsiyonel - eski connection'ları temizlemek için)
Write-Host "`nRabbitMQ temizliği:" -ForegroundColor Yellow
Start-Sleep -Seconds 2  # Connection'ların kapanması için bekle
$queueName = "paymentservice-ordercreated"
$deleteResult = docker exec orderflow-rabbit rabbitmqctl delete_queue $queueName 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "Queue '$queueName' silindi" -ForegroundColor Green
} else {
    Write-Host "Queue '$queueName' bulunamadı veya zaten silinmiş" -ForegroundColor Cyan
}
Write-Host "RabbitMQ connection'ları otomatik kapanacak (3-5 saniye bekle)" -ForegroundColor Cyan

