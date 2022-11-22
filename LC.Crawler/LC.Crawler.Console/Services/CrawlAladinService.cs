using System.Collections.Concurrent;
using Dasync.Collections;
using LC.Crawler.Client.Entities;
using LC.Crawler.Client.Enums;
using LC.Crawler.Console.Services.Helper;
using LC.Crawler.Core.Enums;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace LC.Crawler.Console.Services;

public class CrawlAladinService : CrawlLCEcommerceBaseService
{
    protected override async Task<CrawlEcommercePayload> GetCrawlEcommercePayload(IPage page, string url)
    {
        var crawlAladinPayload   = await GetCrawlAladinPayload(page, url);

        return crawlAladinPayload;
    }

    protected override async Task<ConcurrentBag<CrawlEcommerceProductPayload>> GetCrawlEcommerceProductPayload(CrawlEcommercePayload crawlEcommercePayload)
    {
        var aladinProducts        = new ConcurrentBag<CrawlEcommerceProductPayload>();
        var productUrls           = crawlEcommercePayload.Products.Select(_ => _.Url).Distinct().ToList();

        await productUrls.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async urls =>
        {
            var products = await GetCrawlAladinProducts(urls.ToList());

            aladinProducts.AddRange(products);
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return aladinProducts;
    }

    private async Task<List<CrawlEcommerceProductPayload>> GetCrawlAladinProducts(List<string> productUrls)
    {
        var             products              = new List<CrawlEcommerceProductPayload>();
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0, 
                                                                       string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
        
        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                foreach (var productUrl in productUrls)
                {
                    var productPage = await browserContext.BrowserContext.NewPageAsync();
                    await productPage.UnloadResource();
                    try
                    {
                        System.Console.WriteLine($"CRAWL PRODUCT: {productUrl}");
                        var itemResult = await DoCrawl(productPage, new CrawlModelBase { Url = productUrl });
                        var product = itemResult.Posts.First().CrawlEcommercePayload.Products.First();
                        products.Add(product);
                        System.Console.WriteLine(JsonConvert.SerializeObject(product));
                    }
                    catch (Exception e)
                    {
                        await e.Log(string.Empty, string.Empty);
                    }
                    finally
                    {
                        await productPage.CloseAsync();
                    }
                }
            }
        }

        return products;
    }

    private async Task<CrawlEcommercePayload> GetCrawlAladinPayload(IPage page, string mainDomainUrl)
    {
        var categoryUrls      = await GetCategoryUrls(page);
        var ecommercePayload  = new CrawlEcommercePayload
        {
            Products = new List<CrawlEcommerceProductPayload>()
        };
        
        foreach (var categoryUrl in categoryUrls)
        {
            var menuItemUrl = $"{mainDomainUrl}{categoryUrl}";
            var pageNumber  = 1;
            var products    = new List<CrawlEcommerceProductPayload>();
            
            System.Console.WriteLine($"CRAWL CHECKING CATE: {menuItemUrl}");

            while (true)
            {
                await page.GotoAsync($"{menuItemUrl}?page={pageNumber}");
                System.Console.WriteLine($"CRAWL CHECKING PAGE: {pageNumber}");

                var productElements = await page.QuerySelectorAllAsync("//div[@class='pro-item']/a[1]");
                if (!productElements.Any())
                {
                    break;
                }

                foreach (var productElement in productElements)
                {
                    var productHref = await productElement.GetAttributeAsync("href");
                    products.Add(new CrawlEcommerceProductPayload { Url = $"{mainDomainUrl}{productHref}" });
                }

                pageNumber++;
            }

            ecommercePayload.Products.AddRange(products);
            System.Console.WriteLine($"CRAWL FOUND CATE: {menuItemUrl}: {products.Count}");
        }

        return ecommercePayload;
    }

    private async Task<List<string>> GetCategoryUrls(IPage page)
    {
        var ele_MenuItems = await page.QuerySelectorAllAsync("//ul[@id='menu-menu']//li");
        var urls = new List<string>();
        
        foreach (var ele_MenuItem in ele_MenuItems)
        {
            var ele_MainMenu =
                await ele_MenuItem.QuerySelectorAsync(
                    "//a[not(text() = 'Shop by Brand') and not(text()='Mỹ phẩm thiên nhiên') and not(text()='Mỹ phẩm organic') and not(text()='Mỹ phẩm spa') and not(text()='Mỹ phẩm nam') and not(text()='Thiết bị sức khoẻ') and boolean(@id)]");
            if (ele_MainMenu is not null)
            {
                var menuItemHref = await ele_MainMenu.GetAttributeAsync("href");
                if (menuItemHref is not null)
                {
                    urls.Add(menuItemHref);
                }
                
                var ele_SubMenus = await ele_MenuItem.QuerySelectorAllAsync("//div[@class='item-sub2']/a");
                if (ele_SubMenus.IsNotNullOrEmpty())
                {
                    foreach (var ele_SubMenu in ele_SubMenus)
                    {
                        var subHref = await ele_SubMenu.GetAttributeAsync("href");
                        urls.Add(subHref);
                    }
                }
            }
        }

        return urls;
    }
    
    private async Task<CrawlResult> DoCrawl(IPage page, CrawlModelBase crawlItem)
    {
        await page.GotoAsync(crawlItem.Url);

        var product = new CrawlEcommerceProductPayload() { Url = crawlItem.Url };

        #region Product info

        var ele_Categories = await page.QuerySelectorAllAsync("//div[@id='content']/nav//a[not(text() = 'Trang chủ')]");
        var categoryName     = string.Empty;
        if (ele_Categories.Any())
        {
            foreach (var ele_Category in ele_Categories)
            {
                if (categoryName.IsNotNullOrEmpty())
                {
                    categoryName += " -> ";
                }

                categoryName += await ele_Category.InnerTextAsync();
            }
        }

        if (categoryName.IsNotNullOrEmpty())
        {
            product.Category = categoryName;
        }

        var ele_ProductId = await page.QuerySelectorAsync("//div[@id='content']//div[contains(@class,'summary')]//p[contains(text(),'Mã sản phẩm')]/span");
        if (ele_ProductId is not null)
        {
            product.Code = await ele_ProductId.InnerTextAsync();
        }

        var ele_ProductTitle = await page.QuerySelectorAsync("//h1[@class='product_title entry-title']");
        if (ele_ProductTitle is not null)
        {
            product.Title = await ele_ProductTitle.InnerTextAsync();
            product.Title = product.Title.Replace("\n", string.Empty).Trim();
        }
        
        var ele_ShortDescription = await page.QuerySelectorAsync("//div[@id='content']//div[@itemprop='description']");
        if (ele_ShortDescription is not null)
        {
            var shortDescription = await ele_ShortDescription.InnerTextAsync();
            product.ShortDescription = shortDescription.RemoveHrefFromA();
        }

        var ele_Image = await page.QuerySelectorAsync("//div[@id='content']//a[@id='Zoomer']/img");
        if (ele_Image is not null)
        {
            var imageUrl = await ele_Image.GetAttributeAsync("src");
            product.ImageUrls = new List<string> { imageUrl };
        }

        var ele_Description = await page.QuerySelectorAsync("//div[@id='content']//div[@id='tab-additional_information']");
        if (ele_Description is not null)
        {
            var description = await ele_Description.InnerHTMLAsync();
            product.Description = description.RemoveHrefFromA();
        }
        
        var ele_Comments = await page.QuerySelectorAllAsync("//div[@id='comment-list']//div[contains(@class, 'approved-1')]");
        if (ele_Comments.Any())
        {
            product.Comments = await GetProductComments(ele_Comments);;
        }
        
        var ele_Brand = await page.QuerySelectorAsync("//p[contains(text(),'Thương hiệu')]/a");
        if (ele_Brand is not null)
        {
            var brand = await ele_Brand.InnerTextAsync();
            product.Brand = brand;
        }

        var ele_Tags = await page.QuerySelectorAllAsync("//div[@id='tags']/p[contains(text(),'Tags:')]/a");
        foreach (var ele_tag in ele_Tags)
        {
            product.Tags.Add(await ele_tag.InnerTextAsync());
        }

        #endregion
        
        #region Product attributes

        var manufacturerLabel = "Nhà sản xuất";
        var anotherManufacturerLabel = "Hãng sản xuất";
        var ele_Manufacturer = await page.QuerySelectorAsync($"//p/strong[contains(text(),'{manufacturerLabel}') or contains(text(),'{anotherManufacturerLabel}')]/..") ??
                               await page.QuerySelectorAsync($"//p/strong/span[contains(text(),'{manufacturerLabel}') or contains(text(),'{anotherManufacturerLabel}')]/../..");
        if (ele_Manufacturer is not null)
        {
            var manufacturer = await ele_Manufacturer.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute()
            {
                Key   = manufacturerLabel,
                Value = manufacturer.Replace($"{manufacturerLabel}:", string.Empty).Replace($"{manufacturerLabel} :", string.Empty)
                    .Replace($"{anotherManufacturerLabel}:", string.Empty).Replace($"{anotherManufacturerLabel} :", string.Empty)
            });
        }

        var originLabel = "Xuất xứ";
        var ele_Origin = await page.QuerySelectorAsync($"//p/strong[contains(text(),'{originLabel}')]/..") ??
                         await page.QuerySelectorAsync($"//p/strong/span[contains(text(),'{originLabel}')]/../..");
        if (ele_Origin is not null)
        {
            var origin = await ele_Origin.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute()
            {
                Key   = originLabel,
                Value = origin.Replace($"{originLabel}:", string.Empty).Replace($"{originLabel} :", string.Empty)
            });
        }

        var noteLabel = "Lưu ý";
        var ele_Note  = await page.QuerySelectorAsync($"//div/b[contains(text(),'{noteLabel}')]/..");
        if (ele_Note is not null)
        {
            var note = await ele_Note.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute()
            {
                Key   = noteLabel,
                Value = note.Replace($"{noteLabel}:", string.Empty).Replace($"{noteLabel} :", string.Empty)
            });
        }

        var usageFormLabel = "Đóng gói";
        var ele_UsageForm = await page.QuerySelectorAsync($"//p/strong[contains(text(),'{usageFormLabel}')]/..") ??
                            await page.QuerySelectorAsync($"//p/strong/span[contains(text(),'{usageFormLabel}')]/../..");
        if (ele_UsageForm is not null)
        {
            var usageForm = await ele_UsageForm.InnerTextAsync();
            
            if (usageForm == "Đóng gói")
            {
                ele_UsageForm = await page.QuerySelectorAsync($"//p/strong[contains(text(),'{usageFormLabel}')]/../following-sibling::p[1]");
                if (ele_UsageForm is not null)
                {
                    usageForm = await ele_UsageForm.InnerTextAsync();
                    product.Attributes.Add(new EcommerceProductAttribute()
                    {
                        Key   = usageFormLabel,
                        Value = usageForm
                    });
                }
            }
            else
            {
                product.Attributes.Add(new EcommerceProductAttribute()
                {
                    Key   = usageFormLabel,
                    Value = usageForm.Replace($"{usageFormLabel}:", string.Empty).Replace($"{usageFormLabel} :", string.Empty)
                });
            }
            
        }
        #endregion

        #region Product variants
        var variant = new EcommerceProductVariant
        { 
            SKU          = product.Code
        };
        
        await GetPrices(page, variant, product);

        #endregion

        var crawlResult  = new CrawlResult { DataSourceType         = DataSourceType.Website, SourceType = SourceType.LC };
        var crawlPayload = new CrawlPayload { CrawlEcommercePayload = new CrawlEcommercePayload() { Products = new List<CrawlEcommerceProductPayload> { product } } };

        crawlResult.Posts.Add(crawlPayload);

        return crawlResult;
    }

    private static async Task GetPrices(IPage page, EcommerceProductVariant variant, CrawlEcommerceProductPayload product)
    {
        var crawlPricePayload = await AladinHelper.CrawlPrice(page);
        variant.DiscountedPrice = crawlPricePayload.DiscountedPrice;
        variant.DiscountRate = crawlPricePayload.DiscountRate;
        variant.RetailPrice = crawlPricePayload.RetailPrice;

        product.Variants.Add(variant);
    }

    private static async Task<List<EcommerceProductComment>> GetProductComments(IReadOnlyList<IElementHandle> ele_Comments)
    {
        var comments = new List<EcommerceProductComment>();
        foreach (var ele_Comment in ele_Comments)
        {
            var fullComment       = await ele_Comment.InnerTextAsync();
            var ele_AuthorComment = await ele_Comment.QuerySelectorAsync("//div[@class='comment-name']");
            var authorComment     = ele_AuthorComment is not null ? await ele_AuthorComment.InnerTextAsync() : string.Empty;
            var ele_InfoFeedback  = await ele_Comment.QuerySelectorAsync("//div[@class='info_feeback']");
            var infoFeedback      = ele_InfoFeedback is not null ? await ele_InfoFeedback.InnerTextAsync() : string.Empty;
            var contentComment    = fullComment.Replace(authorComment, string.Empty).Replace(infoFeedback, string.Empty);
            var ele_LikeComment   = await ele_Comment.QuerySelectorAsync("//span[@id='comment_like_']");
            var likeComment       = 0;
            if (ele_LikeComment is not null)
            {
                likeComment = (await ele_LikeComment.InnerTextAsync()).Replace("(", string.Empty).Replace(")", string.Empty).ToIntOrDefault();
            }

            comments.Add(new EcommerceProductComment() { Content = contentComment, Likes = likeComment, Name = authorComment });
        }

        return comments;
    }
}