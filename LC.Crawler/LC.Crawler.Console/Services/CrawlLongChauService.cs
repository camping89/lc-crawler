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
    private const string LongChauUrl = "https://nhathuoclongchau.com/";

    // Example: https://nhathuoclongchau.com/filter/comments?productId=20580&type=desc&page=2
    private const string LoadCommentApi = "comments?productId";

    // Example https://nhathuoclongchau.com/load-more-reply?id=54038&type=comment&page=2
    private const string LoadMoreReplyApi = "load-more-reply";
    private const string BrandThuocUrl = "https://nhathuoclongchau.com/thuoc";
    private const string BrandThuocName = "thuoc";

    protected override async Task<CrawlEcommercePayload> GetCrawlEcommercePayload(IPage page, string url)
    {
        var ecommercePayload = new CrawlEcommercePayload
        {
            Products = new List<CrawlEcommerceProductPayload>()
        };

        var brandUrls = new List<string>();
        var brandTotalProducts = new List<string>();
        // 1. Get list product category
        var ele_brandBtns = await page.QuerySelectorAllAsync("//ul/li/a/i/..");

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
        var ele_ViewMoreButton = await page.QuerySelectorAsync("//div[contains(@class, 'lc__view-more')]/div");
        if (ele_ViewMoreButton is not null)
        {
            await ele_ViewMoreButton.ClickAsync();
            var ele_SubCategories = await page.QuerySelectorAllAsync("//div[contains(@class,'lc__list-cate')]//div[contains(@class, 'list-more-item')]");
            if (ele_SubCategories.Any())
            {
                foreach (var ele_SubCategory in ele_SubCategories)
                {
                    var ele_SubCateUrl = await ele_SubCategory.QuerySelectorAsync("//h3[contains(@class, 'cate__product-name')]/a");
                    var ele_SubCateTotalCount = await ele_SubCategory.QuerySelectorAsync("//div[contains(@class, 'cate__product-count')]");

                    var cateUrl = ele_SubCateUrl is not null ? await ele_SubCateUrl.GetAttributeAsync("href") : string.Empty;
                    if (!cateUrl.IsNotNullOrEmpty()) continue;

                    if (!cateUrl.Contains(LongChauUrl))
                    {
                        cateUrl = Url.Combine(LongChauUrl, cateUrl);
                    }

                    brandTotalProducts.Add(cateUrl);
                }
            }
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
        var ele_Title = await page.QuerySelectorAsync("//div[contains(@class,'pcd-title')]//h1");
        if (ele_Title is not null)
        {
            input.Title = (await ele_Title.InnerTextAsync()).Trim();
            input.Title = input.Title.Replace("\n", string.Empty).Trim();
        }

        // Crawl ProductCode
        var ele_ProductCode = await page.QuerySelectorAsync("//span[@id='copy']");
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
                "//picture/img[contains(@onclick,'Product Detail Page') and contains(@onclick,'Large Image')]");
        foreach (var item in ele_Images)
        {
            var imageUrl = await item.GetAttributeAsync("src");
            if (imageUrl.IsNullOrWhiteSpace()) continue;
            input.ImageUrls.Add(imageUrl);
        }

        // Crawl Descriptions
        await CrawlDescription(page, input);

        // Crawl Comment
        await PerformCrawlingComment(page, input);

        // Crawl Review
        await PerformCrawlingReview(page, input);
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
            var ele_HideViewMore =
                await page.QuerySelectorAsync("//div[@id='lcViewMoreCm' and contains(@style,'display: none;')]");
            if (ele_HideViewMore is not null) break;
            
            
            var ele_CommentViewMore =
                await page.QuerySelectorAsync("//a[contains(@class,'btn') and contains(text(),'Xem thêm bình luận')]");
            if (ele_CommentViewMore is null) break;
            
            if (await ele_CommentViewMore.IsEnabledAsync())
            {
                try
                {
                    await page.RunAndWaitForResponseAsync(async () =>
                    {
                        await ele_CommentViewMore.Click();
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

        var ele_ReplyViewMores =
            await page.QuerySelectorAllAsync("//div[boolean(@style)=false]/a[contains(text(),'Xem thêm trả lời')]");
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

        var ele_FirstPageComments =
            await page.QuerySelectorAllAsync(
                "//div[@id='commentList']/div/div/div[contains(@class,'lc__cmt-content flex-fill')]");

        var ele_NewPageComments =
            await page.QuerySelectorAllAsync(
                "//div[@id='newPageComments']/div/div/div[contains(@class,'lc__cmt-content flex-fill')]");
        var firstPageComments = await CrawlComments(ele_FirstPageComments);
        input.Comments.AddRange(firstPageComments);

        var newPageComments = await CrawlComments(ele_NewPageComments);
        input.Comments.AddRange(newPageComments);
    }

    private static async Task CrawlDescription(IPage page, CrawlEcommerceProductPayload input)
    {
        var ele_DescriptionViewMore = await page.QuerySelectorAsync(
            "//div[@class='container']//div[contains(@class,'ppc-typhography tpcn-typho')]/div[boolean(@style)=false]/a");
        if (ele_DescriptionViewMore is not null) await ele_DescriptionViewMore.Click();
        var ele_Descriptions =
            await page.QuerySelectorAllAsync(
                "//div[@class='container']//div[contains(@class,'ppc-typhography tpcn-typho')]/div[boolean(@style)=false]");
        var descriptions = new List<string>();
        foreach (var item in ele_Descriptions)
        {
            string ele_FontTypeHtml = string.Empty;
            var ele_FontType = await item.QuerySelectorAsync("//div[contains(@class,'option-wrap-update')]");
            if (ele_FontType is not null)
            {
                ele_FontTypeHtml = await ele_FontType.InnerHTMLAsync();
            }

            var text = await item.InnerHTMLAsync();
            if (text.IsNullOrWhiteSpace()) continue;
            if (ele_FontTypeHtml.IsNotNullOrEmpty())
            {
                text = text.Replace(ele_FontTypeHtml, string.Empty);
            }

            descriptions.Add(text);
        }

        var ele_CallOutContent = await page.QuerySelectorAsync("//div[@class='container']//div[contains(@class,'callout-content')]");
        if (ele_CallOutContent is not null)
        {
            var text = await ele_CallOutContent.InnerTextAsync();
            if (text.IsNotNullOrWhiteSpace())
            {
                descriptions.Add(text);
            }
        }

        var desc = string.Join(Environment.NewLine, descriptions).Trim();
        input.Description = desc.RemoveHrefFromA();
    }

    private async Task CrawlAttributes(IPage page, CrawlEcommerceProductPayload input)
    {
        var ele_Ratings = await page.QuerySelectorAllAsync("//ul/ul[@id='starRatingTop']/../li/i[@class='ic-star']");
        input.Attributes.Add(new EcommerceProductAttribute
        {
            Key = "Rating",
            Value = ele_Ratings.Count.ToString()
        });

        var ele_Brand = await page.QuerySelectorAsync("//div/p[contains(text(),'Thương hiệu')]");
        if (ele_Brand is not null)
        {
            input.Brand = (await ele_Brand.InnerTextAsync()).Replace("Thương hiệu:", string.Empty).Trim();
        }

        var ele_ProductDetails =
            await page.QuerySelectorAllAsync("//div[contains(@class,'pcd-meta')]/table/tbody/tr");
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
        var ele_Categories = await page.QuerySelectorAllAsync("//div[@class='container']/ol/li/a");
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
        var category = crawlEcommerceProductPayload.Url.Replace(LongChauUrl, string.Empty);
        var productUrls = new ConcurrentBag<string>();
        var pageCount = 1;
        while (true)
        {
            var isThuocPage = crawlEcommerceProductPayload.Url.Contains($"/{BrandThuocName}/");

            var clientUrl = isThuocPage
                ?
                // $"{BrandThuocUrl}/load-more/?type=productBestSeller&page={pageCount}&slug_cate={category.Replace($"{BrandThuocName}/", string.Empty)}&cate_lev=2" : 
                // $"{crawlEcommerceProductPayload.Url}?page={pageCount}&loadMore=true&sort=mua-nhieu-nhat&currentLink={category}";
                Url.Combine(BrandThuocUrl, category.Replace($"{BrandThuocName}/", string.Empty), $"?type=hotsale&page={pageCount}&filter=1")
                : Url.Combine(LongChauUrl, category, $"?type=hotsale&page={pageCount}&filter=1$size=10");
            var client = new RestClient(clientUrl);
            var request = new RestRequest();
            request.AddHeader("x-requested-with", "XMLHttpRequest");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

            var response = await client.GetAsync<LCApiResponse>(request);
            // var view     = isThuocPage ? response?.Items : response?.View;
            var view = response.HTML;
            if (string.IsNullOrEmpty(view)) break;

            var regex = isThuocPage ? $"href=\"/{BrandThuocName}/" : $"href=\"/{category}/";
            var items = view.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            var urls = (from item in items
                where item.Contains(regex)
                select Url.Combine(LongChauUrl, isThuocPage ? $"{BrandThuocName}" : category,
                    item.Replace(regex, string.Empty)
                        .Replace("/n", string.Empty)
                        .Replace("\"", string.Empty)
                        .Replace(">", string.Empty)
                        .Trim())).Distinct().ToList();

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
            pageCount += 1;
        }

        System.Console.WriteLine($"Total Products Before Distinct: {productUrls.Count}");
        var returnValue = productUrls.ToList();
        return returnValue;
    }

    private async Task<List<EcommerceProductComment>> CrawlComments(IEnumerable<IElementHandle> ele_FirstPageComments)
    {
        var comments = new List<EcommerceProductComment>();
        var commentParent = new EcommerceProductComment();
        var index = 1;
        foreach (var item in ele_FirstPageComments)
        {
            var isFeedback = false;
            var comment = new EcommerceProductComment();

            var nameQuery = await item.QuerySelectorAsync("//div[contains(@class,'avatar-name')]");
            if (nameQuery is not null)
            {
                comment.Name = (await nameQuery.InnerTextAsync()).Trim();
            }

            var commentTime = string.Empty;
            var timeQuery = await item.QuerySelectorAsync("//span[contains(@class,'avatar-time')]");
            if (timeQuery is not null)
            {
                commentTime = (await timeQuery.InnerTextAsync()).Trim();
                comment.CreatedAt = commentTime.TimeAgoToDateTime();
            }

            // time query is null but time feedback is not null -> this comment is feedback
            var timeFeedbackQuery = await item.QuerySelectorAsync("//div[contains(@class,'avt-time')]");
            if (timeFeedbackQuery is not null)
            {
                isFeedback = true;
                commentTime = (await timeFeedbackQuery.InnerTextAsync()).Trim();
                comment.CreatedAt = commentTime.TimeAgoToDateTime();
            }

            var contentQuery = await item.QuerySelectorAsync("//div[contains(@class,'lc__cmt-content--full')]");
            if (contentQuery is not null)
            {
                comment.Content = (await contentQuery.InnerTextAsync()).Trim();
            }

            //Get Like example: (1)
            var likeQuery = await item.QuerySelectorAsync("//span[contains(@class,'total-like')]");
            if (likeQuery is not null)
            {
                var like = (await likeQuery.InnerTextAsync()).Trim();
                like = Regex.Replace(like, "[()]", string.Empty).Trim();
                comment.Likes = like.ToIntODefault();
            }

            if (comment.Name.Contains(commentTime))
                comment.Name = comment.Name.Replace(commentTime, string.Empty).Trim();

            // feedback is in the last comment
            // if not set the last comment is the review just crawled
            if (isFeedback)
            {
                commentParent.Feedbacks.Add(comment);
                comments.Remove(comments.Last());
                comments.Add(commentParent);
            }
            else
            {
                comments.Add(comment);
                commentParent = comment;
                index++;
            }
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
        public string View { get; set; }
        public string Items { get; set; }
        public string HTML { get; set; }
    }
}