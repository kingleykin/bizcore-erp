#!/bin/bash

# Bizcore ERP - Test Execution Script (macOS/Linux)
# Runs tests, generates Junit XML and HTML reports

TEST_PROJECT="src/Tests/Bizcore.ApiTests/Bizcore.ApiTests.csproj"
RESULTS_DIR="TestResults"

echo -e "\033[0;36mStarting Bizcore ERP Integration Tests...\033[0m"

# Ensure results directory exists
mkdir -p "$RESULTS_DIR"

# Run tests with multiple loggers
# 1. html: For human-friendly viewing
# 2. junit: For CI/CD integration
dotnet test "$TEST_PROJECT" \
    --configuration Release \
    --logger "html;LogFileName=test-report.html" \
    --logger "junit;LogFileName=test-report.xml" \
    --results-directory "$RESULTS_DIR" \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput="../../$RESULTS_DIR/coverage.xml"

EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo -e "\033[0;32mTests passed successfully!\033[0m"
    echo -e "\033[0;33mReport generated: $RESULTS_DIR/test-report.html\033[0m"
else
    echo -e "\033[0;31mTests failed. Please check the report.\033[0m"
fi

exit $EXIT_CODE
