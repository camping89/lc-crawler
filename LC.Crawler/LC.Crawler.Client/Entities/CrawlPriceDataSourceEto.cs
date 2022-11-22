using Volo.Abp.EventBus;

namespace LC.Crawler.Client.Entities;

[EventName("Veek.DataProvider.Social.CrawlPriceDataSourceEto")]
public class CrawlPriceDataSourceEto
{
    public List<CrawlPriceDataSourceItem> CrawlPriceDataSourceItems { get; set; }
}

public class CrawlPriceDataSourceItem
{
    public string Url { get; set; }
    public string ProductUrl { get; set; }
}
