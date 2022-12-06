using System.Collections.Concurrent;
using System.Globalization;
using Dasync.Collections;
using Flurl;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services;

public class CrawlSongKhoeMedPlusService : CrawlLCArticleBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        var menuItems = await CrawlMenuItems(page);
        var articlePayloads = await GetCrawlArticles(menuItems);

        return new CrawlArticlePayload
        {
            ArticlesPayload = articlePayloads,
            Url = url
        };
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(
        CrawlArticlePayload crawlArticlePayload)
    {
        var articles = new ConcurrentBag<ArticlePayload>();
        await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize)
                                 .ParallelForEachAsync(async arts => { 
                                      var stArticles = await CrawlArticles(arts.ToList()); 
                                      articles.AddRange(stArticles); 
                                  }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articles;
    }
    
    private async Task<List<ArticlePayload>> CrawlArticles(List<ArticlePayload> articlePayloads)
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot,
                                                                string.Empty, 0, string.Empty, string.Empty,
                                                                new List<CrawlerAccountCookie>(), false);
        var             articles   = new List<ArticlePayload>();
        using var       playwright = browserContext.Playwright;
        await using var browser    = browserContext.Browser;

        try
        {
            foreach (var articlePayload in articlePayloads)
            {
                var articlePage = await browserContext.BrowserContext.NewPageAsync();
                await articlePage.UnloadResource();
                var crawlArticle = await CrawlArticle(articlePage, articlePayload);
                articles.Add(crawlArticle);
            }
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, string.Empty);
        }
        finally
        {
            await browserContext.BrowserContext.CloseAsync();
        }

        return articles.Where(_ => _.Content != null).ToList();
    }
    
    private async Task<ArticlePayload> CrawlArticle(IPage articlePage, ArticlePayload articleInput)
    {
        var articlePayload = articleInput;
        
        try
        {
            await articlePage.GotoAsync(articlePayload.Url, new PageGotoOptions()
            {
                Timeout = 0
            });
            await articlePage.Wait();
            
            System.Console.WriteLine($"CRAWLING ARTICLE {articlePayload.Url}");

            if (articlePayload.Title is null)
            {
                var ele_Title = await articlePage.QuerySelectorAsync("//div[@class='entry-header']//*[@class='jeg_post_title']");
                if (ele_Title is not null)
                {
                    articlePayload.Title = await ele_Title.InnerTextAsync();
                }
            }

            var ele_Content = await articlePage.QuerySelectorAsync("//div[@id='ftwp-postcontent']"); 
            if (ele_Content is not null)
            {
                var ele_ShortDescription = await articlePage.QuerySelectorAsync("//div[contains(@class,'articleContainer__content')]");
                if (ele_ShortDescription is not null)
                {
                    articlePayload.ShortDescription = await ele_ShortDescription.InnerTextAsync();
                }
                else
                {
                    var shortDescription = await ele_Content.QuerySelectorAsync("//p[1]");
                    if (shortDescription is not null)
                    {
                        articlePayload.ShortDescription = await shortDescription.InnerTextAsync();
                    }

                    if (articlePayload.ShortDescription.IsNullOrWhiteSpace())
                    {
                        shortDescription = await ele_Content.QuerySelectorAsync("//p[2]");
                        if (shortDescription is not null)
                        {
                            articlePayload.ShortDescription = await shortDescription.InnerTextAsync();
                        }
                    }
                }

                var content = await ele_Content.InnerHTMLAsync();

                // remove advise
                var ele_Advise = await ele_Content.QuerySelectorAsync("//div[contains(@class,'code-block')]");
                if (ele_Advise is not null)
                {
                    content = content.Replace(await ele_Advise.InnerHTMLAsync(), string.Empty);
                }

                // remove "phu luc"
                var ele_Appendix = await ele_Content.QuerySelectorAsync("//div[contains(@class,'ftwp-in-post')]");
                if (ele_Appendix is not null)
                {
                    content = content.Replace(await ele_Appendix.InnerHTMLAsync(), string.Empty);
                }

                // remove see more
                var seeMoresKeys = new List<string>()
                {
                    "Xem thêm",
                    "Mời bạn đọc tham khảo thêm các bài viết mới nhất",
                    "Xem thêm bài viết",
                    "Nguồn tham khảo",
                    "bạn có thể xem thêm",
                    "Các bài viết cùng chủ đề có thể bạn quan tâm"
                };

                foreach (var seeMoreKey in seeMoresKeys)
                {
                    var ele_SeeMore = await ele_Content.QuerySelectorAsync($"//p[contains(text(),'{seeMoreKey}')]/following-sibling::ul");
                    if (ele_SeeMore is null)
                    {
                        ele_SeeMore = await ele_Content.QuerySelectorAsync($"//p/strong[contains(text(),'{seeMoreKey}')]/../following-sibling::ul");
                    }

                    if (ele_SeeMore is null)
                    {
                        ele_SeeMore = await ele_Content.QuerySelectorAsync($"//b[contains(text(),'{seeMoreKey}')]/../following-sibling::ul");
                    }

                    if (ele_SeeMore is not null)
                    {
                        content = content.Replace(await ele_SeeMore.InnerHTMLAsync(), string.Empty);
                    }

                    var ele_SeeMoreText = await ele_Content.QuerySelectorAsync($"//p[contains(text(),'{seeMoreKey}')]");
                    if (ele_SeeMoreText is null)
                    {
                        ele_SeeMoreText = await ele_Content.QuerySelectorAsync($"//p/strong[contains(text(),'{seeMoreKey}')]");
                    }
                    if (ele_SeeMoreText is null)
                    {
                        ele_SeeMoreText = await ele_Content.QuerySelectorAsync($"//b[contains(text(),'{seeMoreKey}')]");
                    }

                    if (ele_SeeMoreText is not null)
                    {
                        content = content.Replace(await ele_SeeMoreText.InnerHTMLAsync(), string.Empty);
                    }
                }

                // remove source
                var ele_Source = await ele_Content.QuerySelectorAsync("//p[contains(text(),'Nguồn:')]");
                if (ele_Source is not null)
                {
                    content = content.Replace(await ele_Source.InnerHTMLAsync(), string.Empty);
                }

                // remove tags
                var ele_TagContainer = await ele_Content.QuerySelectorAsync("//div[contains(@class,'jeg_post_tags')]");
                if (ele_TagContainer is not null)
                {
                    content = content.Replace(await ele_TagContainer.InnerHTMLAsync(), string.Empty);
                    var ele_Tags = await ele_TagContainer.QuerySelectorAllAsync("//a");
                    if (ele_Tags.IsNotNullOrEmpty())
                    {
                        articlePayload.Tags = new List<string>();
                        foreach (var ele_Tag in ele_Tags)
                        {
                            var tag = await ele_Tag.InnerTextAsync();
                            articlePayload.Tags.Add(tag);
                        }
                    }
                }

                articlePayload.Content = content.RemoveHrefFromA();
            }
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, string.Empty);
        }
        finally
        {
            await articlePage.CloseAsync();
        }

        return articlePayload;
    }

    private async Task<Dictionary<string, string>> CrawlMenuItems(IPage page)
    {
        var menuItems = new Dictionary<string, string>();
        var ele_MainMenus = await page.QuerySelectorAllAsync("//div[contains(@class,'jeg_header_wrapper')]//ul[contains(@class,'jeg_menu')]/li");

        foreach (var ele_MainMenu in ele_MainMenus)
        {
            var parentCategoryName = await ele_MainMenu.InnerTextAsync();
            if (parentCategoryName.Equals("Bảo Hiểm", StringComparison.InvariantCultureIgnoreCase) ||
                parentCategoryName.Equals("Thuốc A-Z", StringComparison.InvariantCultureIgnoreCase)) 
                continue;

            var ele_SubMenus = await ele_MainMenu.QuerySelectorAllAsync("//div[contains(@class,'sub-menu')]//ul/li/a");
            foreach (var ele_SubMenu in ele_SubMenus)
            {
                var childCategoryName = await ele_SubMenu.InnerTextAsync();
                var itemMenuHref = await ele_SubMenu.GetAttributeAsync("href");
                if (!itemMenuHref.IsNotNullOrEmpty())
                    continue;

                var category = parentCategoryName;
                if (!childCategoryName.Equals("All", StringComparison.InvariantCultureIgnoreCase))
                    category += $" -> {childCategoryName}";

                menuItems.Add(category, itemMenuHref);
            }
        }

        return menuItems;
    }

    private async Task<ConcurrentBag<ArticlePayload>> PerformGettingArticleUrl(string categoryUrl, string categoryName)
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot,
            string.Empty, 0, string.Empty, string.Empty, new List<CrawlerAccountCookie>());

        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        var isDateTimeConditionNotMet = false;
        
        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                var pageNumber = 1;
                while (true)
                {
                    if (isDateTimeConditionNotMet) 
                        break;
                    
                    var catePage = await browserContext.BrowserContext.NewPageAsync();
                    await catePage.UnloadResource();

                    try
                    {
                        var catePageHref = Url.Combine(categoryUrl, $"/page/{pageNumber}");
                        await catePage.GotoAsync(catePageHref, new PageGotoOptions()
                        {
                            Timeout = 0
                        });
                        System.Console.WriteLine($"CRAWL CHECKING PAGE: {catePageHref}");

                        var ele_NotFound = await catePage.QuerySelectorAsync("//h1[contains(@class,'jeg_archive_title') and text() = 'Page Not Found']");
                        if (ele_NotFound is not null)
                        {
                            break;
                        }

                        var ele_Articles = await catePage.QuerySelectorAllAsync("//div[contains(@class,'jeg_post')]/article");
                        if (!ele_Articles.Any())
                        {
                            break;
                        }

                        foreach (var ele_Article in ele_Articles)
                        {
                            try
                            {
                                // get created at (get from list articles)
                                var ele_CreatedAt   = await ele_Article.QuerySelectorAsync("//div[contains(@class,'jeg_post_meta')]/div[contains(@class,'jeg_meta_date')]");
                                var createdAt       = string.Empty;
                                var formatCreatedAt = DateTime.MinValue;
                                if (ele_CreatedAt is not null)
                                {
                                    createdAt = await ele_CreatedAt.InnerTextAsync();
                                    createdAt = createdAt.Replace("THÁNG MƯỜI HAI", "December")
                                                         .Replace("THÁNG MƯỜI MỘT", "November")
                                                         .Replace("THÁNG MƯỜI",     "October")
                                                         .Replace("THÁNG CHÍN",     "September")
                                                         .Replace("THÁNG TÁM",      "August")
                                                         .Replace("THÁNG BẢY",      "July")
                                                         .Replace("THÁNG SÁU",      "June")
                                                         .Replace("THÁNG NĂM",      "May")
                                                         .Replace("THÁNG TƯ",       "April")
                                                         .Replace("THÁNG BA",       "March")
                                                         .Replace("THÁNG HAI",      "February")
                                                         .Replace("THÁNG MỘT",      "January")
                                                         .Trim();
                                }

                                if (createdAt.IsNotNullOrEmpty())
                                {
                                    formatCreatedAt = DateTime.ParseExact(createdAt, "d MMMM, yyyy", CultureInfo.CurrentCulture);
                                    if (!await IsValidArticle(articlePayloads.Count, string.Empty, formatCreatedAt))
                                    {
                                        isDateTimeConditionNotMet = true;
                                        break;
                                    }
                                }
                                
                                // get url (get from list articles)
                                var ele_Url    = await ele_Article.QuerySelectorAsync("//div[contains(@class,'jeg_thumb')]/a");
                                var articleUrl = string.Empty;
                                if (ele_Url is not null)
                                {
                                    articleUrl = await ele_Url.GetAttributeAsync("href");
                                }

                                // get image (get from list articles)
                                var articleImageSrc = string.Empty;
                                var ele_Image = await ele_Article.QuerySelectorAsync("//div[contains(@class,'jeg_thumb')]//img");
                                if (ele_Image is not null)
                                {
                                    articleImageSrc = await ele_Image.GetAttributeAsync("src");
                                }

                                // get title (get from list articles)
                                string articleTitle = string.Empty;
                                var ele_Title = await ele_Article.QuerySelectorAsync("//h3[@class='jeg_post_title']");
                                if (ele_Title is not null)
                                {
                                    articleTitle = await ele_Title.InnerTextAsync();
                                }

                                var articlePayload = new ArticlePayload()
                                {
                                    Url = articleUrl,
                                    Title = articleTitle,
                                    FeatureImage = articleImageSrc,
                                    CreatedAt = formatCreatedAt,
                                    Category = categoryName
                                };

                                System.Console.WriteLine($"Url: {articlePayload.Url} - Category: {articlePayload.Category}");

                                articlePayloads.Add(articlePayload);
                            }
                            catch (Exception e)
                            {
                                await e.Log(string.Empty, string.Empty);
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
                        await catePage.CloseAsync();
                    }
                }
            }
        }

        return articlePayloads;
    }

    private async Task<List<ArticlePayload>> GetCrawlArticles(Dictionary<string, string> categoryUrls)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();

        await categoryUrls.Partition(1)
            .ParallelForEachAsync(async categoryUrls =>
                {
                    foreach (var categoryUrl in categoryUrls)
                    {
                        var categoryHref = categoryUrl.Value.Replace("http:", "https:");
                        System.Console.WriteLine($"CRAWL CHECKING CATE: {categoryHref}");

                        try
                        {
                            var articles = await PerformGettingArticleUrl(categoryHref, categoryUrl.Key);
                            articlePayloads.AddRange(articles);
                        }
                        catch (Exception e)
                        {
                            await e.Log(string.Empty, string.Empty);
                        }
                    }
                }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articlePayloads.ToList();
    }
}