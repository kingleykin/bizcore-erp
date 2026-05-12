# Bizcore ERP - Professional Test & Coverage Report
# Runs tests, generates Cobertura Coverage and Premium HTML Dashboard

$testProject = "src/Tests/Bizcore.ApiTests/Bizcore.ApiTests.csproj"
$resultsDir = "TestResults"
$reportDir = "$resultsDir/HtmlReport"

Write-Host "Đang bắt đầu chạy Integration Tests cho Bizcore ERP..." -ForegroundColor Cyan

# Ensure results directory is clean
if (Test-Path $resultsDir) { Remove-Item -Recurse -Force $resultsDir }
New-Item -ItemType Directory -Path $resultsDir

# Run tests
dotnet test $testProject `
    --configuration Release `
    --logger "html;LogFileName=test-report-simple.html" `
    --logger "junit;LogFileName=test-report.xml" `
    --results-directory $resultsDir `
    --collect:"XPlat Code Coverage"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Kiểm thử thành công! Đang khởi tạo Dashboard báo cáo..." -ForegroundColor Green
    
    # Find the generated cobertura file
    $coverageFile = Get-ChildItem -Path $resultsDir -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1
    
    if ($coverageFile) {
        # Generate Premium Report with Vietnamese Title
        dotnet tool run reportgenerator `
            -reports:"$($coverageFile.FullName)" `
            -targetdir:"$reportDir" `
            -reporttypes:"HtmlInline_AzurePipelines" `
            -title:"Bizcore ERP - Báo cáo Kiểm thử & Độ bao phủ mã nguồn"

        Write-Host "`n"
        Write-Host "==========================================================" -ForegroundColor Cyan
        Write-Host " HOÀN THÀNH: TẤT CẢ KIỂM THỬ ĐỀU VƯỢT QUA " -ForegroundColor Green -BackgroundColor Black
        Write-Host "==========================================================" -ForegroundColor Cyan
        Write-Host "1. Dashboard độ bao phủ code: $reportDir/index.html" -ForegroundColor Yellow
        Write-Host "2. Chi tiết từng Test Case  : $resultsDir/test-report-simple.html" -ForegroundColor Yellow
        Write-Host "==========================================================" -ForegroundColor Cyan
    }
} else {
    Write-Host "`n"
    Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
    Write-Host " THẤT BẠI: CÓ TEST CASE KHÔNG VƯỢT QUA " -ForegroundColor White -BackgroundColor Red
    Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
    Write-Host "Vui lòng kiểm tra báo cáo chi tiết: $resultsDir/test-report-simple.html" -ForegroundColor Yellow
}
