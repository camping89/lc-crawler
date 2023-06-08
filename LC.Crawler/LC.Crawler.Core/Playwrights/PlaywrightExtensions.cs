using System.Diagnostics;
using LC.Crawler.Core.Extensions;
using Microsoft.Playwright;

namespace LC.Crawler.Core.Playwrights
{
    public static class PlaywrightExtensions
    {
        /// <summary>
        /// Wait for a selector. Can be DOM selector or xpath start with xpath=
        /// </summary>
        /// <param name="page"></param>
        /// <param name="selector"></param>
        /// <param name="state"></param>
        /// <param name="timeoutInMs"></param>
        /// <returns> true if selector exists, false if not</returns>
        public static async Task<bool> Wait(this IPage page, string selector, WaitForSelectorState state = WaitForSelectorState.Visible, int timeoutInMs = 250)
        {
            try
            {
                await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                {
                    State = state,
                    Timeout = timeoutInMs
                });
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }

        public static async Task HoverAndWait(this IPage page, IElementHandle elementHandle, string waitSelector, int timeoutInMiliseconds = 1500)
        {
            await elementHandle.HoverAsync();
            await page.WaitASecond();
            await page.Wait(waitSelector, WaitForSelectorState.Visible, timeoutInMiliseconds);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        }

        public static async Task WaitLoad(this IPage page, LoadState loadState = LoadState.NetworkIdle)
        {
            await page.WaitForLoadStateAsync(loadState);
        }

        public static async Task Wait(this IPage page, int timeoutInMiliseconds = 1000)
        {
            await page.WaitForTimeoutAsync(timeoutInMiliseconds);
        }

        public static async Task WaitASecond(this IPage page)
        {
            await page.Wait(1000);
        }

        public static async Task WaitMillisecond(this IPage page, int millisecond)
        {
            await page.Wait(millisecond);
        }

        public static async Task WaitHalfSecond(this IPage page)
        {
            await page.Wait(500);
        }

        public static async Task WaitQuaterSecond(this IPage page)
        {
            await page.Wait(250);
        }

        public static async Task<byte[]> Screenshot(this IPage page, string name)
        {
            if (StringExtensions.IsNullOrEmpty(name)) name = string.Empty;

            var path = $"/Screenshots/{name}-{DateTime.UtcNow:yyyy.MM.ddThh:mm:ss}";
            return await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Jpeg,
                Path = path,
                Quality = 100,
                // FullPage = true,
            });
        }

        public static async Task<bool> Click(this IPage page, string selector)
        {
            try
            {
                var clickableEle = await page.QuerySelectorAsync(selector);
                if (clickableEle != null)
                {
                    await page.DispatchEventAsync(selector, "click");
                    await page.WaitQuaterSecond();
                    // await page.ClickAsync(selector, new PageClickOptions
                    // {
                    //     Timeout = 3000
                    // });
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (PlaywrightException pwe)
            {
                if (pwe.Message.Contains("Element is not visible") || pwe.Message.Contains("destroy"))
                {
                    await page.DispatchEventAsync(selector, "click");
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public static async Task<bool> Click(this IElementHandle element, params KeyboardModifier[] keyboardModifiers )
        {
            try
            {
                await element.ClickAsync(new ElementHandleClickOptions
                {
                    Force = true,
                    Timeout = 3000,
                    Modifiers = keyboardModifiers
                });

                return true;
            }
            catch (PlaywrightException pwe)
            {
                if (pwe.Message.Contains("Element is not visible") || pwe.Message.Contains("destroy"))
                {
                    await element.DispatchEventAsync("click");
                    return true;
                }

                throw pwe;
            }
        }

        public static async Task WaitForSelector(this IPage page, string selector, int timeout = 10000)
        {
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
            {
                Timeout = timeout
            });
        }
    }

    public static class PlaywrightHelper
    {
        private const string ExtensionPath = @"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Extensions\cfhdojbkjhnklbpkdaibdccddilifddb";

        private static readonly List<string> AdsRegex = new()
        {
            "wprp.zemanta.com",
            "google",
            "facebook",
            "adi.admicro.vn",
            "geo.dailymotion.com",
            "doubleclick.net",
            "googleapis.com",
            "fbcdn.net",
            "http://tyhuu.com/doc-ngoc"
        };
        
        public static async Task<PlaywrightContext> InitBrowser(string userDataDirRoot, string proxyIp, int proxyPort, string proxyUserName, string proxyPassword, List<CrawlerAccountCookie> crawlerAccountCookies, 
             bool headless = true)
        {
            CreateTempFolder();
            var loadExtensionPath = Environment.ExpandEnvironmentVariables(ExtensionPath);
            loadExtensionPath = Directory.GetDirectories(loadExtensionPath).First();
            var browserTypeLaunchOptions = new BrowserTypeLaunchOptions
            {
                Channel = "chrome",
                Timeout = 0,
                Headless = headless,
                // https://peter.sh/experiments/chromium-command-line-switches/
                Args = new List<string>
                {
                    "--start-maximized",
                    "--ignore-ssl-errors=yes",
                    "--ignore-certificate-errors",
                    "--disable-web-security",
                    //"--allow-running-insecure-content",
                    "--disable-blink-features=AutomationControlled",
                    // "--auto-open-devtools-for-tabs",
                    "--disable-popup-blocking",
                    "--log-level=3",
                    "--disable-notifications",
                    "--unhandled-rejections=strict",
                    $"--load-extension={loadExtensionPath}"
                },
                IgnoreDefaultArgs = new List<string>
                {
                    "--enable-automation",
                    "--disable-extensions"
                },
                // SlowMo = 100,
                DownloadsPath = userDataDirRoot
            };

            if (proxyIp.IsNotNullOrWhiteSpace() && proxyPort != 0 && proxyUserName.IsNotNullOrWhiteSpace() && proxyPassword.IsNotNullOrWhiteSpace())
            {
                browserTypeLaunchOptions.Proxy = new Proxy
                {
                    Server = $"{proxyIp}:{proxyPort}",
                    Username = proxyUserName,
                    Password = proxyPassword
                };
            }

            var playwright = await Playwright.CreateAsync();
            
            var browser = await playwright.Chromium.LaunchAsync(browserTypeLaunchOptions);

            var browserContextOptions = new BrowserNewContextOptions
            {
                Locale = "en-US",
                ViewportSize = ViewportSize.NoViewport
            };
            var browserContext = await browser.NewContextAsync(browserContextOptions);
            
            var cookies = new List<Cookie>();
            foreach (var crawlerAccountCookie in crawlerAccountCookies)
            {
                cookies.Add(new Cookie
                {
                    Domain = crawlerAccountCookie.Domain,
                    Expires = crawlerAccountCookie.Expires,
                    HttpOnly = crawlerAccountCookie.HttpOnly,
                    Name = crawlerAccountCookie.Name,
                    Path = crawlerAccountCookie.Path,
                    Secure = crawlerAccountCookie.Secure,
                    Value = crawlerAccountCookie.Value
                });
            }

            await browserContext.AddCookiesAsync(cookies);
            
            return new PlaywrightContext
            {
                Playwright = playwright,
                Browser = browser,
                BrowserContext = browserContext
            };
        }

        public static async Task UnloadResource(this IPage page)
        {
            await page.RouteAsync("**/*",
                async route =>{
                    if (AdsRegex.Any(route.Request.Url.Contains))
                    {
                        await route.AbortAsync();
                    }
                    else
                    {
                        await route.ContinueAsync();
                    }
                });
            await page.RouteAsync("**/*.{png,jpg,gif,bmp,tiff,svg,webp}", route => route.AbortAsync());
        }

        private static void CreateTempFolder()
        {
            var list = 10.ToList();
            foreach (var i in list)
            {
                var path = Path.Combine(Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Temp"), i.ToString());
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
        }
    }
}