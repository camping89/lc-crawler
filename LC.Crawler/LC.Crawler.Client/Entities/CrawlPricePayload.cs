namespace LC.Crawler.Client.Entities;

public class CrawlPricePayload
{
    public string Url { get; set; }
    public decimal RetailPrice     { get; set; }

    public decimal DiscountRate { get; set; }

    public decimal DiscountedPrice { get; set; }
    public string ProductUrl { get; set; }
    
}