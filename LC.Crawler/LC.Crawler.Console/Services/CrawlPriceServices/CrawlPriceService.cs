using System.Collections.Concurrent;
using Dasync.Collections;
using LC.Crawler.Client.Entities;
using LC.Crawler.Console.Services.Helper;
using LC.Crawler.Core.Enums;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;

namespace LC.Crawler.Console.Services.CrawlPriceServices;

public class CrawlPriceService : BaseService, ICrawlPriceService
{
    public async Task<CrawlPriceResult> Execute(List<CrawlPriceDataSourceItem> items)
    {
        ConcurrentBag<CrawlPricePayload> CrawlPricePayloadResult = new ConcurrentBag<CrawlPricePayload>();
        await items.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async priceDataSourceItems =>
        {
            var             browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0, 
                string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
            await using var browser        = browserContext.Browser;
            try
            {
                var homePage = await browserContext.BrowserContext.NewPageAsync();
                await homePage.UnloadResource();
                
                var crawlPricePayloads = new List<CrawlPricePayload>();
                foreach (var crawlPriceDataSourceItem in priceDataSourceItems)
                {
                    if (!crawlPriceDataSourceItem.ProductUrl.Contains("https://"))
                    {
                        crawlPriceDataSourceItem.ProductUrl = $"https://{crawlPriceDataSourceItem.ProductUrl}";
                    }

                    await homePage.GotoAsync(crawlPriceDataSourceItem.ProductUrl);
                    await homePage.Wait(3000);
                    CrawlPricePayload crawlPricePayload = null;
                    if (crawlPriceDataSourceItem.Url.Contains("nhathuoclongchau.com"))
                    {
                        crawlPricePayload = await LongChauHelper.CrawlPrice(homePage);
                    }
                    else if (crawlPriceDataSourceItem.Url.Contains("aladin.com.vn"))
                    {
                        crawlPricePayload = await AladinHelper.CrawlPrice(homePage);
                    } 
                    else if (crawlPriceDataSourceItem.Url.Contains("sieuthisongkhoe.com"))
                    {
                        crawlPricePayload = await SieuThiSongKhoeHelper.CrawlPrice(homePage);
                    }

                    if (crawlPricePayload is not null)
                    {
                        crawlPricePayload.Url = crawlPriceDataSourceItem.Url;
                        crawlPricePayload.ProductUrl = crawlPriceDataSourceItem.ProductUrl;
                        crawlPricePayloads.Add(crawlPricePayload);
                    }
                }
                
                CrawlPricePayloadResult.AddRange(crawlPricePayloads);
            }
            catch (Exception e)
            {
                System.Console.WriteLine($"CRAWL ERROR ====================================================================");
                System.Console.WriteLine($"{GetType().Name}: CRAWL ERROR");
                System.Console.WriteLine($"{e.Message}");
                System.Console.WriteLine($"================================================================================");
                await e.Log(string.Empty, string.Empty);
            }
            finally
            {
                System.Console.WriteLine("CRAWL FINISHED. CLOSE BROWSER IN 1 SECs");
                await Task.Delay(1000);
                await browserContext.BrowserContext.CloseAsync();
            }
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return new CrawlPriceResult
        {
            ProductPricePayloads = CrawlPricePayloadResult.ToList(),
            Status = CrawlStatus.OK,
            Success = true
        };
    }
}