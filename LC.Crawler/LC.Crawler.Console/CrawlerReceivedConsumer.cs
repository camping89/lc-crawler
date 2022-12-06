using System.Reflection;
using LC.Crawler.Client.Entities;
using LC.Crawler.Client.Enums;
using LC.Crawler.Console.Services;
using LC.Crawler.Core.Enums;
using LC.Crawler.Core.Helpers;
using log4net;
using log4net.Config;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace LC.Crawler.Console;

public class CrawlerReceivedConsumer : IDistributedEventHandler<CrawlerDataSourceEto>, ITransientDependency
{
    
    private readonly CrawlLCService _crawlLcService;
    private readonly IDistributedEventBus _distributedEventBus;
    
    private static readonly ILog Logger = LogManager.GetLogger(typeof(CrawlerReceivedConsumer));

    public CrawlerReceivedConsumer( IDistributedEventBus distributedEventBus, CrawlLCService crawlLcService)
    {
        
        _distributedEventBus = distributedEventBus;
        _crawlLcService = crawlLcService;
    }

    public async Task HandleEventAsync(CrawlerDataSourceEto eventDataSourceEto)
    {
        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
        GlobalContext.Properties["fname"] = "CrawlerMQ";
        XmlConfigurator.Configure(logRepository, new FileInfo("Configurations/log4net.config"));
        Logger.Info($"MQ ID = {eventDataSourceEto.Id} - Created At = {eventDataSourceEto.CreatedAtUtc}");
        System.Console.WriteLine($"MQ ID = {eventDataSourceEto.Id} - Created At = {eventDataSourceEto.CreatedAtUtc}");
        foreach (var item in eventDataSourceEto.Items)
        {
            try
            {
                CrawlResult? crawlResult = null;
                // update Credential after finished all data sources
                var isLastDataSource = eventDataSourceEto.Items.LastOrDefault() == item;

                await PerformCrawlLC(eventDataSourceEto, item, isLastDataSource);

                if (crawlResult is null) continue;

                if (item.SourceType == SourceType.LC) continue;
                
                CrawlResultEto eto;
                if (crawlResult.Status is CrawlStatus.OK)
                {
                    eto = GetEto(crawlResult, eventDataSourceEto.Credential, isLastDataSource);
                    await _distributedEventBus.PublishAsync(eto);
                }
                else if (crawlResult.Status is CrawlStatus.AccountBanned or CrawlStatus.BlockedTemporary or CrawlStatus.LoginApprovalNeeded or CrawlStatus.UnknownFailure)
                {
                    // Account is Banned, blocked, Login Approval => update Credential and stop crawling
                    eto = GetEto(crawlResult, eventDataSourceEto.Credential, true);
                    await _distributedEventBus.PublishAsync(eto);
                    break;
                }
            }
            catch (Exception e)
            {
                await e.Log(eventDataSourceEto.Credential.CrawlerAccount.Username, $"{eventDataSourceEto.Credential.CrawlerAccount.Username}/{item.Url}");
            }
        }
    }

    

    private CrawlResultEto GetEto(CrawlResult crawlResult, CrawlerCredentialEto credential, bool shouldStop)
    {
        var eto = new CrawlResultEto
        {
            DataSourceType = crawlResult.DataSourceType, SourceType = crawlResult.SourceType, Items = crawlResult.Posts, EcommercePayloads = crawlResult.CrawlEcommercePayload,
            ArticlePayloads = crawlResult.CrawlArticlePayload
        };
        if (shouldStop is false) return eto;

        if (credential?.CrawlerCredential == null) return eto;
        
        credential.CrawlerCredential.CrawledAt = DateTime.UtcNow;
        eto.Credential = credential;

        return eto;
    }

    #region LC Crawler

    private async Task<CrawlResult?> CrawlLC(ICrawlLCService instance, CrawlerDataSourceItem item, CrawlerCredentialEto credential)
    {
        var crawlResult = await _crawlLcService.Execute(instance, item, credential);
        return crawlResult;
    }
    
    private async Task PerformCrawlLC(CrawlerDataSourceEto eventDataSourceEto, CrawlerDataSourceItem item, bool isLastDataSource)
    {
        var instances = GetInstances(item.Url);
        foreach (var instance in instances)
        {
            var crawlResult = await CrawlLC(instance, item, eventDataSourceEto.Credential);
            if (crawlResult is not null)
            {
                var eto = GetEto(crawlResult, eventDataSourceEto.Credential, isLastDataSource);
                await _distributedEventBus.PublishAsync(eto);
                
                await using var file = File.CreateText($"{instance.GetType().Name}_{DateTime.UtcNow:dd_MM_yyyy}.json");
                var serializer = new JsonSerializer();
                //serialize object directly into file stream
                serializer.Serialize(file, eto);
            }
        }
    }
    
    private List<ICrawlLCService> GetInstances(string url)
    {
        if (url.Contains("sieuthisongkhoe"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlSieuThiSongKhoeService(),
                new CrawlSieuThiSongKhoeArticleService()
            };
        }

        if (url.Contains("aladin.com.vn"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlAladinArticleService(),
                new CrawlAladinService()
            };
        }

        if (url.Contains("suckhoedoisong.vn"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlSucKhoeDoiSongService()
            };
        }

        if (url.Contains("nhathuoclongchau.com"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlLongChauArticleService(),
                new CrawlLongChauService()
            };
        }

        if (url.Contains("alobacsi.com"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlAloBacSiService()
            };
        }

        if (url.Contains("blogsuckhoe.com"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlBlogSucKhoeService()
            };
        }

        if (url.Contains("suckhoegiadinh.com.vn"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlSucKhoeGiaDinhService()
            };
        }

        if (url.Contains("songkhoe.medplus.vn"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlSongKhoeMedPlusService()
            };
        }

        return new List<ICrawlLCService>();
    }

    #endregion

    
}