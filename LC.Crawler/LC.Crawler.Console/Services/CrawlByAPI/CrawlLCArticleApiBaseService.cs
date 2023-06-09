using System.Collections.Concurrent;
using FluentDate;
using FluentDateTime;
using LC.Crawler.Client.Entities;
using LC.Crawler.Client.Enums;
using LC.Crawler.Core.Enums;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;

namespace LC.Crawler.Console.Services.CrawlByAPI;

public class CrawlLCArticleApiBaseService : BaseService, ICrawlLCService
{
    protected int GetDaysCrawlingInterval()
    {
        return GlobalConfig.CrawlConfig.DaysIntervalArticles;
    }

    protected int GetTotalCrawlingArticlesInterval()
    {
        return GlobalConfig.CrawlConfig.TotalArticles;
    }

    protected virtual async Task<bool> IsValidArticle(int totalArticle, string url, DateTime? createdAtDateTime)
    {
        return await Task.Factory.StartNew(() =>
        {
            if (GlobalConfig.CrawlConfig.ValidateArticleByDateTime)
            {
                return createdAtDateTime >= GetDaysCrawlingInterval().Days().Ago();
            }

            return totalArticle <= GetTotalCrawlingArticlesInterval();
        });
    }
    
    protected virtual async Task<bool> IsValidArticle(int totalArticle)
    {
        return await Task.Factory.StartNew(() => totalArticle <= GetTotalCrawlingArticlesInterval());
    }
    
    protected virtual async Task<bool> IsValidArticle(DateTime? createdAtDateTime)
    {
        return await Task.Factory.StartNew(() => createdAtDateTime >= GetDaysCrawlingInterval().Days().Ago());
    }
    
    public async Task<CrawlResult?> Execute(CrawlerDataSourceItem item, CrawlerCredentialEto credential)
    {
        InitLogConfig(credential);
        try
        {
            var crawlResult = new CrawlResult(CrawlStatus.OK);

            if (StringExtensions.IsNullOrEmpty(item.Url)) return new CrawlResult();

            System.Console.WriteLine($"====================={GetType().Name}: Trying to CRAWL url {item.Url}");
            var crawlArticlePayload = await GetCrawlArticlePayload(item.Url);
            
            var articlePayloads = await GetArticlePayload(crawlArticlePayload);
            crawlArticlePayload.Url = item.Url;
            crawlArticlePayload.ArticlesPayload = articlePayloads.ToList();
            
            crawlResult.CrawlArticlePayload = crawlArticlePayload;
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
        }
    }
    
    protected virtual async Task<CrawlArticlePayload> GetCrawlArticlePayload(string url)
    {
        return await Task.Factory.StartNew(() => new CrawlArticlePayload
        {
            Url = url
        });
    }
    
    protected virtual async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        return await Task.Factory.StartNew(() => new  ConcurrentBag<ArticlePayload>());
    }
}