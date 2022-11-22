using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services.Helper;

public static class LongChauHelper
{
    public static async Task<CrawlPricePayload> CrawlPrice(IPage page)
    {
        CrawlPricePayload crawlPricePayload = new CrawlPricePayload();
        var ele_Price = await page.QuerySelectorAsync("//*[@id='price_default']");
        if (ele_Price is not null)
        {
            var value = await ele_Price.GetAttributeAsync("value");
            if (value is not null) crawlPricePayload.RetailPrice = value.ToDecimalOrDefault();
            System.Console.WriteLine($"---------------Price: {crawlPricePayload.RetailPrice}");

            var ele_Discount = await page.QuerySelectorAsync("//div[contains(@class,'pcd-price')]//strike[boolean(@style='display:none') = false]");
            if (ele_Discount is not null)
            {
                var ele_DiscountPrice =
                    await page.QuerySelectorAsync(
                        "//div[contains(@class,'pcd-price')]//span[contains(@class,'detailFinalPrice')]");
                if (ele_DiscountPrice is not null)
                {
                    var discountPriceText = await ele_DiscountPrice.InnerTextAsync();
                    var discountPrice = discountPriceText.Trim().Trim('đ').Replace(".", string.Empty).ToDecimalOrDefault();
                    crawlPricePayload.DiscountedPrice = discountPrice;
                    crawlPricePayload.DiscountRate = crawlPricePayload.RetailPrice == 0
                        ? 0
                        : (1 - (discountPrice / crawlPricePayload.RetailPrice));
                }
            }
        }

        return crawlPricePayload;
    }
}