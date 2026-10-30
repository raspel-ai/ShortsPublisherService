using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace ShortsPublisher;

/// <summary>
/// Incremental Facebook UI automation. Only opens Facebook and its Profile tab.
/// No media upload or publication is performed.
/// </summary>
public sealed class FacebookPublisher(PublisherOptions options) : IDisposable
{
    private AndroidDriver? _driver;
    private readonly PublisherOptions _options = options;
    private const string FacebookPackage = "com.facebook.katana";

    public Task<FacebookPreparationResult> PreparePublicationAsync(
        string? filePath,
        string? caption,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.Error.WriteLine("[facebook] app.open: connecting to Facebook");
        Connect();
        _driver!.ActivateApp(FacebookPackage);
        Console.Error.WriteLine("[facebook] app.open: ready");

        Console.Error.WriteLine("[facebook] profile.open: locating bottom Profile tab");
        var deadline = DateTime.UtcNow.AddSeconds(20);
        IWebElement? profileTab = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            profileTab = _driver.FindElements(By.XPath(
                "//*[@class='android.app.ActionBar$Tab' and starts-with(@content-desc,'Profile, tab ')]"))
                .FirstOrDefault(element => element.Displayed && element.Enabled);
            if (profileTab is not null) break;
            Thread.Sleep(500);
        }

        if (profileTab is null)
            throw new InvalidOperationException(
                "Facebook bottom Profile tab was not found. No publication attempted.");

        profileTab.Click();
        Console.Error.WriteLine("[facebook] profile.open: tapped Profile tab");

        var verifyDeadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < verifyDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = _driver.FindElements(By.XPath(
                "//*[@class='android.app.ActionBar$Tab' and starts-with(@content-desc,'Profile, tab ') and @selected='true']"));
            if (selected.Count > 0)
            {
                Console.Error.WriteLine("[facebook] profile.verify: Profile tab selected");
                return Task.FromResult(new FacebookPreparationResult(
                    "facebook_profile_opened",
                    "Facebook Profile tab opened. No video was published."));
            }
            Thread.Sleep(500);
        }

        throw new InvalidOperationException(
            "Facebook Profile tab selection could not be confirmed. No publication attempted.");
    }

    private void Connect()
    {
        if (_driver is not null) return;
        var caps = new AppiumOptions();
        caps.PlatformName = "Android";
        caps.AutomationName = "UiAutomator2";
        caps.DeviceName = "Pixel 6a";
        caps.AddAdditionalAppiumOption("udid", _options.DeviceId);
        caps.AddAdditionalAppiumOption("appPackage", FacebookPackage);
        caps.AddAdditionalAppiumOption("noReset", true);
        caps.AddAdditionalAppiumOption("newCommandTimeout", 180);
        _driver = new AndroidDriver(new Uri(_options.AppiumServer), caps, TimeSpan.FromMinutes(3));
    }

    public void Dispose()
    {
        try { _driver?.Quit(); } catch { }
        _driver?.Dispose();
    }
}

public sealed record FacebookPreparationResult(string Status, string Message);
