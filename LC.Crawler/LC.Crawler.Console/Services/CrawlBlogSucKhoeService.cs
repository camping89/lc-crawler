using System.Collections.Concurrent;
using System.Globalization;
using Dasync.Collections;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services;

public class CrawlBlogSucKhoeService : CrawlLCArticleBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        var articlePayloads = await GetCategoryUrls(page);

        return new CrawlArticlePayload
        {
            ArticlesPayload = articlePayloads
        };
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        await crawlArticlePayload.ArticlesPayload.Partition(1).ParallelForEachAsync(async categoryUrls =>
        {
            foreach (var categoryUrl in categoryUrls)
            {
                var categoryHref = categoryUrl.Url.Replace("http:", "https:");
                System.Console.WriteLine($"CRAWL CHECKING CATE: {categoryHref}");

                try
                {
                    var articles = await PerformGettingArticleUrl(categoryHref, categoryUrl.Category);
                    articlePayloads.AddRange(articles);
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                }
            }
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articlePayloads;
    }


    private async Task<ConcurrentBag<ArticlePayload>> PerformGettingArticleUrl(string categoryUrl, string cateName)
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            new List<CrawlerAccountCookie>(),
            false);
        var articlePayloads = new ConcurrentBag<ArticlePayload>();

        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                var pageNumber = 1;
                while (true)
                {
                    var articlePage = await browserContext.BrowserContext.NewPageAsync();
                    await articlePage.UnloadResource();

                    try
                    {
                        var catePageHref = Flurl.Url.Combine(categoryUrl, $"/page/{pageNumber}");
                        await articlePage.GotoAsync(catePageHref, new PageGotoOptions {Timeout = 0});
                        System.Console.WriteLine($"CRAWL CHECKING PAGE: {catePageHref}");

                        var ele_Articles = await articlePage.QuerySelectorAllAsync("//div[@id='content']/div[contains(@class,'post')]");
                        if (!ele_Articles.Any())
                        {
                            break;
                        }

                        foreach (var ele_Article in ele_Articles)
                        {
                            var articleDetailsPage = await browserContext.BrowserContext.NewPageAsync();
                            await articleDetailsPage.UnloadResource();


                            try
                            {
                                // get url (get from list articles)
                                var ele_Url = await ele_Article.QuerySelectorAsync("//h2[@class='title']/a");
                                var articleUrl = string.Empty;
                                if (ele_Url is not null)
                                {
                                    articleUrl = await ele_Url.GetAttributeAsync("href");
                                }

                                await articleDetailsPage.GotoAsync(articleUrl, new PageGotoOptions {Timeout = 0});
                                System.Console.WriteLine($"CRAWL CHECKING DETAILS PAGE: {catePageHref} -- {articleUrl}");

                                var ele_CreatedAt = await articleDetailsPage.QuerySelectorAsync("//div[@id='content']//div[@class='postmeta-primary']/span[@class='meta_date']");
                                var createdAt = string.Empty;
                                var formatCreatedAt = DateTime.Today;
                                if (ele_CreatedAt is not null)
                                {
                                    createdAt = await ele_CreatedAt.InnerTextAsync();
                                }

                                if (createdAt.IsNotNullOrEmpty())
                                {
                                    formatCreatedAt = DateTime.ParseExact(createdAt, "dd/MM/yyyy", CultureInfo.CurrentCulture);
                                    if (!await IsValidArticle(articlePayloads.Count, string.Empty, formatCreatedAt))
                                    {
                                        return articlePayloads;
                                    }
                                }

                                // get image (get from list articles)
                                var articleImageSrc = string.Empty;
                                var ele_Image = await ele_Article.QuerySelectorAsync("//img");
                                if (ele_Image is not null)
                                {
                                    articleImageSrc = await ele_Image.GetAttributeAsync("src");
                                }

                                // get short desc (get from list articles)
                                var ele_ShortDesc = await ele_Article.QuerySelectorAsync("//div[@class='entry clearfix']");
                                var articleShortDesc = string.Empty;
                                if (ele_ShortDesc is not null)
                                {
                                    articleShortDesc = await ele_ShortDesc.InnerTextAsync();
                                    articleShortDesc = articleShortDesc.Replace("more »", string.Empty) + " ...";
                                    articleShortDesc = articleShortDesc.RemoveHrefFromA();
                                }

                                // get title (get from list articles)
                                string articleTitle = string.Empty;
                                var ele_Title = await ele_Article.QuerySelectorAsync("//h2[@class='title']");
                                if (ele_Title is not null)
                                {
                                    articleTitle = await ele_Title.InnerTextAsync();
                                }

                                // get content (get from article details)
                                var ele_Content = await articleDetailsPage.QuerySelectorAsync("//div[@id='content']//div[@class='entry clearfix']");
                                var content = string.Empty;
                                if (ele_Content is not null)
                                {
                                    var ele_Contents =
                                        await articleDetailsPage.QuerySelectorAllAsync("//div[@id='content']//div[@class='entry clearfix']//following-sibling::p[4]");
                                    foreach (var elementHandle in ele_Contents)
                                    {
                                        if (elementHandle != ele_Contents.Last())
                                        {
                                            content = $"{content}\n{await elementHandle.InnerHTMLAsync()}";
                                        }
                                    }

                                    content = content.Trim();

                                    content = content.RemoveHrefFromA();
                                }

                                // get tags (get from article details)
                                var ele_Hashtags = await articleDetailsPage.QuerySelectorAllAsync("//span[@class='meta_tags']/a");
                                var tags = new List<string>();
                                if (ele_Hashtags.Any())
                                {
                                    foreach (var eleHashtag in ele_Hashtags)
                                    {
                                        var tag = await eleHashtag.InnerTextAsync();
                                        tags.Add(tag);
                                    }
                                }

                                articlePayloads.Add(new ArticlePayload
                                {
                                    Url = articleUrl,
                                    Category = cateName,
                                    CreatedAt = formatCreatedAt,
                                    Title = articleTitle,
                                    FeatureImage = articleImageSrc,
                                    ShortDescription = articleShortDesc,
                                    Content = content,
                                    Tags = tags
                                });
                            }
                            catch (Exception e)
                            {
                                await e.Log(string.Empty, string.Empty);
                            }
                            finally
                            {
                                await articleDetailsPage.CloseAsync();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        await e.Log(string.Empty, string.Empty);
                    }
                    finally
                    {
                        pageNumber++;
                        await articlePage.CloseAsync();
                    }
                }
            }
        }

        return articlePayloads;
    }

    private async Task<List<ArticlePayload>> GetCategoryUrls(IPage page)
    {
        // Get main menu and hidden menu
        var articlePayloads = new List<ArticlePayload>();
        var ele_ItemMenus = new List<IElementHandle>();
        var ele_MainMenus = await page.QuerySelectorAllAsync("//div[@class='menu-primary-container']//ul[@id='menu-tren']/li/a[not(text() = 'Khám Bệnh Online')]");
        if (ele_MainMenus.Any())
        {
            ele_ItemMenus.AddRange(ele_MainMenus);
        }

        var ele_OtherMenus = await page.QuerySelectorAllAsync("//div[@class='menu-secondary-container']/ul/li/a");
        if (ele_OtherMenus.Any())
        {
            ele_ItemMenus.AddRange(ele_OtherMenus);
        }

        foreach (var elementHandle in ele_ItemMenus)
        {
            var itemMenuHref = await elementHandle.GetAttributeAsync("href");
            if (itemMenuHref is not null)
            {
                var category = (await elementHandle.InnerTextAsync()).Replace("\n»", string.Empty);
                articlePayloads.Add(new ArticlePayload
                {
                    Url = itemMenuHref,
                    Category = category
                });

                var ele_SubCategories = await elementHandle.QuerySelectorAllAsync("//../ul//a");
                foreach (var ele_SubCategory in ele_SubCategories)
                {
                    itemMenuHref = await ele_SubCategory.GetAttributeAsync("href");
                    if (itemMenuHref is null) continue;
                    var subCategory = await ele_SubCategory.InnerTextAsync();
                    articlePayloads.Add(new ArticlePayload
                    {
                        Category = $"{category} -> {subCategory}",
                        Url = itemMenuHref
                    });
                }
            }
        }
        
        // Crawl Benh Pho Bien
        var ele_RightMenus = await page.QuerySelectorAllAsync("//li[@id='text-9']//a");
        foreach (var elementHandle in ele_RightMenus)
        {
            var url = await elementHandle.GetAttributeAsync("href");
            var category = await elementHandle.InnerTextAsync();
            category = category.Trim();
            
            articlePayloads.Add(new ArticlePayload
            {
                Url = url,
                Category = $"Bệnh Phổ Biến -> {category}"
            });
        }

        return articlePayloads;
    }
}