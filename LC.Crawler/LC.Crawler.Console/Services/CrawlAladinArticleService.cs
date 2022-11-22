using System.Collections.Concurrent;
using System.Globalization;
using Dasync.Collections;
using FluentDate;
using FluentDateTime;
using Flurl;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace LC.Crawler.Console.Services;

public class CrawlAladinArticleService : CrawlLCArticleBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        var newsUrls = new Dictionary<string, string>
        {
            {"Tin tức", Url.Combine(url, "tin-tuc")},
            {"Tin tức -> Tin khuyến mãi", Url.Combine(url, "tin-tuc/tin-khuyen-mai/")}
        };

        var crawlArticlePayload = new CrawlArticlePayload();

        foreach (var newsUrl in newsUrls)
        {
            var pageNumber = 1;
            var totalArticles = 0;
            var isConditionMet = false;
            var articlePayloadCategory = new List<ArticlePayload>();
            do
            {
                var pageUrl = Url.Combine(newsUrl.Value, $"?page={pageNumber}");
                await page.GotoAsync(pageUrl);
                await page.Wait(3000);
                var ele_NewsItems = await page.QuerySelectorAllAsync("//div[@id='content']/div[@class='news-item']");
                totalArticles = ele_NewsItems.Count;

                foreach (var ele_NewsItem in ele_NewsItems)
                {
                    var articlePayload = new ArticlePayload();
                    var ele_Url = await ele_NewsItem.QuerySelectorAsync("//a");
                    if (ele_Url is not null)
                    {
                        var articleUrl = await ele_Url.GetAttributeAsync("href");
                        articleUrl = Url.Combine(url, articleUrl);

                        if (!await IsValidArticle(articlePayloadCategory.Count, articleUrl, null))
                        {
                            isConditionMet = true;
                            break;
                        }

                        articlePayload.Url = articleUrl;
                    }

                    var ele_Title = await ele_NewsItem.QuerySelectorAsync("//a/h3");
                    if (ele_Title is not null)
                    {
                        var title = await ele_Title.InnerTextAsync();

                        articlePayload.Title = title;
                    }

                    var ele_ShortDescription = await ele_NewsItem.QuerySelectorAsync("//p");
                    if (ele_ShortDescription is not null)
                    {
                        var shortDescription = await ele_ShortDescription.InnerTextAsync();

                        articlePayload.ShortDescription = shortDescription;
                    }

                    var ele_Image = await ele_NewsItem.QuerySelectorAsync("//a/img");
                    if (ele_Image is not null)
                    {
                        var image = await ele_Image.GetAttributeAsync("src");
                        articlePayload.FeatureImage = image;
                    }

                    articlePayload.Category = newsUrl.Key;

                    articlePayloadCategory.Add(articlePayload);
                }

                var ele_PageNumber = await page.QuerySelectorAsync("//ul[@class='page-numbers']//a");

                if (isConditionMet || ele_PageNumber is null)
                {
                    break;
                }

                pageNumber += 1;
            } while (totalArticles != 0);

            crawlArticlePayload.ArticlesPayload.AddRange(articlePayloadCategory);
        }

        return crawlArticlePayload;
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        ConcurrentBag<ArticlePayload> articlesPayload = new ConcurrentBag<ArticlePayload>();
        await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async articles =>
        {
            foreach (var articlePayload in articles)
            {
                var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0,
                    string.Empty, string.Empty, new List<CrawlerAccountCookie>());
                await using var browser = browserContext.Browser;
                {
                    var homePage = await browserContext.BrowserContext.NewPageAsync();
                    await homePage.UnloadResource();

                    await homePage.GotoAsync(articlePayload.Url);
                    await homePage.Wait(3000);

                    try
                    {
                        var createDateTime = await GetCreateDateTime(homePage);
                        if (createDateTime.HasValue)
                        {
                            articlePayload.CreatedAt = createDateTime.Value;
                        }

                        var ele_Content = await homePage.QuerySelectorAsync("//div[@class='entry-content']");
                        if (ele_Content is not null)
                        {
                            var content = await ele_Content.InnerHTMLAsync();
                            var ele_Remove = await ele_Content.QuerySelectorAsync("//span[@class='golink']");
                            if (ele_Remove is not null)
                            {
                                var html = await ele_Remove.InnerHTMLAsync();
                                content = content.Replace(html, string.Empty);
                            }

                            ele_Remove = await ele_Content.QuerySelectorAsync("//a[contains( text(),'Viên uống hỗ trợ giấc ngủ ngon Mamori')]");
                            if (ele_Remove is not null)
                            {
                                var html = await ele_Remove.InnerHTMLAsync();
                                content = content.Replace(html, string.Empty);
                            }

                            ele_Remove = await ele_Content.QuerySelectorAsync(
                                "//a[contains(@href,'https://nikenko.vn/danh-muc-san-pham/bo-nao-va-tang-cuong-tri-nho/vien-uong-ho-tro-ngu-ngon-mamori-glycine-l-theanine')]");
                            if (ele_Remove is not null)
                            {
                                var html = await ele_Remove.InnerHTMLAsync();
                                content = content.Replace(html, string.Empty);
                            }

                            ele_Remove = await ele_Content.QuerySelectorAsync("//p[contains(text(),'tham khảo các sản phẩm bổ')]");
                            if (ele_Remove is not null)
                            {
                                var html = await ele_Remove.InnerHTMLAsync();
                                content = content.Replace(html, string.Empty);
                            }

                            ele_Remove = await ele_Content.QuerySelectorAsync("//p[contains(text(),'Link đặt mua sản phẩm')]");
                            if (ele_Remove is not null)
                            {
                                var html = await ele_Remove.InnerHTMLAsync();
                                content = content.Replace(html, string.Empty);
                            }

                            ele_Remove = await ele_Content.QuerySelectorAsync("//em[contains(text(),'Link đặt mua sản phẩm')]");
                            if (ele_Remove is not null)
                            {
                                var html = await ele_Remove.InnerHTMLAsync();
                                content = content.Replace(html, string.Empty);
                            }

                            ele_Remove = await ele_Content.QuerySelectorAsync("//p[contains(text(),'Mua ngay sản phẩm giá ưu đãi')]");
                            if (ele_Remove is not null)
                            {
                                var html = await ele_Remove.InnerHTMLAsync();
                                content = content.Replace(html, string.Empty);
                            }

                            articlePayload.Content = content.RemoveHrefFromA();

                            System.Console.WriteLine(JsonConvert.SerializeObject(articlePayload));

                            articlesPayload.Add(articlePayload);
                        }
                    }
                    catch (Exception e)
                    {
                        await e.Log(string.Empty, $"{articlePayload.Url}");
                    }
                    finally
                    {
                        await homePage.CloseAsync();
                        await browserContext.BrowserContext.CloseAsync();
                    }
                }
            }
        });

        return articlesPayload;
    }


    protected override async Task<bool> IsValidArticle(int totalArticle, string url, DateTime? createdAtDateTime)
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0,
            string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
        await using var browser = browserContext.Browser;
        {
            var homePage = await browserContext.BrowserContext.NewPageAsync();
            await homePage.UnloadResource();

            try
            {
                if (!url.Contains("https://"))
                {
                    url = $"https://{url}";
                }

                await homePage.GotoAsync(url);
                await homePage.Wait(500);

                if (GlobalConfig.CrawlConfig.ValidateArticleByDateTime)
                {
                    var dateTime = await GetCreateDateTime(homePage);
                    if (dateTime.HasValue)
                    {
                        return dateTime.Value >= GetDaysCrawlingInterval().Days().Ago();
                    }

                    return false;
                }
                else
                {
                    return totalArticle <= GetTotalCrawlingArticlesInterval();
                }
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, $"{url}");
                return false;
            }
            finally
            {
                await homePage.CloseAsync();
                await browserContext.BrowserContext.CloseAsync();
            }
        }
    }

    private async Task<DateTime?> GetCreateDateTime(IPage page)
    {
        var ele_DateTime = await page.QuerySelectorAsync("//div[contains(@class,'page_date')]//em");
        if (ele_DateTime is not null)
        {
            var dateTimeString = await ele_DateTime.InnerTextAsync();
            dateTimeString = dateTimeString.Replace("Cập nhật", string.Empty).Trim();
            dateTimeString = dateTimeString.Split(",")[0].Trim();

            var isValid = DateTime.TryParseExact(dateTimeString, "dd-MM-yyyy", DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None, out var dateTime);
            if (isValid)
            {
                return dateTime;
            }
        }

        return null;
    }
}