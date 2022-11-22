using Volo.Abp.EventBus;

namespace LC.Crawler.Client.Entities;

[EventName("Veek.DataProvider.Social.CrawlPriceResultEto")]
public class CrawlPriceResultEto
{
    public List<CrawlPricePayload> PricePayload { get; set; }
}