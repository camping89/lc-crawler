using LC.Crawler.Client.Entities;

namespace LC.Crawler.Console.Services.CrawlPriceServices;

public interface ICrawlPriceService
{
    Task<CrawlPriceResult> Execute(List<CrawlPriceDataSourceItem> items);
}