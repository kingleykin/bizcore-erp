# Bizcore ERP - Test Execution Script
# Runs tests, generates Junit XML and HTML reports

$testProject = "src/Tests/Bizcore.ApiTests/Bizcore.ApiTests.csproj"
$resultsDir = "TestResults"

Write-Host "Starting Bizcore ERP Integration Tests..." -ForegroundColor Cyan

# Ensure results directory exists
if (!(Test-Path $resultsDir)) { New-Item -ItemType Directory -Path $resultsDir }

# Run tests with multiple loggers
# 1. html: For human-friendly viewing
# 2. junit: For CI/CD integration
dotnet test $testProject `
    --configuration Release `
    --logger "html;LogFileName=test-report.html" `
    --logger "junit;LogFileName=test-report.xml" `
    --results-directory $resultsDir `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    /p:CoverletOutput="../../$resultsDir/coverage.xml"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Tests passed successfully!" -ForegroundColor Green
    Write-Host "Report generated: $resultsDir/test-report.html" -ForegroundColor Yellow
} else {
    Write-Host "Tests failed. Please check the report." -ForegroundColor Red
}
