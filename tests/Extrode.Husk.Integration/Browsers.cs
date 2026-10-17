using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Safari;

namespace Extrode.Husk.Integration;

/// <summary>WebDriver sessions. Selenium Manager finds or downloads the driver (and Chrome for Testing
/// when no Chrome is installed); Safari needs `sudo safaridriver --enable` once per machine.</summary>
public static class Browsers
{
    public static TheoryData<string> All => OperatingSystem.IsMacOS() ? ["chrome", "firefox", "safari"] : ["chrome", "firefox"];

    public static IWebDriver Start(string name)
    {
        IWebDriver driver = name switch
        {
            "chrome" => new ChromeDriver(Chrome()),
            "firefox" => new FirefoxDriver(Firefox()),
            "safari" => new SafariDriver(),
            _ => throw new ArgumentException(name, nameof(name)),
        };
        driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(10);
        return driver;
    }

    private static ChromeOptions Chrome()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.SetLoggingPreference(LogType.Browser, LogLevel.All);
        return options;
    }

    private static FirefoxOptions Firefox()
    {
        var options = new FirefoxOptions();
        options.AddArgument("-headless");
        return options;
    }

    /// <summary>Waits until the script returns a non-null value, or fails with the last one.</summary>
    public static object Until(IWebDriver driver, string script, TimeSpan? timeout = null)
    {
        var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            var value = ((IJavaScriptExecutor)driver).ExecuteScript(script);
            if (value is not null)
                return value;
            if (DateTime.UtcNow > until)
                throw new TimeoutException($"no value from: {script}");
            Thread.Sleep(50);
        }
    }
}
