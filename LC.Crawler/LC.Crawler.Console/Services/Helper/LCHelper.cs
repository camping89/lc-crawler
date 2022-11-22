using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services.Helper;

public static class LCHelper
{
    public static async Task  Scroll(this IPage page, int height)
    {
        await page.EvaluateAsync("height => window.scrollTo(0, height)", height);
        await page.Wait();
    }
}