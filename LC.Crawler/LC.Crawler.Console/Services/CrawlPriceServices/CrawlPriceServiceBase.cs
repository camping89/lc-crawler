using LC.Crawler.Client.Entities;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services.CrawlPriceServices;

public class CrawlPriceServiceBase : BaseService
{
    public virtual async Task<CrawlPricePayload> CrawlPrice(IPage page, string productUrl)
    {
        return await Task.FromResult(new CrawlPricePayload());
    }
}