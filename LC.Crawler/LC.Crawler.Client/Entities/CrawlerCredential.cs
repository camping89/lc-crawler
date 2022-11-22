using LC.Crawler.Core.Enums;

namespace LC.Crawler.Client.Entities;

public class CrawlerCredential
{
    public Guid Id { get; set; }
    public virtual DataSourceType DataSourceType { get; set; }
    public Guid? CrawlerAccountId { get; set; }
    public Guid? CrawlerProxyId { get; set; }
        
    public DateTime? CrawledAt { get; set; }
    public bool IsAvailable { get; set; }
    public string ConcurrencyStamp { get; set; }
}