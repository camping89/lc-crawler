using System.Collections.Concurrent;
using System.Globalization;
using Dasync.Collections;
using LC.Crawler.Client.Entities;
using LC.Crawler.Console.Services.Helper;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace LC.Crawler.Console.Services;

public class CrawlSucKhoeDoiSongService : CrawlLCArticleBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        var crawlArticlePayload = await GetArticlePayload(page, url);
        return new CrawlArticlePayload
        {
            Url = url,
            ArticlesPayload = crawlArticlePayload
        };
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var articlePayloads     = new ConcurrentBag<ArticlePayload>();
        await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async crawlArticles =>
        {
            var articles = await GetCrawlSKDSArticles(crawlArticles.ToList());
            articlePayloads.AddRange(articles);
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articlePayloads;
    }
    
    private async Task<List<ArticlePayload>> GetCrawlSKDSArticles(List<ArticlePayload> crawlArticles)
    {
        var             articles       = new List<ArticlePayload>();
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0, 
                                                                       string.Empty, string.Empty, new List<CrawlerAccountCookie>());

        using (browserContext.Playwright)
        {
            await using (browserContext.Browser)
            {
                foreach (var crawlArticle in crawlArticles)
                {
                    var articlePage = await browserContext.BrowserContext.NewPageAsync();
                    await articlePage.UnloadResource();
                    try
                    {
                        var article = await CrawlArticle(articlePage, crawlArticle);
                        System.Console.WriteLine(JsonConvert.SerializeObject(article));
                        articles.Add(article);
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
        
        return articles;
    }

    private async Task<ArticlePayload> CrawlArticle(IPage page, ArticlePayload articleInput)
    {
        await page.GotoAsync(articleInput.Url, new PageGotoOptions()
        {
            Timeout = 0
        });
        await page.WaitForSelectorAsync("//h1[@data-role='title']");
        
        try
        {
            articleInput.Category = string.Empty;
            var ele_ParentCate = await page.QuerySelectorAsync("//div[contains(@class,'box-breadcrumb-name')]");
            if (ele_ParentCate is not null)
            {
                var textInfo   = new CultureInfo("en-US", false).TextInfo;
                var parentCate = textInfo.ToTitleCase((await ele_ParentCate.InnerTextAsync()).ToLower()); 
                articleInput.Category = parentCate;
            }

            var ele_ChildCate = await page.QuerySelectorAsync("//div[contains(@class,'box-breadcrumb-sub')]/a[@class='active']");
            if (ele_ChildCate is not null)
            {
                if (articleInput.Category.IsNotNullOrEmpty())
                {
                    articleInput.Category += " -> ";
                }
                
                articleInput.Category += await ele_ChildCate.InnerTextAsync();
            }
            
            var ele_ShortDescription = await page.QuerySelectorAsync("//h2[@data-role='sapo']");
            if (ele_ShortDescription is not null)
            {
                var shortDescription = await ele_ShortDescription.InnerTextAsync();
                articleInput.ShortDescription = shortDescription.RemoveHrefFromA();
            }

            var ele_Content = await page.QuerySelectorAsync("//div[contains(@class, 'detail-content')]");
            if (ele_Content is not null)
            {
                var content = await ele_Content.InnerHTMLAsync();
                
                // remove related box news
                var ele_RelatedBoxNews = await ele_Content.QuerySelectorAsync("//div[contains(@class,'VCSortableInPreviewMode') and @type='RelatedNewsBox']");
                if (ele_RelatedBoxNews is not null)
                {
                    var relatedBoxNews = await ele_RelatedBoxNews.InnerHTMLAsync();
                    content = content.Replace(relatedBoxNews, string.Empty);
                }
                
                // remove related one news
                var ele_RelatedOneNews = await ele_Content.QuerySelectorAllAsync("//div[contains(@class,'VCSortableInPreviewMode') and @type='RelatedOneNews']");
                if (ele_RelatedOneNews.IsNotNullOrEmpty())
                {
                    foreach (var ele_RelatedOneNew in ele_RelatedOneNews)
                    {
                        var relatedOneNew = await ele_RelatedOneNew.InnerHTMLAsync();
                        content = content.Replace(relatedOneNew, string.Empty);
                    }
                }
                
                // remove ads div
                var adsIds = new List<string>() { "zone-krlv706p", "zone-krlutq8c" };
                foreach (var adsId in adsIds)
                {
                    var ele_Ads = await ele_Content.QuerySelectorAsync($"//div[@id='{adsId}']");
                    if (ele_Ads is not null)
                    {
                        var ads = await ele_Ads.InnerHTMLAsync();
                        content = content.Replace(ads, string.Empty);
                    }
                }

                var ele_ReadMores = await ele_Content.QuerySelectorAllAsync(
                    "//*[contains(text(),'xem thêm') or contains(text(),'Xem thêm') or contains(text(),'XEM THÊM') or contains(text(),'xem tiếp') or contains(text(),'Xem tiếp') or contains(text(),'XEM TIẾP')]");
                foreach (var elementHandle in ele_ReadMores)
                {
                    var readMore = await elementHandle.InnerHTMLAsync();
                    content = content.Replace(readMore, string.Empty);
                }

                var ele_Videos = await ele_Content.QuerySelectorAllAsync(
                    "//div[contains(@class,'VCSortableInPreviewMode') and boolean(@type='BoxTable') = false or contains(@class,'inread') or contains(@data-id,'stream')]");
                foreach (var elementHandle in ele_Videos)
                {
                    var video = await elementHandle.InnerHTMLAsync();
                    content = content.Replace(video, string.Empty);
                }

                // add header to content
                articleInput.Content = content.RemoveHrefFromA();
            }

            var ele_Tags = await page.QuerySelectorAllAsync("//ul[contains(@class,'detail-tag-list')]/li/a");
            if (ele_Tags.IsNotNullOrEmpty())
            {
                articleInput.Tags = new List<string>();
                foreach (var ele_Tag in ele_Tags)
                {
                    var tag = await ele_Tag.InnerTextAsync();
                    articleInput.Tags.Add(tag);
                }
            }
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, string.Empty);
        }

        return articleInput;
    }

    private async Task<List<ArticlePayload>> GetArticlePayload(IPage page, string url)
    {
        var articlesPayload = new List<ArticlePayload>();
        var categoryUrls    = await GetCateUrls(url);
        categoryUrls.Add("Đời sống", "https://suckhoedoisong.vn/doi-song.htm");
        categoryUrls.Add("Văn hóa – Giải trí", "https://suckhoedoisong.vn/van-hoa-giai-tri.htm");

        foreach (var categoryUrl in categoryUrls)
        {
            var articlesCategory = new List<ArticlePayload>();
            var categoryName = categoryUrl.Key;
            await page.GotoAsync(categoryUrl.Value);
            
            System.Console.WriteLine($"====================={categoryUrl.Key}: Trying to CRAWL url {categoryUrl.Value}");
            
            var count = 0;
            var height = 100;
            while (true)
            {
                await page.Scroll(height);
                await page.Wait(5000);
                var ele_News= await page.QuerySelectorAllAsync("//div[@class='list__main']//div[@class='box-home-focus']//a[@data-linktype='newsdetail']");
                ele_News = ele_News.Skip(count).ToList();
                if(!ele_News.Any()) break;

                foreach (var ele_New in ele_News)
                {
                    var ele_Time = await ele_New.QuerySelectorAsync("//../../span[contains(@class,'time-ago')]");
                    if (ele_Time is null) continue;
                    
                    var timeString = await ele_Time.GetAttributeAsync("title");
                    var dateTime = GetDateTime(timeString);
                    
                    if(! await IsValidArticle(articlesCategory.Count, string.Empty, dateTime)) continue;

                    var articlePayload = await InitCrawlArticlePayload(ele_New, categoryName, url, dateTime);
                    
                    var ele_Image = await ele_New.QuerySelectorAsync("//../..//img[@data-type='avatar']");
                    if (ele_Image is not null)
                    {
                        var imageUrl = await ele_Image.GetAttributeAsync("src");
                        articlePayload.FeatureImage = imageUrl;
                    }
                    
                    articlesCategory.Add(articlePayload);
                }

                count += ele_News.Count;
                height += 100;
            }

            count = 0;
            while (true)
            {
                await page.Scroll(height);
                await page.Wait(5000);
                var ele_News = await page.QuerySelectorAllAsync("//div[@class='list__main']//div[@class='box-category']//a[@class='box-category-link-with-avatar']");
                
                ele_News = ele_News.Skip(count).ToList();
                if(!ele_News.Any()) break;
                
                foreach (var ele_New in ele_News)
                {
                    var ele_Time = await ele_New.QuerySelectorAsync("//..//span[contains(@class,'time-ago')]");
                    if (ele_Time is null) continue;
                    
                    var timeString = await ele_Time.GetAttributeAsync("title");
                    var dateTime = GetDateTime(timeString);
                    
                    if(! await IsValidArticle(articlesCategory.Count, string.Empty, dateTime)) continue;

                    var articlePayload = await InitCrawlArticlePayload(ele_New, categoryName, url, dateTime);
                    
                    var ele_Image = await ele_New.QuerySelectorAsync("//img");
                    if (ele_Image is not null)
                    {
                        var imageUrl = await ele_Image.GetAttributeAsync("src");
                        articlePayload.FeatureImage = imageUrl;
                    }
                    
                    articlesCategory.Add(articlePayload);
                }
                
                count += ele_News.Count;
                height += 100;
            }


            count = 0;
            bool isDateTimeConditionNotMet = false;
            var  notFoundNewsCount         = 0;
            while (true)
            {
                if(isDateTimeConditionNotMet) break;
                
                CRAWL:

                if (notFoundNewsCount > 10) break;
                
                await page.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)");
                await page.Wait(10000);
                var ele_News = await page.QuerySelectorAllAsync("//div[@class='list__main']//div[@class='box-category timeline']//a[@class='box-category-link-with-avatar']");
                
                ele_News = ele_News.Skip(count).ToList();
                if (!ele_News.Any())
                {
                    height += 100;
                    notFoundNewsCount++;
                    var ele_ReadMore = await page.QuerySelectorAsync("//a[@title='Xem thêm']");
                    if (ele_ReadMore is not null)
                    {
                        await ele_ReadMore.Click();
                        await page.Wait(2000);
                    }
                    goto CRAWL;
                }
                
                foreach (var ele_New in ele_News)
                {
                    var ele_Time = await ele_New.QuerySelectorAsync("//..//span[contains(@class,'time-ago')]");
                    if (ele_Time is null) continue;
                    
                    var timeString = await ele_Time.GetAttributeAsync("title");
                    var dateTime = GetDateTime(timeString);

                    if (!await IsValidArticle(articlesCategory.Count, string.Empty, dateTime))
                    {
                        isDateTimeConditionNotMet = true;
                        break;
                    }
                    var articlePayload = await InitCrawlArticlePayload(ele_New, categoryName, url, dateTime);
                    
                    var ele_Image = await ele_New.QuerySelectorAsync("//img");
                    if (ele_Image is not null)
                    {
                        var imageUrl = await ele_Image.GetAttributeAsync("src");
                        articlePayload.FeatureImage = imageUrl;
                    }
                    
                    articlesCategory.Add(articlePayload);
                }
                
                count += ele_News.Count;
                height += 100;
            }
            
            articlesPayload.AddRange(articlesCategory);
        }

        return articlesPayload;
    }

    private async Task<Dictionary<string, string>> GetCateUrls(string url)
    {
        var categoryUrls = new Dictionary<string, string>();
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
                var catePage = await browserContext.BrowserContext.NewPageAsync();
                await catePage.UnloadResource();

                try
                {
                    await catePage.GotoAsync(url, new PageGotoOptions()
                    {
                        Timeout = 0
                    });
                    var ele_Categories =
                        await catePage.QuerySelectorAllAsync("//div[@class='header__nav']//ul/li/a[boolean(@title='Trang chủ') = false and boolean(@title='Thời sự') = false]");
                    

                    foreach (var category in ele_Categories)
                    {
                        var categoryName = await category.InnerTextAsync();
                        var categoryHref = await category.GetAttributeAsync("href");
                        categoryHref = Flurl.Url.Combine(url, categoryHref);
                        categoryUrls.Add(categoryName, categoryHref);
                        System.Console.WriteLine($"FOUND {categoryName}: {categoryHref}");

                        // go to each cate to get sub cate
                        var subCatePage = await browserContext.BrowserContext.NewPageAsync();
                        await subCatePage.UnloadResource();

                        try
                        {
                            await subCatePage.GotoAsync(categoryHref, new PageGotoOptions() { Timeout = 0 });
                            var ele_ChildCates = await subCatePage.QuerySelectorAllAsync("//div[contains(@class,'box-breadcrumb-sub')]/a");

                            foreach (var ele_ChildCate in ele_ChildCates)
                            {
                                var subCateHref = await ele_ChildCate.GetAttributeAsync("href");
                                subCateHref = Flurl.Url.Combine(url, subCateHref);
                                var subCateName = await ele_ChildCate.InnerTextAsync();
                                categoryUrls.Add($"{categoryName} -> {subCateName}", subCateHref);
                                System.Console.WriteLine($"FOUND {categoryName} -> {subCateName}: {subCateHref}");
                            }
                        }
                        catch (Exception e)
                        {
                            await e.Log(string.Empty, string.Empty);
                        }
                        finally
                        {
                            await subCatePage.CloseAsync();
                        }
                    }
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                }
                finally
                {
                    await catePage.CloseAsync();
                }
            }
        }

        return categoryUrls;
    }

    private static async Task<ArticlePayload> InitCrawlArticlePayload(IElementHandle news, string categoryName, string mainUrl, DateTime createdAt)
    {
        var url = await news.GetAttributeAsync("href");
        var title = await news.GetAttributeAsync("title");

        var crawlArticlePayload = new ArticlePayload
        {
            Category = categoryName
        };
        if (url is not null)
        {
            crawlArticlePayload.Url = Flurl.Url.Combine(mainUrl, url);
        }

        if (title is not null)
        {
            crawlArticlePayload.Title = title;
        }
        
        crawlArticlePayload.CreatedAt = createdAt;

        return crawlArticlePayload;
    }

    

    private DateTime GetDateTime(string dateTimeString)
    {
        var dateTime = DateTime.ParseExact(dateTimeString, "dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

        return dateTime;
    }
    
}