using System.Collections.Concurrent;
using FluentDate;
using FluentDateTime;
using LC.Crawler.Client.Entities;
using LC.Crawler.Client.Enums;
using LC.Crawler.Core.Enums;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services;

public abstract class CrawlLCArticleBaseService : BaseService, ICrawlLCService
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
    
    public async Task<CrawlResult?> Execute(CrawlerDataSourceItem item, CrawlerCredentialEto credential)
    {
        InitLogConfig(credential);
        if (item.Url.Contains("nhathuoclongchau"))
        {
            var crawlerProxy = GetCrawlProxy();
            credential.CrawlerProxy = crawlerProxy;
            CrawlerProxy = crawlerProxy;
        }
        
        var             browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, credential.CrawlerProxy.Ip, 
            credential.CrawlerProxy.Port, 
            credential.CrawlerProxy.Username, credential.CrawlerProxy.Password, new List<CrawlerAccountCookie>(), false);
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

            await homePage.GotoAsync(item.Url, new PageGotoOptions{Timeout = 300000});
            await homePage.Wait(3000);
            
            var crawlArticlePayload = await GetCrawlArticlePayload(homePage, item.Url);
            
            await homePage.CloseAsync();
            
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
            await browserContext.BrowserContext.CloseAsync();
        }
    }
    
    protected virtual async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        return await Task.Factory.StartNew(() => new CrawlArticlePayload());
    }
    
    protected virtual async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        return await Task.Factory.StartNew(() => new  ConcurrentBag<ArticlePayload>());
    }
}