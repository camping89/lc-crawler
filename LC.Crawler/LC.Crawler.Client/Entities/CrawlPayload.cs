namespace LC.Crawler.Client.Entities
{
    public class CrawlPayload
    {
        #region LC

        public CrawlEcommercePayload CrawlEcommercePayload { get; set; }
        public CrawlArticlePayload   CrawlArticlePayload   { get; set; }

        #endregion
    }
}