using LC.Crawler.Client.Enums;
using LC.Crawler.Core.Enums;
using Volo.Abp.EventBus;

namespace LC.Crawler.Client.Entities;

[EventName("Veek.DataProvider.Social.CrawlDataResultEto")]
public class CrawlResultEto
{
    public List<CrawlPayload> Items { get; set; }
    public CrawlEcommercePayload EcommercePayloads { get; set; }
    public CrawlArticlePayload ArticlePayloads { get; set; }
    public DataSourceType DataSourceType { get; set; }
    public SourceType SourceType { get; set; }
    public CrawlerCredentialEto Credential { get; set; }
}