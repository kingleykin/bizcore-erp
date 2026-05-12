#!/bin/bash

# Bizcore ERP - Professional Test & Coverage Report (macOS/Linux)
# Runs tests, generates Cobertura Coverage and Premium HTML Dashboard

TEST_PROJECT="src/Tests/Bizcore.ApiTests/Bizcore.ApiTests.csproj"
RESULTS_DIR="TestResults"
REPORT_DIR="$RESULTS_DIR/HtmlReport"

echo -e "\033[0;36mĐang bắt đầu chạy Integration Tests cho Bizcore ERP...\033[0m"

# Ensure results directory is clean
rm -rf "$RESULTS_DIR"
mkdir -p "$RESULTS_DIR"

# Run tests
dotnet test "$TEST_PROJECT" \
    --configuration Release \
    --logger "html;LogFileName=test-report-simple.html" \
    --logger "junit;LogFileName=test-report.xml" \
    --results-directory "$RESULTS_DIR" \
    --collect:"XPlat Code Coverage"

EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo -e "\033[0;32mKiểm thử thành công! Đang khởi tạo Dashboard báo cáo...\033[0m"
    
    # Find the generated cobertura file
    COVERAGE_FILE=$(find "$RESULTS_DIR" -name "coverage.cobertura.xml" | head -n 1)
    
    if [ -n "$COVERAGE_FILE" ]; then
        # Generate Premium Report with Vietnamese Title
        dotnet tool run reportgenerator \
            -reports:"$COVERAGE_FILE" \
            -targetdir:"$REPORT_DIR" \
            -reporttypes:"HtmlInline_AzurePipelines" \
            -title:"Bizcore ERP - Báo cáo Kiểm thử & Độ bao phủ mã nguồn"

        echo -e "\n"
        echo -e "\033[0;36m==========================================================\033[0m"
        echo -e "\033[1;32m HOÀN THÀNH: TẤT CẢ KIỂM THỬ ĐỀU VƯỢT QUA \033[0m"
        echo -e "\033[0;36m==========================================================\033[0m"
        echo -e "\033[0;33m1. Dashboard độ bao phủ code: $REPORT_DIR/index.html\033[0m"
        echo -e "\033[0;33m2. Chi tiết từng Test Case  : $RESULTS_DIR/test-report-simple.html\033[0m"
        echo -e "\033[0;36m==========================================================\033[0m"
    fi
else
    echo -e "\n"
    echo -e "\033[0;31m!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!\033[0m"
    echo -e "\033[1;37;41m THẤT BẠI: CÓ TEST CASE KHÔNG VƯỢT QUA \033[0m"
    echo -e "\033[0;31m!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!\033[0m"
    echo -e "\033[0;33mVui lòng kiểm tra báo cáo chi tiết: $RESULTS_DIR/test-report-simple.html\033[0m"
fi

exit $EXIT_CODE
