using LC.Crawler.Client.Entities;
using LC.Crawler.Client.Enums;
using LC.Crawler.Core.Enums;
using Volo.Abp.EventBus;

namespace LC.Crawler.Console;

[EventName("Veek.DataProvider.Social.CrawlerDataSourceEto")]
public class CrawlerDataSourceEto
{
    public List<CrawlerDataSourceItem> Items { get; set; }
    public CrawlerCredentialEto Credential { get; set; }
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CrawlerDataSourceItem
{
    public string         Url            { get; set; }
    public SourceType     SourceType     { get; set; }
    public DataSourceType DataSourceType { get; set; }
    public DateTime StopDateTime { get; set; }
    public string SourceId { get; set; }
}


