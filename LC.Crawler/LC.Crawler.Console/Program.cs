// See https://aka.ms/new-console-template for more information

using LC.Crawler.Client.Entities;
using LC.Crawler.Console.Services;
using LC.Crawler.Core.Enums;
using LC.Crawler.Core.Playwrights;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Veek.DataProvider.Crawler.Console;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace LC.Crawler.Console;

static class Program
{
    static async Task Main(string[] args)
    {
        // Rabbit MQ

        using var application = AbpApplicationFactory.Create<CrawlerModule>(options => { options.UseAutofac(); });
        await application.InitializeAsync();
        var autoResetEvent = new AutoResetEvent(false);
        autoResetEvent.WaitOne();


        // DEBUG code
        // var crawlFaireService = new CrawlLongChauService();
        // var crawlResult = await crawlFaireService.Execute(new CrawlerDataSourceItem
        // {
        //     DataSourceType = DataSourceType.Website,
        //     Url = "https://nhathuoclongchau.com.vn"
        // }, new CrawlerCredentialEto
        // {
        //     CrawlerAccount = new CrawlerAccount
        //     {
        //         Email = "samuel.lim@reebonz.com",
        //         Password = "samtcsp7bdq",
        //         Cookies = new List<CrawlerAccountCookie>()
        //     }
        // });
        // await using var file = File.CreateText(@"C:\path.txt");
        // var serializer = new JsonSerializer();
        // //serialize object directly into file stream
        // serializer.Serialize(file, crawlResult.CrawlArticlePayload);
        
        // using var application = AbpApplicationFactory.Create<CrawlerModule>(options => { options.UseAutofac(); });
        // application.Initialize();
        //
        // var messagingService = application
        //     .ServiceProvider
        //     .GetRequiredService<App1MessagingService>();
        //
        // await messagingService.RunAsync();
        //
        // application.Shutdown();

        
        // var crawlFaireService = new CrawlFaireService();
        // await crawlFaireService.Execute(new CrawlerDataSourceItem
        // {
        //     DataSourceType = DataSourceType.Website,
        //     Url = "https://www.faire.com/brand/b_1kujzy6gls"
        // }, new CrawlerCredentialEto
        // {
        //     CrawlerAccount = new CrawlerAccount
        //     {
        //         Email = "samuel.lim@reebonz.com",
        //         Password = "tcsp7bdq",
        //         Cookies = new List<CrawlerAccountCookie>()
        //         
        //     }
        // });
    }
}

// public class App1MessagingService : ITransientDependency
// {
//     private readonly IDistributedEventBus _distributedEventBus;
//
//     public App1MessagingService(IDistributedEventBus distributedEventBus)
//     {
//         _distributedEventBus = distributedEventBus;
//     }
//
//     public async Task RunAsync()
//     {
//         ICrawlLCService crawlFaireService = new CrawlAloBacSiService();
//         var crawlResult = await crawlFaireService.Execute(new CrawlerDataSourceItem
//         {
//             DataSourceType = DataSourceType.Website,
//             Url = "https://alobacsi.com"
//         }, new CrawlerCredentialEto
//         {
//             CrawlerAccount = new CrawlerAccount
//             {
//                 Email = "samuel.lim@reebonz.com",
//                 Password = "samtcsp7bdq",
//                 Cookies = new List<CrawlerAccountCookie>()
//             }
//         });
//
//         var eto = GetEto(crawlResult, null, true);
//         await _distributedEventBus.PublishAsync(eto);
//         
//         
//         await using var file = File.CreateText($"{crawlFaireService.GetType().Name}_{DateTime.UtcNow:dd_MM_yyyy}.json");
//         var serializer = new JsonSerializer();
//         //serialize object directly into file stream
//         serializer.Serialize(file, eto);
//     }
//
//     private CrawlResultEto GetEto(CrawlResult crawlResult, CrawlerCredentialEto credential, bool shouldStop)
//     {
//         var eto = new CrawlResultEto
//         {
//             DataSourceType = crawlResult.DataSourceType, SourceType = crawlResult.SourceType, Items = crawlResult.Posts, EcommercePayloads = crawlResult.CrawlEcommercePayload,
//             ArticlePayloads = crawlResult.CrawlArticlePayload
//         };
//         if (shouldStop is false) return eto;
//
//         if (credential?.CrawlerCredential == null) return eto;
//
//         credential.CrawlerCredential.CrawledAt = DateTime.UtcNow;
//         eto.Credential = credential;
//
//         return eto;
//     }
// }