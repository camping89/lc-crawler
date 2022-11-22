using System.Collections.Concurrent;
using Dasync.Collections;
using Flurl;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using LC.Crawler.Core.Playwrights;
using Microsoft.Playwright;

namespace LC.Crawler.Console.Services;

public class CrawlSieuThiSongKhoeArticleService : CrawlLCArticleBaseService
{
    private string _sTSKArticleUrl;

    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        _sTSKArticleUrl = Url.Combine(url, "blog");
        var stskArticles = await GetArticlePayloads();

        return new CrawlArticlePayload
        {
            Url = url,
            ArticlesPayload = stskArticles.ToList()
        };
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var articles = new ConcurrentBag<ArticlePayload>();
        await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize)
            .ParallelForEachAsync(async arts => { 
                var stArticles = await CrawlArticles(arts.ToList()); 
                articles.AddRange(stArticles); 
            }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articles;
    }
    
    private async Task<ConcurrentBag<ArticlePayload>> GetArticlePayloads()
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot,
                                                                string.Empty, 0, string.Empty, string.Empty,
                                                                new List<CrawlerAccountCookie>(), false);
        using var       playwright   = browserContext.Playwright;
        await using var browser      = browserContext.Browser;
        var             homePage     = await browserContext.BrowserContext.NewPageAsync();
        await homePage.UnloadResource();
        var             stskArticles = new ConcurrentBag<ArticlePayload>();
        
        try
        {
            await homePage.GotoAsync(_sTSKArticleUrl, new PageGotoOptions()
            {
                Timeout = 0
            });
            await homePage.Wait();

            var ele_Categories = await homePage.QuerySelectorAllAsync("//li[contains(@class,'cat-item')]/a");
            await ele_Categories.Partition(1)
                                .ParallelForEachAsync(async cates => { 
                                     foreach (var cate in cates) 
                                     { 
                                         var cateUrl  = await cate.GetAttributeAsync("href"); 
                                         var cateName = await cate.InnerTextAsync(); 
                                         var articles = await GetArticleLinks(cateUrl, cateName);
                                         stskArticles.AddRange(articles);
                                     }}, GlobalConfig.CrawlConfig.Crawl_MaxThread);
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, string.Empty);
        }
        finally
        {
            await homePage.CloseAsync();
            await browserContext.BrowserContext.CloseAsync();
        }

        return stskArticles;
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
                var articlePage  = await browserContext.BrowserContext.NewPageAsync();
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

        // remove article with content is null because that is product
        articles = articles.Where(_ => _.Content != null).ToList();
        
        return articles;
    }

    private async Task<List<ArticlePayload>> GetArticleLinks(string url, string cateName)
    {
        var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty, 0,
            string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
        var articlePayloads = new List<ArticlePayload>();

        using var       playwright = browserContext.Playwright;
        await using var browser    = browserContext.Browser;

        var page = 1;
        while (true)
        {
            if (!await IsValidArticle(articlePayloads.Count, string.Empty, null))
            {
                break;
            }
            
            var pageSite = await browserContext.BrowserContext.NewPageAsync();
            await pageSite.UnloadResource();
            
            try
            {
                var pagingUrl = Url.Combine(url, $"/page/{page}");
                await pageSite.GotoAsync(pagingUrl, new PageGotoOptions()
                {
                    Timeout = 0
                });
                
                System.Console.WriteLine($"CRAWLING PAGE {pagingUrl}");

                var ele_Articles = await pageSite.QuerySelectorAllAsync("//div[@class='blog-list']//div[@class='blog-item']");
                if (!ele_Articles.Any())
                {
                    break;
                }
                
                foreach (var ele_Article in ele_Articles)
                {
                    var article = new ArticlePayload
                    {
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    var ele_Url = await ele_Article.QuerySelectorAsync("//a");
                    if (ele_Url is not null)
                    {
                        article.Url = await ele_Url.GetAttributeAsync("href");
                    }

                    var ele_Title = await ele_Article.QuerySelectorAsync("//h2[@class='title']");
                    if (ele_Title is not null)
                    {
                        article.Title = await ele_Title.InnerTextAsync();
                    }

                    var ele_ShortDesc = await ele_Article.QuerySelectorAsync("//div[@class='des']");
                    if (ele_ShortDesc is not null)
                    {
                        article.ShortDescription = (await ele_ShortDesc.InnerHTMLAsync()).RemoveHrefFromA();
                    }

                    article.Category = cateName;
                    articlePayloads.Add(article);
                }
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
            finally
            {
                page++;
                await pageSite.CloseAsync();
            }
        }

        return articlePayloads;
    }

    private async Task<bool> IsProduction(IPage articlePage)
    {
        var categories     = new List<string>();
        var ele_Categories = await articlePage.QuerySelectorAllAsync("//nav[@class='woocommerce-breadcrumb']/span/a[not(text() = 'Trang Chủ')]");
        if (ele_Categories.Any())
        {
            foreach (var ele_Category in ele_Categories)
            {
                categories.Add(await ele_Category.InnerTextAsync());
            }
        }

        return categories.Any(_ => _ == "Sản Phẩm");
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

            // Dont get the product in the article, this issue from sieuthisongkhoe
            var isProduction = await IsProduction(articlePage);
            if (isProduction)
            {
                return articlePayload;
            }

            var ele_Content = await articlePage.QuerySelectorAsync("//div[@class='news-detail-content']");
            if (ele_Content is not null)
            {
                var content = await ele_Content.InnerHTMLAsync();
                
                // Remove Xem them
                var ele_ReadMore = await ele_Content.QuerySelectorAsync("//strong[contains(text(), 'Xem thêm:')]");
                if (ele_ReadMore is not null)
                {
                    content = content.Replace(await ele_ReadMore.InnerHTMLAsync(), string.Empty);
                }
                
                articlePayload.Content = content.RemoveHrefFromA();
            }

            var ele_Thum = await articlePage.QuerySelectorAsync("//div[@class='news-detail-head']//img");
            if (ele_Thum is not null)
            {
                articlePayload.FeatureImage = await ele_Thum.GetAttributeAsync("src");
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
}