using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services.Helper;

public static class LongChauHelper
{
    public static async Task<CrawlPricePayload> CrawlPrice(IPage page)
    {
        CrawlPricePayload crawlPricePayload = new CrawlPricePayload();
        var ele_Price = await page.QuerySelectorAsync("//div[@class='prices']");
        if (ele_Price is not null)
        {
            var ele_SalePrice = await ele_Price.QuerySelectorAsync("//div[@class='sale-price']/span[1]");
            var value = (await ele_SalePrice.InnerTextAsync()).Trim().Trim('đ').Replace(".", string.Empty);
            crawlPricePayload.RetailPrice = value.ToDecimalOrDefault();
            System.Console.WriteLine($"---------------Price: {crawlPricePayload.RetailPrice}");

            var ele_OriginalPrice = await ele_Price.QuerySelectorAsync("//div[@class='original-price']/span[1]");
            if (ele_OriginalPrice is not null)
            {
                var originalValue = (await ele_OriginalPrice.InnerTextAsync()).Trim().Trim('đ').Replace(".", string.Empty);
                crawlPricePayload.DiscountedPrice = crawlPricePayload.RetailPrice;
                crawlPricePayload.RetailPrice = originalValue.ToDecimalOrDefault();
                
                crawlPricePayload.DiscountRate = crawlPricePayload.RetailPrice == 0
                    ? 0
                    : (1 - (crawlPricePayload.DiscountedPrice / crawlPricePayload.RetailPrice));
            }
        }

        return crawlPricePayload;
    }
}