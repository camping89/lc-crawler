using LC.Crawler.Client.Enums;
using LC.Crawler.Core.Enums;

namespace LC.Crawler.Client.Entities
{
    public class CrawlResult
    {
        public CrawlResult()
        {
            Success = true;
            Posts = new List<CrawlPayload>();
            Status = CrawlStatus.OK;
        }

        public CrawlResult(CrawlStatus status)
        {
            Posts = new List<CrawlPayload>();
            Status = status;
            Success = status is CrawlStatus.OK or CrawlStatus.PostUnavailable;
        }
        
        public bool Success { get; set; }
        public CrawlStatus Status { get; set; }
        public List<CrawlPayload> Posts { get; set; }
        
        public DataSourceType DataSourceType { get; set; }
        public SourceType SourceType { get; set; }

        public CrawlEcommercePayload? CrawlEcommercePayload { get; set; }
        
        public CrawlArticlePayload? CrawlArticlePayload { get; set; }
    }
}