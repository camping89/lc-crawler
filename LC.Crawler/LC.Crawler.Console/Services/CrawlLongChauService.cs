using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Dasync.Collections;
using Flurl;
using LC.Crawler.Client.Entities;
using LC.Crawler.Console.Services.Helper;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;
using Newtonsoft.Json;
using RestSharp;

namespace LC.Crawler.Console.Services;

public class CrawlLongChauService : CrawlLCEcommerceBaseService
{
    private const string LongChauUrl = "https://nhathuoclongchau.com.vn";

    // Example: https://nhathuoclongchau.com/filter/comments?productId=20580&type=desc&page=2
    private const string LoadCommentApi = "comments?skipCount";

    // Example https://nhathuoclongchau.com/load-more-reply?id=54038&type=comment&page=2
    private const string LoadMoreReplyApi = "load-more-reply";
    private const string BrandThuocUrl = "https://nhathuoclongchau.com.vn/thuoc";
    private const string BrandThuocName = "thuoc";

    private const string CategoryApiUrl =
        "https://api.nhathuoclongchau.com.vn/lccus/search-product-service/api/products/ecom/product/search/cate";

    protected override async Task<CrawlEcommercePayload> GetCrawlEcommercePayload(IPage page, string url)
    {
        var ecommercePayload = new CrawlEcommercePayload
        {
            Products = new List<CrawlEcommerceProductPayload>()
        };

        var brandUrls = new List<string>();
        var brandTotalProducts = new List<string>();
        // 1. Get list product category
        var ele_brandBtns = await page.QuerySelectorAllAsync("//div[@class='ant-space-item']/li/a[not(contains(@href, 'bai-viet')) and not(contains(@href, 'benh')) and not(contains(@href, 'he-thong-cua-hang'))]");

        foreach (var result in ele_brandBtns)
        {
            var brandUrl = await result.GetAttributeAsync("href");
            if (brandUrl.IsNullOrWhiteSpace()) continue;
            if (!brandUrl.Contains(LongChauUrl))
            {
                brandUrl = Url.Combine(LongChauUrl, brandUrl);
            }

            brandUrls.Add(brandUrl);
        }

        foreach (var brandUrl in brandUrls)
        {
            await page.GotoAsync(brandUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions {Timeout = 60000});
            if (brandUrl.Contains(BrandThuocUrl))
            {
                await GetTotalProductThuocBrand(page, brandTotalProducts);
            }
            else
            {
                brandTotalProducts.Add(brandUrl);
            }
        }

        foreach (var brandTotalProduct in brandTotalProducts)
        {
            ecommercePayload.Products.Add(new CrawlEcommerceProductPayload
            {
                Url = brandTotalProduct
            });
        }
        return ecommercePayload;
    }

    protected override async Task<ConcurrentBag<CrawlEcommerceProductPayload>> GetCrawlEcommerceProductPayload(CrawlEcommercePayload crawlEcommercePayload)
    {
        var crawlEcommercePayloads = new ConcurrentBag<CrawlEcommerceProductPayload>();

        foreach (var brandUrl in crawlEcommercePayload.Products)
        {
            try
            {
                var urls = new List<string>();

                urls.AddRange(await GetProductUrlByApi(brandUrl));

                await GetProducts(urls, crawlEcommercePayloads);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(
                    $"CRAWL ERROR ====================================================================");
                System.Console.WriteLine($"{GetType().Name}: CRAWL ERROR {brandUrl.Url}");
                System.Console.WriteLine($"{e.Message}");
                System.Console.WriteLine(
                    $"================================================================================");
                await e.Log(string.Empty, string.Empty);
            }
        }

        return crawlEcommercePayloads;
    }


    private static async Task GetTotalProductThuocBrand(IPage page, ICollection<string> brandTotalProducts)
    {
        var ele_categories = await page.QuerySelectorAllAsync("//h2[text()='Thuốc theo nhóm trị liệu']/../div/a");
        foreach (var elementHandle in ele_categories)
        {
            var cateUrl = await elementHandle.GetAttributeAsync("href");
            if (!cateUrl.IsNotNullOrEmpty()) continue;
            if (!cateUrl.Contains(LongChauUrl))
            {
                cateUrl = Url.Combine(LongChauUrl, cateUrl);
            }
            
            brandTotalProducts.Add(cateUrl);
        }
    }

    private async Task GetProducts(IEnumerable<string> urls, ConcurrentBag<CrawlEcommerceProductPayload> crawlEcommercePayloads)
    {
        await urls.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async batch =>
        {
            var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0,
                string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);

            using (browserContext.Playwright)
            {
                await using (browserContext.Browser)
                {
                    try
                    {
                        foreach (var url in batch)
                        {
                            RETRY:
                            var hasException = false;
                            var productPage = await browserContext.BrowserContext.NewPageAsync();
                            await productPage.UnloadResource();
                            
                            try
                            {
                                System.Console.WriteLine($"-----------------------Product Count: {batch.ToList().IndexOf(url)}/{batch.Count()}");
                                var product = new CrawlEcommerceProductPayload()
                                {
                                    Url = url
                                };
                                System.Console.WriteLine(
                                    $"====================={this.GetType().Name}: Trying to CRAWL url {url}");
                                if (!url.Contains("https://"))
                                {
                                    product.Url = $"https://{url}";
                                }

                                await productPage.GotoAsync(product.Url, new PageGotoOptions {WaitUntil = WaitUntilState.DOMContentLoaded});
                                await productPage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                                    new PageWaitForLoadStateOptions {Timeout = 60000});

                                // 3. Crawl product detail
                                await GetCrawlProductPayload(productPage, product);
                                System.Console.WriteLine(JsonConvert.SerializeObject(product));
                                crawlEcommercePayloads.Add(product);
                            }
                            catch (Exception e)
                            {
                                await e.Log(string.Empty, string.Empty);
                                hasException = true;
                            }
                            finally
                            {
                                await productPage.CloseAsync();
                            }

                            if (hasException)
                            {
                                goto RETRY;
                            }
                        }
                    }
                    finally
                    {
                        await browserContext.BrowserContext.CloseAsync();
                    }
                }
            }
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);
    }

    private async Task GetCrawlProductPayload(IPage page, CrawlEcommerceProductPayload input)
    {
        await CrawlCategory(page, input);

        // Crawl Title
        var ele_Title = await page.QuerySelectorAsync("//div[contains(@class,'detail_product')]//h1");
        if (ele_Title is not null)
        {
            input.Title = (await ele_Title.InnerTextAsync()).Trim();
            input.Title = input.Title.Replace("\n", string.Empty).Trim();
        }

        // Crawl ProductCode
        var ele_ProductCode = await page.QuerySelectorAsync("//div[contains(@class,'detail_sku-information')]/span");
        if (ele_ProductCode is not null)
        {
            input.Code = (await ele_ProductCode.InnerTextAsync()).Trim();
        }

        // Crawl Variant
        await CrawlVariant(page, input);

        // Crawl Attributes
        await CrawlAttributes(page, input);

        // Crawl Images
        var ele_Images =
            await page.QuerySelectorAllAsync(
                "//div[@class='cursor-pointer']/picture[contains(@class,'gallery-img')]/source[1]");
        foreach (var item in ele_Images)
        {
            var imageUrl = await item.GetAttributeAsync("srcset");
            if (imageUrl.IsNullOrWhiteSpace()) continue;
            input.ImageUrls.Add(imageUrl);
        }

        // Crawl Descriptions
        await CrawlDescription(page, input);

        // Crawl Comment
        await PerformCrawlingComment(page, input);

        // Crawl Review
        // await PerformCrawlingReview(page, input);
    }

    private async Task PerformCrawlingReview(IPage page, CrawlEcommerceProductPayload input)
    {
        while (true)
        {
            var ele_HideViewMore =
                await page.QuerySelectorAsync("//div[@id='loadMoreReview' and contains(@style,'display: none;')]");
            if (ele_HideViewMore is not null) break;
            var ele_ReviewViewMore =
                await page.QuerySelectorAsync("//a[contains(@class,'btn') and contains(text(),'Xem thêm đánh giá')]");
            if (ele_ReviewViewMore is null) break;
            if (await ele_ReviewViewMore.IsEnabledAsync())
            {
                try
                {
                    System.Console.WriteLine($"---------------Click Review View More-----------------");
                    await page.RunAndWaitForResponseAsync(async () =>
                    {
                        await ele_ReviewViewMore.Click();
                    }, response => response.Url.Contains(LoadCommentApi) && response.Status == 200, new PageRunAndWaitForResponseOptions {Timeout = 3000});
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                    break;
                }
            }
            else
            {
                break;
            }
        }

        var ele_ReplyReviewViewMores =
            await page.QuerySelectorAllAsync("//div[boolean(@style)=false]/a[contains(text(),'Xem thêm trả lời')]");
        foreach (var item in ele_ReplyReviewViewMores)
        {
            if (await item.IsEnabledAsync())
            {
                try
                {
                    System.Console.WriteLine($"---------------Click Reply View More-----------------");
                    await page.RunAndWaitForResponseAsync(async () =>
                    {
                        await item.Click();
                    }, response => response.Url.Contains(LoadMoreReplyApi) && response.Status == 200, new PageRunAndWaitForResponseOptions {Timeout = 3000});
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                    break;
                }
            }
        }

        var ele_FirstPageReviews =
            await page.QuerySelectorAllAsync(
                "//div[@id='listReview']/div/div/div[contains(@class,'lc__cmt-content flex-fill')]");

        var ele_NewPageReviews =
            await page.QuerySelectorAllAsync(
                "//div[contains(@class,'new-page-reviews')]/div/div/div[contains(@class,'lc__cmt-content flex-fill')]");

        var firstPageReviews = await CrawlReviews(ele_FirstPageReviews);
        input.Reviews.AddRange(firstPageReviews);

        var newPageReviews = await CrawlReviews(ele_NewPageReviews);
        input.Reviews.AddRange(newPageReviews);
    }

    private async Task PerformCrawlingComment(IPage page, CrawlEcommerceProductPayload input)
    {
        while (true)
        {
            var ele_CommentViewMore =
                await page.QuerySelectorAsync("//div[@class='title']/h2[contains(text(),'Hỏi đáp')]/../../../../../descendant::div[contains(@class,'lc-preview__loadmore')]/p[contains(text(),'Xem thêm ')]");
            if (ele_CommentViewMore is null) break;
            
            if (await ele_CommentViewMore.IsEnabledAsync())
            {
                try
                {
                    await page.RunAndWaitForResponseAsync(async () =>
                    {
                        await ele_CommentViewMore.Click();
                    }, response => response.Url.Contains(LoadCommentApi) && response.Status == 200, new PageRunAndWaitForResponseOptions {Timeout = 10000});
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                    break;
                }
            }
            else
            {
                break;
            }
        }

        var ele_ReplyViewMores =
            await page.QuerySelectorAllAsync("//div[@class='title']/h2[contains(text(),'Hỏi đáp')]/../../../../../descendant::div[contains(@class,'lc-preview__comments')]/descendant::li/p[contains(text(),'Xem thêm')]");
        foreach (var item in ele_ReplyViewMores)
        {
            if (await item.IsEnabledAsync())
            {
                try
                {
                    await page.RunAndWaitForResponseAsync(async () =>
                    {
                        await item.Click();
                    }, response => response.Url.Contains(LoadMoreReplyApi) && response.Status == 200, new PageRunAndWaitForResponseOptions {Timeout = 3000});
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                    break;
                }
            }
        }

        var ele_NewPageComments =
            await page.QuerySelectorAllAsync(
                "//div[@class='title']/h2[contains(text(),'Hỏi đáp')]/../../../../../descendant::div[contains(@class,'lc-preview__comments')]/ul/li");
        
        var newPageComments = await CrawlComments(ele_NewPageComments);
        input.Comments.AddRange(newPageComments);
    }

    private static async Task CrawlDescription(IPage page, CrawlEcommerceProductPayload input)
    {
        var ele_DescriptionViewMore = await page.QuerySelectorAsync(
            "//div[@id='content-wrapper']/descendant::p[text()='Xem thêm']");
        if (ele_DescriptionViewMore is not null) await ele_DescriptionViewMore.Click();
        var ele_Description =
            await page.QuerySelectorAsync(
                "//div[@id='content-wrapper']/div/div[contains(@class,'lc-col-2')]");
        if (ele_Description is not null)
        {
            var desc = await ele_Description.InnerHTMLAsync();
            input.Description = desc.RemoveHrefFromA();
        }
    }

    private async Task CrawlAttributes(IPage page, CrawlEcommerceProductPayload input)
    {
        var ele_Ratings = await page.QuerySelectorAsync("//div[@class='rating-avarageScore']/p[2]");
        input.Attributes.Add(new EcommerceProductAttribute
        {
            Key = "Rating",
            Value = (await ele_Ratings.InnerTextAsync())
        });

        var ele_Brand = await page.QuerySelectorAsync("//div/span[contains(text(),'Thương hiệu')]/../span[2]");
        if (ele_Brand is not null)
        {
            input.Brand = (await ele_Brand.InnerTextAsync());
        }

        var ele_ProductDetails =
            await page.QuerySelectorAllAsync("//table[@class='content-list']/tbody/tr");
        foreach (var item in ele_ProductDetails)
        {
            var td_Elements = await item.QuerySelectorAllAsync("//td");
            if (td_Elements.Any() && td_Elements.Count > 1)
            {
                var key = (await td_Elements.First().InnerTextAsync()).Replace(":", string.Empty).Trim();
                var value = (await td_Elements.Last().InnerTextAsync());
                input.Attributes.Add(new EcommerceProductAttribute
                {
                    Key = key,
                    Value = value
                });
            }
        }

        var ele_PreserveForm = await page.QuerySelectorAsync("//div[contains(@class,'typho-BaoQuan')]");
        if (ele_PreserveForm is not null)
        {
            var value = await ele_PreserveForm.InnerTextAsync();
            input.Attributes.Add(new EcommerceProductAttribute
            {
                Key = "Bảo quản",
                Value = value.Replace("Bảo quản", string.Empty).Trim()
            });
        }
    }

    private static async Task CrawlVariant(IPage page, CrawlEcommerceProductPayload input)
    {
        var ecommerceProductVariant = new EcommerceProductVariant();

        var crawlPricePayload = await LongChauHelper.CrawlPrice(page);

        ecommerceProductVariant.RetailPrice = crawlPricePayload.RetailPrice;
        ecommerceProductVariant.DiscountedPrice = crawlPricePayload.DiscountedPrice;
        ecommerceProductVariant.DiscountRate = crawlPricePayload.DiscountRate;


        input.Variants.Add(ecommerceProductVariant);
    }

    private static async Task CrawlCategory(IPage page, CrawlEcommerceProductPayload input)
    {
        var ele_Categories = await page.QuerySelectorAllAsync("//div[@id='overlay-menu']/../div/div/nav/ol/li/span[@class='ant-breadcrumb-link']");
        var categories = new List<string>();
        foreach (var item in ele_Categories)
        {
            var text = await item.InnerTextAsync();
            if (text.Contains("Trang chủ")) continue;
            categories.Add(text);
        }

        input.Category = string.Join(" -> ", categories);
        System.Console.WriteLine($"---------------Category: {input.Category}");
    }

    private async Task<List<string>> GetProductUrlByApi(CrawlEcommerceProductPayload crawlEcommerceProductPayload)
    {
        var category = crawlEcommerceProductPayload.Url.Replace(LongChauUrl, string.Empty).Trim('/');
        var productUrls = new ConcurrentBag<string>();
        var skipCount = 0;
        while (true)
        {
            var client = new RestClient(CategoryApiUrl);
            var request = new RestRequest();

            request.AddJsonBody(new LCApiRequest(skipCount, 50, new List<string>
            {
                "brand",
                "objectUse",
                "indications",
                "priceRanges"
            }, 4, new List<string> { category }));

            var response = await client.PostAsync<LCApiResponse>(request);
            
            if (response.products.IsNullOrEmpty()) break;

            var urls = response.products.Select(product => Url.Combine(LongChauUrl, product.slug)).ToList();

            if (urls.Any())
            {
                System.Console.WriteLine(JsonConvert.SerializeObject(urls));

                productUrls.AddRange(urls);
            }
            else
            {
                break;
            }
            
            System.Console.WriteLine($"Total Products: {productUrls.Count}");

            using var autoResetEvent = new AutoResetEvent(false);
            autoResetEvent.WaitOne(100);
            skipCount += 50;
        }

        System.Console.WriteLine($"Total Products Before Distinct: {productUrls.Count}");
        var returnValue = productUrls.Distinct().ToList();
        return returnValue;
    }

    private async Task<List<EcommerceProductComment>> CrawlComments(IEnumerable<IElementHandle> ele_FirstPageComments)
    {
        var comments = new List<EcommerceProductComment>();
        
        var index = 1;
        foreach (var item in ele_FirstPageComments)
        {
            var parentComment = new EcommerceProductComment();
            var ele_commentParent = await item.QuerySelectorAsync("//div/descendant::div[@class='content flex-1']/p");
            if (ele_commentParent is not null)
            {
                parentComment.Name = (await ele_commentParent.InnerTextAsync()).Trim().Replace("Quản Trị Viên", string.Empty)
                    .Replace("Quản trị viên",string.Empty);
            }
            
            var ele_content = await item.QuerySelectorAsync("//div/descendant::div[@class='user-comment']/p");
            if (ele_content is not null)
            {
                parentComment.Content = (await ele_content.InnerTextAsync()).Trim();
            }
            
            var ele_dateTime = await item.QuerySelectorAsync("//div/descendant::div[@class='flex items-center']/span[1]");
            if (ele_dateTime is not null)
            {
                var commentTime = (await ele_dateTime.InnerTextAsync()).Trim();
                parentComment.CreatedAt = commentTime.TimeAgoToDateTime();
            }
            
            var ele_childrenComment = await item.QuerySelectorAllAsync("//ul/li");
            if (ele_childrenComment.IsNotNullOrEmpty())
            {
                parentComment.Feedbacks.AddRange(await CrawlComments(ele_childrenComment));
            }
            comments.Add(parentComment);
        }

        return comments;
    }

    private async Task<List<EcommerceProductReview>> CrawlReviews(IEnumerable<IElementHandle> ele_FirstPageReviews)
    {
        var reviews = new List<EcommerceProductReview>();
        var reviewParent = new EcommerceProductReview();
        var index = 1;
        foreach (var item in ele_FirstPageReviews)
        {
            var isFeedback = false;
            var review = new EcommerceProductReview();

            var nameQuery = await item.QuerySelectorAsync("//div[contains(@class,'avatar-name')]");
            if (nameQuery is not null)
            {
                review.Name = (await nameQuery.InnerTextAsync()).Trim();
            }

            var commentTime = string.Empty;
            var timeQuery =
                await item.QuerySelectorAsync(
                    "//div[contains(@class,'lc__cmt-rate')]//span[contains(@class,'avatar-time')]");
            if (timeQuery is not null)
            {
                commentTime = (await timeQuery.InnerTextAsync()).Trim();
                review.CreatedAt = commentTime.TimeAgoToDateTime();
            }

            // time query is null but time feedback is not null -> this review is feedback
            var timeFeedbackQuery = await item.QuerySelectorAsync("//div[contains(@class,'avt-time')]");
            if (timeFeedbackQuery is not null)
            {
                isFeedback = true;
                commentTime = (await timeFeedbackQuery.InnerTextAsync()).Trim();
                review.CreatedAt = commentTime.TimeAgoToDateTime();
            }

            var contentQuery = await item.QuerySelectorAsync("//div[contains(@class,'lc__cmt-content--full')]");
            if (contentQuery is not null)
            {
                review.Content = (await contentQuery.InnerTextAsync()).Trim();
            }

            //Get Like example: (1)
            var likeQuery = await item.QuerySelectorAsync("//span[contains(@class,'total-like')]");
            if (likeQuery is not null)
            {
                var like = (await likeQuery.InnerTextAsync()).Trim();
                like = Regex.Replace(like, "[()]", string.Empty).Trim();
                review.Likes = like.ToIntODefault();
            }

            var ele_ReviewRatings = await item.QuerySelectorAllAsync("//ul/li/span[@class='ic-star']");
            review.Rating = ele_ReviewRatings.Count;

            // feedback is in the last review
            // if not set the last review is the review just crawled
            if (isFeedback)
            {
                reviewParent.Feedbacks.Add(review);
                reviews.Remove(reviews.Last());
                reviews.Add(reviewParent);
            }
            else
            {
                reviews.Add(review);
                reviewParent = review;
                index++;
            }
        }

        return reviews;
    }


    public class LCApiResponse
    {
        public List<Product> products { get; set; }
        public List<Aggregation> aggregations { get; set; }
        public int totalCount { get; set; }
    }
    
    public class LCApiRequest
    {
        public LCApiRequest(int skipCount, int maxResultCount, IList<string> codes, int sortType, IList<string> category)
        {
            SkipCount = skipCount;
            MaxResultCount = maxResultCount;
            Codes = codes;
            SortType = sortType;
            Category = category;
        }

        public int SkipCount { get; set; }
        public int MaxResultCount { get; set; }
        public IList<string> Codes { get; set; }
        public int SortType { get; set; }
        public IList<string> Category { get; set; }
    }
    
    public class Aggregation
    {
        public string code { get; set; }
        public List<string> values { get; set; }
    }
    
    public class Category
    {
        public int id { get; set; }
        public string name { get; set; }
        public string parentName { get; set; }
        public string slug { get; set; }
        public int level { get; set; }
        public bool isActive { get; set; }
    }

    public class Price
    {
        public int id { get; set; }
        public int measureUnitCode { get; set; }
        public string measureUnitName { get; set; }
        public bool isSellDefault { get; set; }
        public double price { get; set; }
        public string currencySymbol { get; set; }
        public bool isDefault { get; set; }
        public int inventory { get; set; }
    }

    public class Product
    {
        public string sku { get; set; }
        public string name { get; set; }
        public string webName { get; set; }
        public string image { get; set; }
        public List<Category> category { get; set; }
        public Price price { get; set; }
        public string slug { get; set; }
        public string ingredients { get; set; }
        public string dosageForm { get; set; }
        public string brand { get; set; }
        public int displayCode { get; set; }
        public bool isActive { get; set; }
        public bool isPublish { get; set; }
        public double searchScoring { get; set; }
        public int productRanking { get; set; }
        public string specification { get; set; }
    }
}