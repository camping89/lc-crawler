using System.Collections.Concurrent;
using System.Globalization;
using Dasync.Collections;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace LC.Crawler.Console.Services;

public class CrawlSucKhoeGiaDinhService : CrawlLCArticleBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        // get URL articles
        var crawlSKGDPayloads = await GetCrawlSKGDPayload(url);

        return new CrawlArticlePayload
        {
            ArticlesPayload = crawlSKGDPayloads,
            Url = url
        };
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var skgdArticles        = new ConcurrentBag<ArticlePayload>();
        await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async listArticles =>
        {
            var articles = await GetCrawlSKGDArticles(listArticles.ToList());

            skgdArticles.AddRange(articles);
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return skgdArticles;
    }

    private async Task<List<ArticlePayload>> GetCrawlSKGDArticles(List<ArticlePayload> articles)
    {
        var             articlePayloads = new List<ArticlePayload>();
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0, 
                                                                       string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                foreach (var article in articles)
                {
                    var articlePage = await browserContext.BrowserContext.NewPageAsync();
                    await articlePage.UnloadResource();
                    try
                    {
                        var articlePayload = await CrawlArticle(articlePage, article);
                        System.Console.WriteLine(JsonConvert.SerializeObject(articlePayload));
                        articlePayloads.Add(articlePayload);
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

    private async Task<List<ArticlePayload>> GetCrawlSKGDPayload(string homeUrl)
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0, 
                                                                string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                var             homePage = await browserContext.BrowserContext.NewPageAsync();
                await homePage.UnloadResource();
                await homePage.GotoAsync(homeUrl);
                await homePage.Wait(3000);
                var articlePayloads = new List<ArticlePayload>();
        
                try
                {
                    var categoryUrls              = await GetCategoryUrls(homePage);
                    foreach (var categoryUrl in categoryUrls)
                    {
                        var articles = new List<ArticlePayload>();
                        await homePage.GotoAsync(Flurl.Url.Combine(homeUrl, categoryUrl));
            
                        System.Console.WriteLine($"CRAWL CHECKING CATE: {categoryUrl}");
            
                        // get main news
                        await GetMainArticles(browserContext, homePage, homeUrl, articles);
                        
                        // get primary news
                        await GetPrimaryArticles(homePage, homeUrl, articles);
                
                        // get sub news
                        await GetSubArticles(homePage, homeUrl, articles);
                        
                        articlePayloads.AddRange(articles);
            
                        System.Console.WriteLine($"CRAWL FOUND CATE: {categoryUrl}");
                    }
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                }
                finally
                {
                    // close home page
                    await homePage.CloseAsync();
                    await browserContext.BrowserContext.CloseAsync();
                }
                return articlePayloads;
            }
        }
        
    }

    private async Task GetSubArticles(IPage page, string homeUrl, List<ArticlePayload> articlePayloads)
    {
        var count                     = 0;
        var isDateTimeConditionNotMet = false;
        
        while (true)
        {
            if (isDateTimeConditionNotMet)
            {
                break;
            }
            
            var ele_News  = await page.QuerySelectorAllAsync("//div[contains(@class, 'col-right')]/div[@class='block-item-small-cat']/article");
            var validNews = ele_News.Skip(count).ToList();
            if (!validNews.Any())
            {
                break;
            }

            foreach (var validNew in validNews)
            {
                var ele_CreatedAt = await validNew.QuerySelectorAsync("//div[@class='tag']");
                if (ele_CreatedAt is not null)
                {
                    var createdAt = await GetArticleCreatedAt(ele_CreatedAt);
                    if (! await IsValidArticle(articlePayloads.Count, string.Empty, createdAt))
                    {
                        isDateTimeConditionNotMet = true;
                        break;
                    }
                            
                    var article = await CrawlShortArticle(validNew, homeUrl);
                    article.CreatedAt = createdAt;
                    articlePayloads.Add(article);
                }
            }
            
            if (isDateTimeConditionNotMet)
            {
                break;
            }

            count += validNews.Count;

            var viewMoreBtn = await page.QuerySelectorAsync("//div[contains(@class, 'col-right')]/div[contains(@class,'xem-them')]/a[@class='load_more_news_related']");
            if (viewMoreBtn is not null)
            {
                await viewMoreBtn.ClickAsync();
            }
        }
    }

    private async Task GetMainArticles(PlaywrightContext browserContext, IPage page, string homeUrl,
        List<ArticlePayload> articlePayloads)
    {
        var ele_MainNews    = await page.QuerySelectorAllAsync("//div[contains(@class, 'section_topstory__left')]//article");
        if (ele_MainNews.Any())
        {
            var mainNewPage = await browserContext.BrowserContext.NewPageAsync();
            await mainNewPage.UnloadResource();
            
            try
            {
                foreach (var ele_MainNew in ele_MainNews)
                {
                    var ele_Link = await ele_MainNew.QuerySelectorAsync("//a[contains(@class,'thumb')]");
                    if (ele_Link is not null)
                    {
                        var link = await ele_Link.GetAttributeAsync("href");
                        await mainNewPage.GotoAsync(Flurl.Url.Combine(homeUrl, link));
                            
                        var ele_CreatedAt = await mainNewPage.QuerySelectorAsync("//div[@class='detail_tin']/div/div/div[@class='date']");
                        if (ele_CreatedAt is not null)
                        {
                            var createdAt = await GetArticleCreatedAt(ele_CreatedAt);
                            if (! await IsValidArticle(articlePayloads.Count, string.Empty, createdAt))
                            {
                                break;
                            }
                            var mainArticle = await CrawlShortArticle(ele_MainNew, homeUrl);
                            mainArticle.CreatedAt = createdAt;
                            articlePayloads.Add(mainArticle);
                        }
                    }
                        
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
            }
            finally
            {
                await mainNewPage.CloseAsync();
            }
        }
    }

    private async Task<DateTime> GetArticleCreatedAt(IElementHandle ele_CreatedAt)
    {
        var createdAtStr = await ele_CreatedAt.InnerTextAsync();
        var ele_Category = await ele_CreatedAt.QuerySelectorAsync("//a");
        var category     = ele_Category is not null ? await ele_Category.InnerTextAsync() : string.Empty;
        if (category.IsNotNullOrEmpty())
        {
            createdAtStr = createdAtStr.Replace(category, string.Empty).Trim();
        }
        createdAtStr = createdAtStr.Replace("-", string.Empty).Trim();
        
        var timeAt = DateTime.Parse($"{createdAtStr.Split("|")[0].Trim()}");
        var dateAt = DateTime.ParseExact($"{createdAtStr.Split("|")[1].Trim()}", "dd/MM/yyyy", CultureInfo.CurrentCulture);

        var createdAt = dateAt.Add(timeAt.TimeOfDay);
        return createdAt;
    }

    private async Task GetPrimaryArticles(IPage page, string homeUrl, List<ArticlePayload> articlePayloads)
    {
        var ele_PrimaryNews = await page.QuerySelectorAllAsync("//div[contains(@class, 'col-right')]/article");
        if (ele_PrimaryNews.Any())
        {
            foreach (var ele_PrimaryNew in ele_PrimaryNews)
            {
                var ele_CreatedAt = await ele_PrimaryNew.QuerySelectorAsync("//div[@class='tag']");
                if (ele_CreatedAt is not null)
                {
                    var createdAt = await GetArticleCreatedAt(ele_CreatedAt);
                    if (!await IsValidArticle(articlePayloads.Count, string.Empty, createdAt))
                    {
                        break;
                    }
                            
                    var primaryArticle = await CrawlShortArticle(ele_PrimaryNew, homeUrl);
                    primaryArticle.CreatedAt = createdAt;
                    articlePayloads.Add(primaryArticle);
                }
            }
        }
    }

    private static async Task<ArticlePayload> CrawlShortArticle(IElementHandle mainNew, string homeUrl)
    {
        var mainArticle   = new ArticlePayload();
        var ele_ThumImage = await mainNew.QuerySelectorAsync("//div[@class='thumb-art']/a/img");
        if (ele_ThumImage is not null)
        {
            mainArticle.FeatureImage = await ele_ThumImage.GetAttributeAsync("src");
        }

        var ele_Title = await mainNew.QuerySelectorAsync("//h3[contains(@class, 'title-news')]/a");
        if (ele_Title is not null)
        {
            mainArticle.Title = await ele_Title.InnerTextAsync();

            var articleUrl = await ele_Title.GetAttributeAsync("href");
            mainArticle.Url = Flurl.Url.Combine(homeUrl, articleUrl);
        }

        var ele_ShortDesc = await mainNew.QuerySelectorAsync("//div[@class='description']");
        if (ele_ShortDesc is not null)
        {
            mainArticle.ShortDescription = await ele_ShortDesc.InnerTextAsync();
        }

        return mainArticle;
    }

    private async Task<List<string>> GetCategoryUrls(IPage page)
    {
        // Get main menu
        var ele_MainMenus = await page.QuerySelectorAllAsync("//nav[@class='main-nav']/ul/li/a");
        
        var menuUrls = new List<string>();
        foreach (var ele_ItemMenu in ele_MainMenus)
        {
            var itemMenuHref = await ele_ItemMenu.GetAttributeAsync("href");
            if (itemMenuHref is not null)
            {
                menuUrls.Add(itemMenuHref);
            }
        }

        return menuUrls;
    }
    
    private async Task<ArticlePayload> CrawlArticle(IPage page, ArticlePayload articleInput)
    {
        var article = articleInput;
        await page.GotoAsync(articleInput.Url);
        
        try
        {
            var ele_ShortDesc = await page.QuerySelectorAsync("//p[@class='lead']");
            if (ele_ShortDesc is not null)
            {
                article.ShortDescription = await ele_ShortDesc.InnerTextAsync();
            }

            if (article.CreatedAt == DateTime.MinValue)
            {
                var ele_CreatedAt = await page.QuerySelectorAsync("//div[@class='detail_tin']/div/div/div[@class='date']");
                if (ele_CreatedAt is not null)
                {
                    var createdAt = await ele_CreatedAt.InnerTextAsync(); 
                    createdAt = createdAt.Replace("-", string.Empty).Trim();
                
                    var timeAt = DateTime.Parse($"{createdAt.Split("|")[0].Trim()}");
                    var dateAt = DateTime.ParseExact($"{createdAt.Split("|")[1].Trim()}", "dd/MM/yyyy", CultureInfo.CurrentCulture);

                    article.CreatedAt = dateAt + timeAt.TimeOfDay;
                }
            }

            var ele_Category = await page.QuerySelectorAsync("//div[@class='detail_tin']/div/div/a[@class='cate']");
            if (ele_Category is not null)
            {
                article.Category = await ele_Category.InnerTextAsync();
            }

            await CrawlContent(page, article);

            await CrawlHashTags(page, article);
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, string.Empty);
        }

        return article;
    }

    private static async Task CrawlContent(IPage page, ArticlePayload article)
    {
        var ele_Content = await page.QuerySelectorAsync("//div[@class='detail_tin']//div[@class='content-detail__right']/div[@id='content']");
        if (ele_Content is not null)
        {
            article.Content = await ele_Content.InnerHTMLAsync();

            var element_ReadMore =
                await page.QuerySelectorAsync("//div[@class='detail_tin']//div[@class='content-detail__right']/div[@id='content']//p[contains(text(), 'Xem thêm:')]");
            if (element_ReadMore is not null)
            {
                var readMore = await element_ReadMore.InnerHTMLAsync();

                article.Content = article.Content.Replace(readMore, string.Empty);
            }

            var element_Author = await page.QuerySelectorAllAsync("//div[@class='detail_tin']//div[@class='content-detail__right']/div[@id='content']//p[@align='right']");
            foreach (var elementHandle in element_Author)
            {
                var author = await elementHandle.InnerHTMLAsync();
                article.Content = article.Content.Replace(author, string.Empty);
            }

            article.Content = article.Content.RemoveHrefFromA();
        }
    }

    private static async Task CrawlHashTags(IPage page, ArticlePayload article)
    {
        article.Tags = new List<string>();
        var ele_Tags = await page.QuerySelectorAllAsync("//div[@class='box-tag']//a[@class='item_tag']");
        foreach (var elementHandle in ele_Tags)
        {
            var tag = await elementHandle.InnerTextAsync();
            article.Tags.Add(tag);
        }
    }
}