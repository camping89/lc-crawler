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

public class CrawlSieuThiSongKhoeService : CrawlLCEcommerceBaseService
{
    protected override async Task<CrawlEcommercePayload> GetCrawlEcommercePayload(IPage page, string url)
    {
        var crawlSTSKPayload   = await GetCrawlSTSKPayload(page);
        crawlSTSKPayload.Url = url;
        
        return crawlSTSKPayload;
    }

    protected override async Task<ConcurrentBag<CrawlEcommerceProductPayload>> GetCrawlEcommerceProductPayload(CrawlEcommercePayload crawlEcommercePayload)
    {
        var stskProducts        = new ConcurrentBag<CrawlEcommerceProductPayload>();
        var productUrls           = crawlEcommercePayload.Products.Select(_ => _.Url).Distinct().ToList();

        await productUrls.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async urls =>
        {
            var products = await GetCrawlSTSKProducts(urls.ToList());
            stskProducts.AddRange(products);
        }, maxDegreeOfParallelism: GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return stskProducts;
    }

    private async Task<List<CrawlEcommerceProductPayload>> GetCrawlSTSKProducts(List<string> productUrls)
    {
        var             products       = new List<CrawlEcommerceProductPayload>();
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

    private async Task<CrawlEcommercePayload> GetCrawlSTSKPayload(IPage page)
    {
        var categoryUrls      = await GetCategoryUrls(page);
        var ecommercePayload  = new CrawlEcommercePayload
        {
            Products = new List<CrawlEcommerceProductPayload>()
        };
        
        foreach (var categoryUrl in categoryUrls)
        {
            var pageNumber  = 1;
            var products    = new List<CrawlEcommerceProductPayload>();
            
            System.Console.WriteLine($"CRAWL CHECKING CATE: {categoryUrl}");

            while (true)
            {
                await page.GotoAsync($"{categoryUrl}/page/{pageNumber}");
                System.Console.WriteLine($"CRAWL CHECKING PAGE: {pageNumber}");

                var ele_Products = await page.QuerySelectorAllAsync("//main[@id='main']//a[contains(@class,'product')]");
                if (!ele_Products.Any())
                {
                    break;
                }

                foreach (var ele_Product in ele_Products)
                {
                    var productHref = await ele_Product.GetAttributeAsync("href");
                    products.Add(new CrawlEcommerceProductPayload { Url = $"{productHref}" });
                }

                pageNumber++;
            }

            ecommercePayload.Products.AddRange(products);
            System.Console.WriteLine($"CRAWL FOUND CATE: {categoryUrl}: {products.Count}");
        }

        return ecommercePayload;
    }

    private async Task<List<string>> GetCategoryUrls(IPage page)
    {
        // Get main menu and hidden menu
        var ele_ItemMenus   = new List<IElementHandle>();
        var ele_MainMenus   = await page.QuerySelectorAllAsync("//div[@class='container']/ul/li[boolean(@class='expand-navbar')=false]/a");
        if (ele_MainMenus.Any())
        {
            ele_ItemMenus.AddRange(ele_MainMenus);
        }
        
        var ele_HiddenMenus = await page.QuerySelectorAllAsync("//div[@class='container']/ul/li/div/p/a");
        if (ele_HiddenMenus.Any())
        {
            ele_ItemMenus.AddRange(ele_HiddenMenus);
        }
        
        var menuUrls = new List<string>();
        
        foreach (var ele_ItemMenu in ele_ItemMenus)
        {
            var itemMenuHref = await ele_ItemMenu.GetAttributeAsync("href");
            if (itemMenuHref is not null)
            {
                menuUrls.Add(itemMenuHref);
            }
        }

        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/ba-bau/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/benh-gan/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/benh-phoi/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/benh-than/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/benh-tri/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/boi-bo-co-the/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/da-day-ruot/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/duong-tiet-nieu/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/giam-can/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/huyet-ap");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/mat/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/man-ngua-di-ung/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/mui-co-the/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/nao-than-kinh/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/phu-ne-sung-tay/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/rang-mieng/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/cham-soc-sac-dep/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/sinh-ly-nam/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/sinh-ly-nu/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/suy-gian-tinh-mach/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/tang-can/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/tang-chieu-cao/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/tieu-duong");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/tim-mach/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/toc/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/u-buou/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/viem-hong/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/viem-xoang/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thuc-pham-chuc-nang/xuong-khop/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/bo-qua-tang-suc-khoe");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/duoc-my-pham/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/duoc-my-pham/collagen-2/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/duoc-my-pham/nam-da/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/duoc-my-pham/trang-da/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/duoc-my-pham/tri-tham-mun/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thao-duoc/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thao-duoc/dong-trung-ha-thao");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thao-duoc/fucoidan");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thiet-bi-y-te/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thiet-bi-y-te/may-do-duong-huyet/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/thiet-bi-y-te/may-do-huyet-ap/");
        menuUrls.Add("https://sieuthisongkhoe.com/loai/tra-thao-duoc/");

        return menuUrls;
    }

    private async Task<CrawlResult> DoCrawl(IPage page, CrawlModelBase crawlItem)
    {
        await page.GotoAsync(crawlItem.Url);

        var productDetailPrefixUrl = "https://sieuthisongkhoe.com/san-pham/";
        var product                = new CrawlEcommerceProductPayload() { Url = crawlItem.Url };
        
        #region Product Info
        
        product.Code = crawlItem.Url.Replace(productDetailPrefixUrl, string.Empty).Replace("/", string.Empty);
        
        var ele_Categories = await page.QuerySelectorAllAsync("//main[@id='main']//div[@class='d-none d-lg-block']/nav/span/a[not(text() = 'Trang Chủ') and not(text()='Sản Phẩm')]");
        var categoryName   = string.Empty;
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
        
        var ele_ProductTitle = await page.QuerySelectorAsync("//main[@id='main']//h1");
        if (ele_ProductTitle is not null)
        {
            product.Title = await ele_ProductTitle.InnerTextAsync();
            product.Title = product.Title.Replace("\n", string.Empty).Trim();
        }

        var ele_Images = await page.QuerySelectorAllAsync("//ul[@role='tablist']/li/img");
        if (ele_Images.Any())
        {
            product.ImageUrls = new List<string>();
            foreach (var ele_Image in ele_Images)
            {
                var imageUrl = await ele_Image.GetAttributeAsync("src");
                product.ImageUrls.Add(imageUrl);
            }
        }

        var ele_Description = await page.QuerySelectorAsync("//div[@class='product-detail-content']");
        if (ele_Description is not null)
        {
            var description = await ele_Description.InnerHTMLAsync();
            product.Description = description.RemoveHrefFromA();
        }
        
        var ele_Comments = await page.QuerySelectorAllAsync("//div[@id='comments']//ol[@class='commentlist']/li");
        if (ele_Comments.Any())
        {
            var comments = new List<EcommerceProductComment>();
            foreach (var ele_Comment in ele_Comments)
            {
                var ele_mainComment = await ele_Comment.QuerySelectorAsync("//div[@class='comment_container']"); 
                var mainComment     = await GetCommentDetails(ele_mainComment);

                var ele_ChildrenComments = await ele_Comment.QuerySelectorAllAsync("//ul[@class='children']/li");
                if (ele_ChildrenComments.Any())
                {
                    mainComment.Feedbacks = new List<EcommerceProductComment>();
                    foreach (var ele_ChildrenComment in ele_ChildrenComments)
                    {
                        var subComment = await GetCommentDetails(ele_ChildrenComment);
                        mainComment.Feedbacks.Add(subComment);
                    }
                }
            
                comments.Add(mainComment);
            }

            product.Comments = comments;
        }
        
        var ele_Brand = await page.QuerySelectorAsync("//div/p[contains(text(),'Nhà sản xuất')]/../p[2]");
        if (ele_Brand is not null)
        {
            var brand = await ele_Brand.InnerTextAsync();
            product.Brand = brand;
        }
        
        #endregion

        #region Product Variants

        var variant = new EcommerceProductVariant
        {
            SKU = product.Code
        };
        
        var crawlPrice = await SieuThiSongKhoeHelper.CrawlPrice(page);
        variant.RetailPrice = crawlPrice.RetailPrice;
        variant.DiscountedPrice = crawlPrice.DiscountedPrice;
        variant.DiscountRate = crawlPrice.DiscountRate;
        
        product.Variants.Add(variant);

        #endregion

        #region Product Attributes
        
        
        var manufacturerLabel = "Nhà sản xuất";
        var ele_Manufacturer  = await page.QuerySelectorAsync($"//p[contains(text(),'{manufacturerLabel}')]/../p[2]");
        if (ele_Manufacturer is not null)
        {
            var manufacturer = await ele_Manufacturer.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute()
            {
                Key   = manufacturerLabel,
                Value = manufacturer.Replace($"{manufacturerLabel}:", string.Empty).Replace($"{manufacturerLabel} :", string.Empty)
            });
        }
        
        var originLabel = "Nơi sản xuất";
        var ele_Origin  = await page.QuerySelectorAsync($"//p[contains(text(),'{originLabel}')]/../p[2]"); 
        if (ele_Origin is not null)
        {
            var origin = await ele_Origin.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute()
            {
                Key   = originLabel,
                Value = origin.Replace($"{originLabel}:", string.Empty).Replace($"{originLabel} :", string.Empty)
            });
        }
        
        var usageFormLabel = "Quy cách đóng gói";
        var ele_UsageForm  = await page.QuerySelectorAsync($"//p[contains(text(),'{usageFormLabel}')]/../p[2]"); 
        if (ele_UsageForm is not null)
        {
            var usageForm = await ele_UsageForm.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute()
            {
                Key   = usageFormLabel,
                Value = usageForm.Replace($"{usageFormLabel}:", string.Empty).Replace($"{usageFormLabel} :", string.Empty)
            });
        }
        
        var preserveFormLabel = "Bảo quản";
        var ele_PreserveForm  = await page.QuerySelectorAsync($"//p[contains(text(),'{preserveFormLabel}')]/../p[2]"); 
        if (ele_PreserveForm is not null)
        {
            var preserveForm = await ele_PreserveForm.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute()
            {
                Key   = preserveFormLabel,
                Value = preserveForm.Replace($"{preserveFormLabel}:", string.Empty).Replace($"{preserveFormLabel} :", string.Empty)
            });
        }
        
        var noteLabel = "Các lưu ý";
        var ele_Note  = await page.QuerySelectorAsync($"//p[contains(text(),'{noteLabel}')]/../p[2]");
        if (ele_Note is not null)
        {
            var note = await ele_Note.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute()
            {
                Key   = noteLabel,
                Value = note.Replace($"{noteLabel}:", string.Empty).Replace($"{noteLabel} :", string.Empty)
            });
        }

        var productNameLabel = "Tên sản phẩm";
        var ele_Name = await page.QuerySelectorAsync($"//p[contains(text(),'{productNameLabel}')]/../p[2]");
        if (ele_Name is not null)
        {
            var name = await ele_Name.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute
            {
                Key = productNameLabel,
                Value = name.Trim()
            });
        }

        var producerNameLabel = "Nhà phân phối";
        var ele_Producer = await page.QuerySelectorAsync($"//p[contains(text(),'{producerNameLabel}')]/../p[2]");
        if (ele_Producer is not null)
        {
            var producer = await ele_Producer.InnerTextAsync();
            product.Attributes.Add(new EcommerceProductAttribute
            {
                Key = producerNameLabel,
                Value = producer.Trim()
            });
        }

        var addressNameLabel = "Địa chỉ";
        var ele_Address = await page.QuerySelectorAsync($"//p[contains(text(),'{addressNameLabel}')]/../p[2]");
        if (ele_Address is not null)
        {
            var address = await ele_Address.InnerTextAsync();
            if (!address.Contains("Địa chỉ:"))
            {
                product.Attributes.Add(new EcommerceProductAttribute
                {
                    Key   = addressNameLabel,
                    Value = address.Trim()
                });
            }
        }
        
        #endregion

        var crawlResult  = new CrawlResult { DataSourceType         = DataSourceType.Website, SourceType = SourceType.LC };
        var crawlPayload = new CrawlPayload { CrawlEcommercePayload = new CrawlEcommercePayload { Products = new List<CrawlEcommerceProductPayload> { product } } };

        crawlResult.Posts.Add(crawlPayload);

        return crawlResult;
    }
    
    async Task<EcommerceProductComment> GetCommentDetails(IElementHandle? eleMainComment)
    {
        var ele_CommentText    = await eleMainComment.QuerySelectorAsync("//div[contains(@class,'comment-text')]");
        var ele_AuthorComment  = await ele_CommentText.QuerySelectorAsync("//strong[contains(@class,'woocommerce-review__author')]");
        var authorComment      = ele_AuthorComment is not null ? await ele_AuthorComment.InnerTextAsync() : string.Empty;
        var ele_CreatedComment = await ele_CommentText.QuerySelectorAsync("//time[@class='woocommerce-review__published-date']");
        var createdComment     = ele_CreatedComment is not null ? await ele_CreatedComment.GetAttributeAsync("datetime") : string.Empty;
        var ele_RatingComment  = await ele_CommentText.QuerySelectorAsync("//div[@class='star-rating']//strong[@class='rating']");
        var ratingComment      = ele_RatingComment is not null ? await ele_RatingComment.InnerTextAsync() : "0";
        var ele_ContentComment = await ele_CommentText.QuerySelectorAsync("//div[@class='description']");
        var contentComment     = ele_ContentComment is not null ? await ele_ContentComment.InnerTextAsync() : string.Empty;
        
        var productComment = new EcommerceProductComment
        {
            Content = contentComment,
            Likes   = 0,
            Name    = authorComment,
            Rating  = ratingComment.ToIntOrDefault()
        };
        
        if (createdComment.IsNotNullOrEmpty())
        {
            var createdAtComment = createdComment.ToDateTime();
            if (createdAtComment is not null) 
                productComment.CreatedAt = (DateTime)createdAtComment;
        }

        return productComment;
    }
}