using Microsoft.Playwright;

namespace Bizcore.E2ETests.Infrastructure;

public abstract class E2ETestBase : IAsyncLifetime
{
    protected IPlaywright _playwright = default!;
    protected IBrowser _browser = default!;
    protected IBrowserContext _context = default!;
    protected IPage _page = default!;

    protected virtual string BaseUrl => Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5173";

    public virtual async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });
        
        _page = await _context.NewPageAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    protected async Task LoginAsync(string username, string password)
    {
        await _page.GotoAsync($"{BaseUrl}/login");
        await _page.FillAsync("input[name='username']", username);
        await _page.FillAsync("input[name='password']", password);
        await _page.ClickAsync("button[type='submit']");
        await _page.WaitForURLAsync(url => url != $"{BaseUrl}/login");
    }
}
