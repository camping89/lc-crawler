using LC.Crawler.Core.Enums;

namespace LC.Crawler.Client.Entities;

public class CrawlPriceResult
{
    public CrawlPriceResult()
    {
        Success = true;
        ProductPricePayloads = new List<CrawlPricePayload>();
        Status = CrawlStatus.OK;
    }

    public CrawlPriceResult(CrawlStatus status)
    {
        ProductPricePayloads = new List<CrawlPricePayload>();
        Status = status;
        Success = status is CrawlStatus.OK or CrawlStatus.PostUnavailable;
    }
    
    public bool Success { get; set; }
    public CrawlStatus Status { get; set; }
    public List<CrawlPricePayload> ProductPricePayloads { get; set; }
}