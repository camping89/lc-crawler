using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services.Helper;

public class AladinHelper
{
    public static async Task<CrawlPricePayload> CrawlPrice(IPage page)
    {
        var crawlPricePayload = new CrawlPricePayload();
        var ele_Price = await page.QuerySelectorAsync("//div[@id='content']//div[@itemprop='offers']//del/span[@class='amount']");
        var ele_SalePrice = await page.QuerySelectorAsync("//div[@id='content']//div[@itemprop='offers']//span[@itemprop='price']");
        var ele_Discount = await page.QuerySelectorAsync("//div[@id='content']//div[@itemprop='offers']//span[@class='aladin-price']/span[@class='pre']");
        if (ele_Discount is not null)
        {
            crawlPricePayload.DiscountRate = (await ele_Discount.InnerTextAsync()).Replace("%", "").ToDecimalOrDefault() / 100;
            if (ele_Price is not null)
            {
                crawlPricePayload.RetailPrice = (await ele_Price.InnerTextAsync()).Replace(".", "").ToDecimalOrDefault();
            }

            if (ele_SalePrice is not null)
            {
                crawlPricePayload.DiscountedPrice = (await ele_SalePrice.InnerTextAsync()).Replace(".", "").ToDecimalOrDefault();
            }
        }
        else
        {
            if (ele_Price is not null)
            {
                crawlPricePayload.RetailPrice = (await ele_Price.InnerTextAsync()).Replace(".", "").ToDecimalOrDefault();
            }
            else if (ele_SalePrice is not null)
            {
                crawlPricePayload.RetailPrice = (await ele_SalePrice.InnerTextAsync()).Replace(".", "").ToDecimalOrDefault();
            }
        }

        return crawlPricePayload;
    }
}