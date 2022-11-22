using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services.Helper;

public class SieuThiSongKhoeHelper
{
    public static async Task<CrawlPricePayload> CrawlPrice(IPage page)
    {
        var crawlPricePayload = new CrawlPricePayload();
        var ele_Price     = await page.QuerySelectorAsync("//form[@class='cart cart-form']//span[contains(@class,'item-price-row')]/del//span[@class='woocommerce-Price-amount amount']/bdi"); // done
        var xPath_SalePrice = ele_Price is not null ? "//form[@class='cart cart-form']//span[contains(@class,'item-price-row')]/ins//span[@class='woocommerce-Price-amount amount']/bdi" 
            : "//form[@class='cart cart-form']//span[contains(@class,'item-price-row')]//span[@class='woocommerce-Price-amount amount']/bdi";
        var ele_SalePrice = await page.QuerySelectorAsync(xPath_SalePrice);
        var ele_Currency =
            await page.QuerySelectorAsync("//form[@class='cart cart-form']//span[@class='woocommerce-Price-amount amount']/bdi/span[@class='woocommerce-Price-currencySymbol']");
        
        if (ele_Price is not null || ele_SalePrice is not null)
        {
            var currency  = ele_Currency is not null ? await ele_Currency.InnerTextAsync() : "đ";
            var rootPrice = await (ele_Price ?? ele_SalePrice)?.InnerTextAsync()!;
            if (rootPrice.IsNotNullOrEmpty())
            {
                crawlPricePayload.RetailPrice = rootPrice.Replace(".", string.Empty).Replace(currency, string.Empty).ToDecimalOrDefault();
            }

            if (ele_Price is not null && ele_SalePrice is not null)
            {
                var oldPrice        = (await ele_Price.InnerTextAsync()).Replace(".", string.Empty).Replace(currency, string.Empty).ToDecimalOrDefault();
                var newPrice        = (await ele_SalePrice.InnerTextAsync()).Replace(".", string.Empty).Replace(currency, string.Empty).ToDecimalOrDefault();
                
                crawlPricePayload.DiscountedPrice = newPrice;
                var discountPercent = (100 - ((newPrice * 100) / oldPrice)) / 100;
                if (discountPercent != 0)
                {
                    crawlPricePayload.DiscountRate = discountPercent;
                }
            }
        }

        return crawlPricePayload;
    }
}