using Bizcore.E2ETests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Bizcore.E2ETests.Tests;

public class InvoiceFlowTests : E2ETestBase
{
    [Fact]
    public async Task CreateInvoice_ShouldShowInList()
    {
        // Arrange
        await LoginAsync("admin", "Admin@123");
        var customerName = $"Test Customer {Guid.NewGuid().ToString().Substring(0, 8)}";
        var amount = "1500.50";

        // Act
        await _page.ClickAsync("text=Hóa đơn");
        await _page.ClickAsync("text=Tạo hóa đơn mới");
        
        // Find input by label or proximity since they are in a modal
        await _page.FillAsync("label:has-text('Tên khách hàng') + input", customerName);
        await _page.FillAsync("label:has-text('Số tiền') + input", amount);
        await _page.ClickAsync("button:has-text('Xác nhận tạo')");

        // Assert
        await _page.WaitForSelectorAsync($"text={customerName}");
        var row = _page.Locator("tr", new PageLocatorOptions { HasText = customerName });
        (await row.IsVisibleAsync()).Should().BeTrue();
        
        var amountCell = row.Locator("td", new LocatorLocatorOptions { HasText = "1500.5" });
        (await amountCell.IsVisibleAsync()).Should().BeTrue();
    }
}
