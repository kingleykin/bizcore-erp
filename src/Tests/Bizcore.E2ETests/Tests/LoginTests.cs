using Bizcore.E2ETests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Bizcore.E2ETests.Tests;

public class LoginTests : E2ETestBase
{
    [Fact]
    public async Task Login_WithValidCredentials_ShouldNavigateToDashboard()
    {
        // Arrange & Act
        await LoginAsync("admin", "Admin@123");

        // Assert
        await _page.WaitForSelectorAsync("text=Tổng quan hệ thống");
        var title = await _page.InnerTextAsync(".title");
        title.Should().Be("Tổng quan hệ thống");
        
        var dashboardItem = _page.Locator(".nav-item.active");
        await dashboardItem.InnerTextAsync().ContinueWith(t => t.Result.Should().Contain("Dashboard"));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldShowErrorMessage()
    {
        // Arrange
        await _page.GotoAsync($"{BaseUrl}/login");

        // Act
        await _page.FillAsync("input[type='text']", "admin");
        await _page.FillAsync("input[type='password']", "WrongPassword");
        await _page.ClickAsync("button[type='submit']");

        // Assert
        var error = _page.Locator("text=Tên đăng nhập hoặc mật khẩu không đúng");
        (await error.IsVisibleAsync()).Should().BeTrue();
    }
}
