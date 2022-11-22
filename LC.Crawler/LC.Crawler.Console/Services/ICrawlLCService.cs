using LC.Crawler.Client.Entities;

namespace LC.Crawler.Console.Services;

public interface ICrawlLCService
{
    Task<CrawlResult?> Execute(CrawlerDataSourceItem item, CrawlerCredentialEto credential);
}