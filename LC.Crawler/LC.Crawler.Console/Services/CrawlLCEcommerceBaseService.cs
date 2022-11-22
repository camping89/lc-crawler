using System.Collections.Concurrent;
using LC.Crawler.Client.Entities;
using LC.Crawler.Client.Enums;
using LC.Crawler.Core.Enums;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services;

public abstract class CrawlLCEcommerceBaseService : BaseService, ICrawlLCService
{
    public async Task<CrawlResult?> Execute(CrawlerDataSourceItem item, CrawlerCredentialEto credential)
    {
        InitLogConfig(credential);
        
        var             browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0, 
            string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
        await using var browser        = browserContext.Browser;
        try
        {
            var crawlResult = new CrawlResult(CrawlStatus.OK);

            if (StringExtensions.IsNullOrEmpty(item.Url)) return new CrawlResult();

            System.Console.WriteLine($"====================={GetType().Name}: Trying to CRAWL url {item.Url}");

            var homePage = await browserContext.BrowserContext.NewPageAsync();
            await homePage.UnloadResource();
            if (!item.Url.Contains("https://"))
            {
                item.Url = $"https://{item.Url}";
            }

            await homePage.GotoAsync(item.Url);
            await homePage.Wait(3000);

            var crawlEcommercePayload = await GetCrawlEcommercePayload(homePage, item.Url);
            
            await homePage.CloseAsync();

            var crawlEcommerceProductPayload = await GetCrawlEcommerceProductPayload(crawlEcommercePayload);
            crawlEcommercePayload.Url = item.Url;
            crawlEcommercePayload.Products = crawlEcommerceProductPayload.ToList();
            
            
            crawlResult.CrawlEcommercePayload = crawlEcommercePayload;
            crawlResult.DataSourceType        = DataSourceType.Website;
            crawlResult.SourceType            = SourceType.LC;

            return crawlResult;

        }
        catch (Exception e)
        {
            System.Console.WriteLine($"CRAWL ERROR ====================================================================");
            System.Console.WriteLine($"{GetType().Name}: CRAWL ERROR {item.Url}");
            System.Console.WriteLine($"{e.Message}");
            System.Console.WriteLine($"================================================================================");
            await e.Log(string.Empty, $"{item.Url}");
            return new CrawlResult(CrawlStatus.UnknownFailure);
        }
        finally
        {
            System.Console.WriteLine("CRAWL FINISHED. CLOSE BROWSER IN 1 SECs");
            await Task.Delay(1000);
            await browserContext.BrowserContext.CloseAsync();
        }
    }

    protected virtual async Task<CrawlEcommercePayload> GetCrawlEcommercePayload(IPage page, string url)
    {
        return await Task.Factory.StartNew(() => new CrawlEcommercePayload());
    }

    protected virtual async Task<ConcurrentBag<CrawlEcommerceProductPayload>> GetCrawlEcommerceProductPayload(CrawlEcommercePayload crawlEcommercePayload)
    {
        return await Task.Factory.StartNew(() => new  ConcurrentBag<CrawlEcommerceProductPayload>());
    }
}