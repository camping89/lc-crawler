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
    private const string LongChauUrl = "https://nhathuoclongchau.com.vn/";

    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
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
                                var ele_title = await homePage.QuerySelectorAsync("//h1[contains(@class,'text-text-primary')]");
                                if (ele_title is not null)
                                {
                                    var title = await ele_title.InnerTextAsync();
                                    articlePayload.Title = title;
                                }

                                var ele_CreatedAt = await homePage.QuerySelectorAsync("//h1[contains(@class,'text-text-primary')]/../div[2]//span[contains(@class,'estore-icon')]/../span[2]");
                                if (ele_CreatedAt is not null)
                                {
                                    var createdAt = GetArticleCreatedAt(await ele_CreatedAt.InnerTextAsync());
                                    articlePayload.CreatedAt = createdAt;
                                }

                                var ele_ShortDescription =
                                    await homePage.QuerySelectorAsync("//p[contains(@class,'text-text-primary')]");
                                if (ele_ShortDescription is not null)
                                {
                                    var shortDescription = (await ele_ShortDescription.InnerTextAsync()).Trim();
                                    articlePayload.ShortDescription = shortDescription;
                                }

                                var ele_content = await homePage.QuerySelectorAsync("//div[@class='news-detail-content-wrapper']");
                                if (ele_content is not null)
                                {
                                    var content = await ele_content.InnerHTMLAsync();
                                    content = content.RemoveHrefFromA();
                                    articlePayload.Content = content.Trim();
                                }

                                var ele_Tags = await homePage.QuerySelectorAllAsync("//span[text()='Chủ đề:']/../a");
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
                    var categories = new List<string> {"Bệnh nam giới", "Bệnh nữ giới", "Bệnh người già", "Bệnh trẻ em"};
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

    private async Task GetUrlDiseasesByBodyPart(IPage homePage, Dictionary<string, List<string>> categoryUrls)
    {
        var ele_buttons = await homePage.QuerySelectorAllAsync("//div[@class='menu-list md:h-full']/button");
        if (ele_buttons.IsNotNullOrEmpty())
        {
            foreach (var ele_button in ele_buttons)
            {
                var category = $"Cơ thể người -> {await ele_button.InnerTextAsync()}";
                await ele_button.Click();
                await homePage.Wait(500);
                var urls = await GetUrls(homePage);
                categoryUrls.Add(category, urls.ToList());
            }
        }
    }

    private async Task<IList<string>> GetUrls(IPage homePage)
    {
        var urls = new List<string>();
        var ele_btnNexts = await homePage.QuerySelectorAllAsync(
            "//div[@class='menu-content-pagination']//ul/li[contains(@class,'ant-pagination-item')]");
        if (ele_btnNexts.IsNotNullOrEmpty())
        {
            foreach (var ele_btn in ele_btnNexts)
            {
                await ele_btn.Click();
                await homePage.Wait(500);
            
                urls.AddRange(await PerformGettingUrls(homePage));
            }
        }
        else
        {
            urls.AddRange(await PerformGettingUrls(homePage));
        }
        return urls;
    }

    private async Task<IList<string>> PerformGettingUrls(IPage homePage)
    {
        var urls = new List<string>();
        var ele_Links = await homePage.QuerySelectorAllAsync("//div[@class='menu-content-list--item']/a");
        if (ele_Links.IsNotNullOrEmpty())
        {
            foreach (var ele_link in ele_Links)
            {
                var articleUrl = await ele_link.GetAttributeAsync("href");
                articleUrl = Url.Combine(LongChauUrl, articleUrl);
                urls.Add(articleUrl);
            }
        }

        return urls;
    }

    private async Task<IList<string>> PerformGettingUrlDiseaseGroup(IPage homePage)
    {
        IList<string> urls = new List<string>();
        var ele_Items = await homePage.QuerySelectorAllAsync("((//h2[contains(@class,'items-center')])[4])/..//a[contains(@class,'disease-item')]");
        if (ele_Items.IsNotNullOrEmpty())
        {
            foreach (var elementHandle in ele_Items)
            {
                var articleUrl = await elementHandle.GetAttributeAsync("href");
                articleUrl = Url.Combine(LongChauUrl, articleUrl);
                urls.Add(articleUrl);
            }
        }

        return urls;
    }

    private async Task GetUrlDiseaseGroup(IPage homePage, Dictionary<string, List<string>> categoryUrls)
    {
        var ele_groups = await homePage.QuerySelectorAllAsync("((//h2[contains(@class,'items-center')])[4])/..//div[@class='ant-space-item']");
        if (ele_groups.IsNotNullOrEmpty())
        {
            foreach (var eleGroup in ele_groups)
            {
                await eleGroup.Click();
                await homePage.Wait(1000);
                var ele_category = await eleGroup.QuerySelectorAsync("//span/p");
                var category = await ele_category.InnerTextAsync();
                category = $"{"Nhóm bệnh chuyên khoa"} -> {category}";
                var ele_btnNexts = await homePage.QuerySelectorAllAsync("((//h2[contains(@class,'items-center')])[4])/..//li[contains(@class,'ant-pagination-item')]");
                if (ele_btnNexts.IsNotNullOrEmpty())
                {
                    var urls = new List<string>();
                    foreach (var eleBtnNext in ele_btnNexts)
                    {
                        await eleBtnNext.Click();
                        await homePage.Wait(1000);
                        urls.AddRange(await PerformGettingUrlDiseaseGroup(homePage));
                    }
                    
                    categoryUrls.Add(category, urls.ToList());
                }
                else
                {
                    var urls = await PerformGettingUrlDiseaseGroup(homePage);
                    categoryUrls.Add(category, urls.ToList());
                }
            }
        }
    }

    private async Task GetUrlSeasonalDisease(IPage homePage, Dictionary<string, List<string>> categoryUrls)
    {
        var ele_Urls = await homePage.QuerySelectorAllAsync("//h2[contains(text(),'Bệnh theo mùa')]/..//a[contains(@class,'text-text-link')]");
        var urls = new List<string>();
        foreach (var ele_Url in ele_Urls)
        {
            var url = await ele_Url.GetAttributeAsync("href");
            url = Url.Combine(LongChauUrl, url);
            urls.Add(url);
        }

        categoryUrls.Add("Bệnh theo mùa", urls);
    }

    private async Task GetUrlCommonDiseases(string category, IPage homePage, PlaywrightContext context, Dictionary<string, List<string>> categoryUrls)
    {
        var subCategory = $"Bệnh theo đối tượng -> {category}";
        var subUrls = new List<string>();
        var ele = await homePage.QuerySelectorAsync($"//h3[text()='{category}']/../..//div[@class='mt-auto']/a");
        if (ele is not null)
        {
            var url = await ele.GetAttributeAsync("href");
            url = Url.Combine(LongChauUrl, url);

            var page = await context.BrowserContext.NewPageAsync();
            await page.UnloadResource();

            try
            {
                await page.GotoAsync(url);
                await page.Wait();
                
                var ele_btnNexts = await page.QuerySelectorAllAsync("//li[contains(@class,'ant-pagination-item')]");
                if (ele_btnNexts.IsNotNullOrEmpty())
                {
                    foreach (var elementHandle in ele_btnNexts)
                    {
                        await elementHandle.Click();
                        await page.Wait(500);
            
                        subUrls.AddRange(await PerformGetSubUrlCommonDiseases(page));
                    }
                }
                else
                {
                    subUrls.AddRange(await PerformGetSubUrlCommonDiseases(page));
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

    private async Task<IList<string>> PerformGetSubUrlCommonDiseases(IPage homePage)
    {
        var subUrls = new List<string>();
        var elements = await homePage.QuerySelectorAllAsync("//li[@class='cate-item']/a");
     
        foreach (var elementHandle in elements)
        {
            var subUrl = await elementHandle.GetAttributeAsync("href");
            subUrl = Url.Combine(LongChauUrl, subUrl);
            subUrls.Add(subUrl);
        }
        return subUrls;
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
        articleTime = articleTime.Replace("ngày", string.Empty).Replace("Ngày", string.Empty)
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