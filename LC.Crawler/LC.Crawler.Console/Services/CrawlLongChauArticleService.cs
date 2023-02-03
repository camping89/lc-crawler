using System.Collections.Concurrent;
using System.Globalization;
using Dasync.Collections;
using Flurl;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace LC.Crawler.Console.Services;

public class CrawlLongChauArticleService : CrawlLCArticleBaseService
{
    private const string ArticleBaseSlug = "bai-viet";

    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        // var articlePayloads = await GetArticleLinks(url);
        // await GetMainArticleLinks(url, articlePayloads);

        return new CrawlArticlePayload
        {
            ArticlesPayload = new List<ArticlePayload>(),
            Url = url
        };
    }

    private async Task GetMainArticleLinks(string url, List<ArticlePayload> articlePayloads)
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            new List<CrawlerAccountCookie>(),
            false);

        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                var homePage = await browserContext.BrowserContext.NewPageAsync();
                await homePage.UnloadResource();
                try
                {
                    var mainArticleUrl = Url.Combine(url, ArticleBaseSlug);
                    await homePage.GotoAsync(mainArticleUrl);
                    await homePage.Wait();

                    var ele_Hover = await homePage.QuerySelectorAllAsync("//div[@class='ss-chuyende-content']//div[@class='img-hover']/div");
                    foreach (var elementHandle in ele_Hover)
                    {
                        var ele_ArticleUrl = await elementHandle.QuerySelectorAsync("//a");
                        var articleUrl = await ele_ArticleUrl.GetAttributeAsync("href");
                        var ele_Image = await elementHandle.QuerySelectorAsync("//img");
                        var img = await ele_Image.GetAttributeAsync("src");
                        var ele_Title = await elementHandle.QuerySelectorAsync("//h3");
                        var title = await ele_Title.InnerTextAsync();
                        var ele_Date = await elementHandle.QuerySelectorAsync("//span");
                        var dateString = await ele_Date.InnerTextAsync();
                        var dateTime = GetArticleCreatedAt(dateString);
                        articlePayloads.Add(new ArticlePayload
                        {
                            Url = articleUrl,
                            Title = title,
                            FeatureImage = img,
                            CreatedAt = dateTime
                        });
                    }

                    var articles = await GetArticleUrls(homePage, mainArticleUrl, true);
                    articlePayloads.AddRange(articles);
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                }
                finally
                {
                    await homePage.CloseAsync();
                }
            }
        }
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var lcArticles = new ConcurrentBag<ArticlePayload>();
        // await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async articlePayloadBatch =>
        // {
        //     var articles = await CrawlLCArticles(articlePayloadBatch.ToList());
        //     lcArticles.AddRange(articles);
        // }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        var articleDisease = await CrawlDisease(crawlArticlePayload.Url);

        lcArticles.AddRange(articleDisease);

        return lcArticles;
    }

    #region Crawl Disease

    private async Task<List<ArticlePayload>> CrawlDisease(string domainPage)
    {
        var articlePayloads = new List<ArticlePayload>();
        var categoryUrls = await GetCategoryUrls(domainPage);

        foreach (var categoryUrl in categoryUrls)
        {
            await categoryUrl.Value.Partition(5).ParallelForEachAsync(async urls =>
            {
                var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot,
                    string.Empty,
                    0,
                    string.Empty,
                    string.Empty,
                    new List<CrawlerAccountCookie>(),
                    false);
                using (browserContext.Playwright)
                {
                    await using (browserContext.Browser)
                    {
                        var homePage = await browserContext.BrowserContext.NewPageAsync();
                        await homePage.UnloadResource();

                        try
                        {
                            foreach (var url in urls)
                            {
                                await homePage.GotoAsync(url);
                                await homePage.Wait();

                                System.Console.WriteLine($"Crawl Article {categoryUrl.Key} - {url}");

                                var articlePayload = new ArticlePayload();
                                var ele_title = await homePage.QuerySelectorAsync("//div[@class='content-main']//div[contains(@class,'cs-benh-heading')]/h1");
                                if (ele_title is not null)
                                {
                                    var title = await ele_title.InnerTextAsync();
                                    articlePayload.Title = title;
                                }

                                var ele_CreatedAt = await homePage.QuerySelectorAsync("//div[@class='content-main']//div[contains(@class,'cs-benh-heading')]/span[@class='time']");
                                if (ele_CreatedAt is not null)
                                {
                                    var createdAt = GetArticleCreatedAt(await ele_CreatedAt.InnerTextAsync());
                                    articlePayload.CreatedAt = createdAt;
                                }

                                var ele_ShortDescription =
                                    await homePage.QuerySelectorAsync("//div[@class='content-main']//div[contains(@class,'cs-benh-heading')]/div[@class='description']");
                                if (ele_ShortDescription is not null)
                                {
                                    var shortDescription = (await ele_ShortDescription.InnerTextAsync()).Trim();
                                    articlePayload.ShortDescription = shortDescription;
                                }

                                var ele_content = await homePage.QuerySelectorAsync("//div[@class='content-main']//div[contains(@class,'cs-benh-content')]");
                                if (ele_content is not null)
                                {
                                    var content = await ele_content.InnerHTMLAsync();
                                    content = content.RemoveHrefFromA();
                                    articlePayload.Content = content.Trim();
                                }

                                var ele_Tags = await homePage.QuerySelectorAllAsync("//div[@class='content-main']//div[contains(@class,'tags-item')]/a");
                                var tags = new List<string>();
                                foreach (var ele_Tag in ele_Tags)
                                {
                                    tags.Add(await ele_Tag.InnerTextAsync());
                                }

                                articlePayload.Tags = tags;
                                articlePayload.Category = categoryUrl.Key;
                                articlePayload.Url = url;
                                articlePayloads.Add(articlePayload);
                            }
                        }
                        catch (Exception e)
                        {
                            await e.Log(string.Empty, string.Empty);
                        }
                        finally
                        {
                            await homePage.CloseAsync();
                        }
                    }
                }
            }, GlobalConfig.CrawlConfig.Crawl_MaxThread);
        }

        return articlePayloads;
    }

    private async Task<Dictionary<string, List<string>>> GetCategoryUrls(string domainPage)
    {
        var categoryUrls = new Dictionary<string, List<string>>();

        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            new List<CrawlerAccountCookie>(),
            false);
        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                var homePage = await browserContext.BrowserContext.NewPageAsync();
                await homePage.UnloadResource();
                try
                {
                    var url = Url.Combine(domainPage, "benh");
                    await homePage.GotoAsync(url);
                    await homePage.Wait();

                    // Benh theo bo phan co the
                    await GetUrlDiseasesByBodyPart(homePage, categoryUrls);

                    // Benh Thuong Gap
                    var categories = new List<string> {"BỆNH NAM GIỚI", "BỆNH NỮ GIỚI", "BỆNH NGƯỜI GIÀ", "BỆNH TRẺ EM"};
                    foreach (var category in categories)
                    {
                        await GetUrlCommonDiseases(category, homePage, browserContext, categoryUrls);
                    }

                    // Seasonal disease
                    await GetUrlSeasonalDisease(homePage, categoryUrls);

                    //Disease Group
                    await GetUrlDiseaseGroup(homePage, categoryUrls);
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                }
                finally
                {
                    await homePage.CloseAsync();
                }
            }
        }

        return categoryUrls;
    }

    private static async Task GetUrlDiseasesByBodyPart(IPage homePage, Dictionary<string, List<string>> categoryUrls)
    {
        var ele_Btns = await homePage.QuerySelectorAllAsync("//a[contains(@class,'nut-btn')]");
        foreach (var ele_btn in ele_Btns)
        {
            var category = $"Bộ phận cơ thể -> {await ele_btn.InnerTextAsync()}";
            await ele_btn.Click();
            await homePage.Wait(500);
            var ele_Links = await homePage.QuerySelectorAllAsync("//div[@class='tab-content current']/a[boolean(@href)]");
            var urls = new List<string>();
            foreach (var ele_Link in ele_Links)
            {
                var articleUrl = await ele_Link.GetAttributeAsync("href");
                urls.Add(articleUrl);
            }

            categoryUrls.Add(category, urls);
        }
    }

    private async Task GetUrlDiseaseGroup(IPage homePage, Dictionary<string, List<string>> categoryUrls)
    {
        var ele_Groups = await homePage.QuerySelectorAllAsync("//div[@id='benh-desktop']//div[contains(@class,'title')]/h3");
        // a[contains(text(),'Xem Thêm') or contains(text(),'Xem thêm') and boolean(@style) = false]
        var loadMoreBtn = "//../..//ul//a[(contains(text(),'Xem Thêm') or contains(text(),'Xem thêm') or contains(text(),'xem thêm')) and boolean(@style) = false]";
            
        foreach (var ele_Group in ele_Groups)
        {
            var category = await ele_Group.InnerTextAsync();
            await ele_Group.Click();
            await homePage.Wait(500);
            var ele_LoadMore = await ele_Group.QuerySelectorAsync(loadMoreBtn);
            if (ele_LoadMore is not null)
            {
                while (ele_LoadMore is not null)
                {
                    await ele_LoadMore.Click();
                    await homePage.Wait(500);
                    ele_LoadMore = await ele_Group.QuerySelectorAsync(loadMoreBtn);
                }
            }

            var ele_Urls = await ele_Group.QuerySelectorAllAsync("//../..//ul//a[boolean(contains(text(),'Xem thêm')) = false and boolean(contains(text(),'Xem Thêm')) = false and boolean(contains(text(),'xem thêm')) = false]");
            var urls = new List<string>();
            foreach (var ele_Url in ele_Urls)
            {
                var url = await ele_Url.GetAttributeAsync("href");
                urls.Add(url);
            }

            categoryUrls.Add(category, urls);
        }
    }

    private async Task GetUrlSeasonalDisease(IPage homePage, Dictionary<string, List<string>> categoryUrls)
    {
        var ele_Urls = await homePage.QuerySelectorAllAsync("//h2[text()='Bệnh theo mùa']/..//a");
        var urls = new List<string>();
        foreach (var ele_Url in ele_Urls)
        {
            var url = await ele_Url.GetAttributeAsync("href");
            urls.Add(url);
        }

        categoryUrls.Add("Bệnh theo mùa", urls);
    }

    private async Task GetUrlCommonDiseases(string category, IPage homePage, PlaywrightContext context, Dictionary<string, List<string>> categoryUrls)
    {
        var ele = await homePage.QuerySelectorAsync($"//h3[text()='{category}']/..");
        if (ele is not null)
        {
            var url = await ele.GetAttributeAsync("href");

            var page = await context.BrowserContext.NewPageAsync();
            await page.UnloadResource();

            try
            {
                await page.GotoAsync(url);
                await page.Wait();

                var elements = await page.QuerySelectorAllAsync("//div[@class='list-item']/a");
                var subCategory = $"Bệnh thường gặp -> {category}";
                var subUrls = new List<string>();
                foreach (var elementHandle in elements)
                {
                    var subUrl = await elementHandle.GetAttributeAsync("href");
                    subUrls.Add(subUrl);
                }

                categoryUrls.Add(subCategory, subUrls);
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
            finally
            {
                await page.CloseAsync();
            }
        }
    }

    #endregion

    private async Task<List<ArticlePayload>> GetArticleLinks(string domainPage)
    {
        var articlePayloads = new List<ArticlePayload>();
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            new List<CrawlerAccountCookie>(),
            false);

        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                var homePage = await browserContext.BrowserContext.NewPageAsync();
                await homePage.UnloadResource();
                try
                {
                    var articleUrl = Url.Combine(domainPage, ArticleBaseSlug);
                    await homePage.GotoAsync(articleUrl);
                    await homePage.Wait();

                    var ele_Categories = await homePage.QuerySelectorAllAsync("//div[@class='ss-chuyende-content']//div[@class='nav']//li[boolean(@class='home') = false]/a");
                    var categoryUrls = new List<string>();
                    foreach (var ele_Category in ele_Categories)
                    {
                        var categoryUrl = await ele_Category.GetAttributeAsync("href");
                        categoryUrls.Add(categoryUrl);
                    }

                    var subCategoryUrls = new List<string>();
                    foreach (var categoryUrl in categoryUrls)
                    {
                        await homePage.GotoAsync(categoryUrl);
                        await homePage.Wait();
                        var ele_SubCategories = await homePage.QuerySelectorAllAsync("//div[@class='item']/ul/li[@class='active']/ul/li/a[boolean(@id='more') = false]");
                        if (ele_SubCategories.Any())
                        {
                            foreach (var ele_SubCategory in ele_SubCategories)
                            {
                                var url = await ele_SubCategory.GetAttributeAsync("href");
                                subCategoryUrls.Add(url);
                            }
                        }
                        else
                        {
                            subCategoryUrls.Add(categoryUrl);
                        }
                    }

                    foreach (var subCategoryUrl in subCategoryUrls)
                    {
                        var articles = await GetArticleUrls(homePage, subCategoryUrl);
                        articlePayloads.AddRange(articles);
                    }
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                }
                finally
                {
                    await homePage.CloseAsync();
                }
            }
        }

        return articlePayloads.ToList();
    }

    private async Task<List<ArticlePayload>> GetArticleUrls(IPage articlePage, string categoryUrl, bool isMainArticle = false)
    {
        var page = 1;
        var isDateTimeConditionNotMet = false;
        var articleUrls = new List<ArticlePayload>();

        while (true)
        {
            if (isDateTimeConditionNotMet) break;

            var url = Url.Combine(categoryUrl, $"?page={page}");
            await articlePage.GotoAsync(url);
            await articlePage.Wait();

            try
            {
                IReadOnlyList<IElementHandle> ele_Articles;
                if (isMainArticle)
                {
                    ele_Articles = await articlePage.QuerySelectorAllAsync("//div[contains(@class, 'ss-chuyende-news')]//article[@class='t-news']");
                }
                else
                {
                    ele_Articles = await articlePage.QuerySelectorAllAsync("//div[contains(@class, 'chuyende-sub-news')]//article[@class='t-news']");
                }
                
                if (ele_Articles.Any())
                {
                    foreach (var ele_Article in ele_Articles)
                    {
                        var ele_CreatedAt = await ele_Article.QuerySelectorAsync("//span[@class='date']");
                        if (ele_CreatedAt is null) continue;

                        var createdAt = GetArticleCreatedAt(await ele_CreatedAt.InnerTextAsync());
                        if (!await IsValidArticle(articleUrls.Count, string.Empty, createdAt))
                        {
                            isDateTimeConditionNotMet = true;
                            break;
                        }

                        var ele_Link = await ele_Article.QuerySelectorAsync("//a[@class='title']");
                        if (ele_Link is null) continue;

                        var link = await ele_Link.GetAttributeAsync("href");

                        var ele_Image = await ele_Article.QuerySelectorAsync("//img");
                        var image = string.Empty;
                        var title = string.Empty;
                        if (ele_Image is not null)
                        {
                            image = await ele_Image.GetAttributeAsync("src");
                            title = await ele_Image.GetAttributeAsync("alt");
                        }

                        articleUrls.Add(new ArticlePayload
                        {
                            Url = link,
                            CreatedAt = createdAt,
                            FeatureImage = image,
                            Title = title
                        });
                    }
                }
                else
                {
                    return articleUrls;
                }
                
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
            finally
            {
                page++;
            }
        }

        return articleUrls;
    }

    private DateTime GetArticleCreatedAt(string articleTime)
    {
        articleTime = articleTime.Replace("ngày", string.Empty)
            .Replace("Thứ Hai", string.Empty)
            .Replace("Thứ Ba", string.Empty)
            .Replace("Thứ Tư", string.Empty)
            .Replace("Thứ Năm", string.Empty)
            .Replace("Thứ Sáu", string.Empty)
            .Replace("Thứ Bảy", string.Empty)
            .Replace("Chủ Nhật", string.Empty)
            .Trim();

        var createdAt = DateTime.ParseExact(articleTime, "dd/MM/yyyy", CultureInfo.CurrentCulture);
        return createdAt;
    }


    private async Task<List<ArticlePayload>> CrawlLCArticles(List<ArticlePayload> articlePayloads)
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0,
            string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                foreach (var articlePayload in articlePayloads)
                {
                    var articlePage = await browserContext.BrowserContext.NewPageAsync();
                    await articlePage.UnloadResource();
                    try
                    {
                        System.Console.WriteLine($"CRAWL ARTICLE: {articlePayload.Url}");
                        await CrawlArticle(articlePage, articlePayload);
                        System.Console.WriteLine(JsonConvert.SerializeObject(articlePayload));
                    }
                    catch (Exception e)
                    {
                        await e.Log(string.Empty, string.Empty);
                    }
                    finally
                    {
                        await articlePage.CloseAsync();
                    }
                }
            }
        }

        return articlePayloads;
    }

    private async Task CrawlArticle(IPage page, ArticlePayload articleInput)
    {
        var article = articleInput;
        await page.GotoAsync(articleInput.Url);

        try
        {
            var ele_ShortDesc = await page.QuerySelectorAsync("//div[contains(@class,' post-detail')]/div/p[@class='short-description']");
            if (ele_ShortDesc is not null)
            {
                var shortDescription = await ele_ShortDesc.InnerTextAsync();
                article.ShortDescription = shortDescription.RemoveHrefFromA();
            }

            var categoryName = string.Empty;
            var ele_Categories = await page.QuerySelectorAllAsync("//ol[contains(@class, 'breadcrumb')]/li/a[not(text() = 'Trang chủ')]");
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
                article.Category = categoryName;
            }

            var ele_Content = await page.QuerySelectorAsync("//div[contains(@class,' post-detail')]/div[@class='r1-1']");
            if (ele_Content is not null)
            {
                var createdAtStr = string.Empty;
                var ele_CreatedAt = await page.QuerySelectorAsync("//div[@class= 'detail']/p");
                if (ele_CreatedAt is not null)
                {
                    createdAtStr = await ele_CreatedAt.InnerTextAsync();
                }

                var content         = await ele_Content.InnerHTMLAsync();
                var ele_RelatedNews = await ele_Content.QuerySelectorAsync("//div[@class='list-title']");
                var relatedNews     = string.Empty;
                if (ele_RelatedNews is not null)
                {
                    relatedNews = await ele_RelatedNews.InnerHTMLAsync();
                }

                var hashtag     = string.Empty;
                var ele_Hashtag = await ele_Content.QuerySelectorAsync("//div[@class='tag']");
                if (ele_Hashtag is not null)
                {
                    hashtag      = await ele_Hashtag.InnerHTMLAsync();
                    var ele_Hashtags = await ele_Hashtag.QuerySelectorAllAsync("//li");
                    if (ele_Hashtags.Any())
                    {
                        article.Tags = new List<string>();
                        foreach (var eleHashtag in ele_Hashtags)
                        {
                            var tag = await eleHashtag.InnerTextAsync();
                            article.Tags.Add(tag);
                        }
                    }
                }

                // remove useless value in content
                content = content.Replace($"<h1>{article.Title}</h1>", string.Empty);
                if (createdAtStr.IsNotNullOrEmpty())
                {
                    content = content.Replace($"{createdAtStr}", string.Empty);
                }
                
                if (relatedNews.IsNotNullOrEmpty())
                {
                    content = content.Replace(relatedNews, string.Empty);
                }

                if (hashtag.IsNotNullOrEmpty())
                {
                    content = content.Replace(hashtag, string.Empty);
                }
                
                content         = content.RemoveHrefFromA();
                article.Content = content;
            }
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, string.Empty);
        }
    }
}