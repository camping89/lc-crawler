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

public class CrawlAloBacSiService : CrawlLCArticleBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(IPage page, string url)
    {
        var categoryItems = await CrawlCategory(page, url);

        var crawlArticlePayload = new CrawlArticlePayload
        {
            Url = url,
            ArticlesPayload = categoryItems
        };

        return crawlArticlePayload;
    }

    private async Task<List<ArticlePayload>> CrawlCategory(IPage page, string url)
    {
        var ele_Categories =
            await page.QuerySelectorAllAsync(
                "//div[@class='container']//a[@class='nav-link' and boolean(text()='Trang chủ') = false and boolean(text()='Khám bệnh online') = false and boolean(text()='Dịch vụ y') = false  and boolean(text()='Cơ sở y tế') = false and boolean(text()='Bác sĩ') = false and boolean(text()='Thuốc') = false]");

        var categoryUrls = new List<KeyValuePair<string, string>>();
        foreach (var elementHandle in ele_Categories)
        {
            var categoryUrl = await elementHandle.GetAttributeAsync("href");
            if (!categoryUrl.Contains("alobacsi.com"))
            {
                categoryUrl = Url.Combine(url, categoryUrl);
            }

            var categoryName = await elementHandle.InnerTextAsync();

            categoryUrls.Add(new KeyValuePair<string, string>(categoryName, categoryUrl));
        }

        categoryUrls.Add(new KeyValuePair<string, string>("Bệnh tìm kiếm nhiều", "https://alobacsi.com/benh-duoc-tim-kiem-nhieu-nhat-c984/"));
        categoryUrls.Add(new KeyValuePair<string, string>("Dinh dưỡng", "https://alobacsi.com/dinh-duong-c164/"));
        categoryUrls.Add(new KeyValuePair<string, string>("Tiêm chủng - Xét nghiệm", "https://alobacsi.com/tiem-chung-xet-nghiem-c243/"));
        categoryUrls.Add(new KeyValuePair<string, string>("Y học Cổ truyền", "https://alobacsi.com/y-hoc-co-truyen-c802/"));
        categoryUrls.Add(new KeyValuePair<string, string>("Khỏe đẹp", "https://alobacsi.com/khoe-dep-c165/"));

        var articlePayloads = new List<ArticlePayload>();
        foreach (var categoryUrl in categoryUrls)
        {
            var categoryArticles = new ConcurrentBag<ArticlePayload>();
            await page.GotoAsync(categoryUrl.Value, new PageGotoOptions {Timeout = 60000});
            await page.Wait(500);

            if (categoryUrl.Key.Contains("Video"))
            {
                await CrawlVideos(page, categoryArticles, categoryUrl);
            }
            else
            {
                await PerformCrawlArticleUrl(page, url, categoryUrl, categoryArticles);
            }


            articlePayloads.AddRange(categoryArticles);
        }

        return articlePayloads;
    }

    private async Task PerformCrawlArticleUrl(IPage page, string url, KeyValuePair<string, string> categoryUrl, ConcurrentBag<ArticlePayload> categoryArticles)
    {
        var ele_SubMenus = await page.QuerySelectorAllAsync("//ol[@class='breadcrumb']//a[boolean(@data-toggle) = false]");
        if (ele_SubMenus.Any())
        {
            var subCategoryUrls = new List<KeyValuePair<string, string>>();
            foreach (var elementHandle in ele_SubMenus)
            {
                var subCategory = await elementHandle.InnerTextAsync();
                subCategory = $"{categoryUrl.Key} -> {subCategory.Trim()}";
                var subUrl = await elementHandle.GetAttributeAsync("href");
                if (!subUrl.Contains("alobacsi.com"))
                {
                    subUrl = Url.Combine(url, subUrl);
                }

                subCategoryUrls.Add(new KeyValuePair<string, string>(subCategory, subUrl));
            }

            await CrawlNonVideos(page, categoryArticles, categoryUrl);

            await subCategoryUrls.ParallelForEachAsync(async subCategoryUrl =>
            {
                var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty,
                    0, string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
                using (browserContext.Playwright)
                {
                    await using (browserContext.Browser)
                    {
                        var page = await browserContext.BrowserContext.NewPageAsync();
                        await page.UnloadResource();

                        try
                        {
                            var subArticles = new ConcurrentBag<ArticlePayload>();
                            await page.GotoAsync(subCategoryUrl.Value, new PageGotoOptions {WaitUntil = WaitUntilState.DOMContentLoaded});
                            await page.Wait(500);

                            await CrawlNonVideos(page, subArticles, subCategoryUrl);

                            ele_SubMenus = await page.QuerySelectorAllAsync("//ol[@class='breadcrumb']//a[boolean(@data-toggle) = false]");
                            if (ele_SubMenus.Any())
                            {
                                await PerformCrawlArticleUrl(page, url, subCategoryUrl, subArticles);
                            }

                            categoryArticles.AddRange(subArticles);
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
            }, GlobalConfig.CrawlConfig.Crawl_MaxThread);
        }
        else
        {
            await CrawlNonVideos(page, categoryArticles, categoryUrl);
        }
    }

    private async Task CrawlNonVideos(IPage page, ConcurrentBag<ArticlePayload> categoryArticles, KeyValuePair<string, string> categoryUrl)
    {
        int pageNumber = 1;
        var isConditionMet = false;
        do
        {
            var urlByPage = Url.Combine(categoryUrl.Value, $"p-{pageNumber}/");
            await page.GotoAsync(urlByPage, new PageGotoOptions {WaitUntil = WaitUntilState.DOMContentLoaded});
            await page.Wait(500);

            System.Console.WriteLine($"CRAWL Category: {categoryUrl.Key} -> {urlByPage}");

            var ele_TopArticle = await page.QuerySelectorAsync("//div[contains(@class,'top-article')]/a");
            if (ele_TopArticle is not null)
            {
                var topArticleUrl = await ele_TopArticle.GetAttributeAsync("href");
                if (topArticleUrl.Contains(".html"))
                {
                    var ele_Img = await ele_TopArticle.QuerySelectorAsync("//img");
                    var img = await ele_Img.GetAttributeAsync("src");
                    var ele_Title = await ele_TopArticle.QuerySelectorAsync("//h2");
                    var title = await ele_Title.InnerTextAsync();
                    title = title.Trim();
                    categoryArticles.Add(new ArticlePayload
                    {
                        Title = title,
                        Category = categoryUrl.Key,
                        Url = topArticleUrl,
                        FeatureImage = img
                    });
                }
                else
                {
                    return;
                }
            }

            var ele_t3News = await page.QuerySelectorAllAsync("//div[@class='t3-news']//article[@class='card']");
            // var ele_t3News = await page.QuerySelectorAllAsync("//div[@class='t3-news']//div[contains(@class,'card-body')]/a");
            foreach (var elementHandle in ele_t3News)
            {
                var ele_articleUrl = await elementHandle.QuerySelectorAsync("//div[contains(@class,'card-body')]/a");
                var articleUrl = await ele_articleUrl.GetAttributeAsync("href");
                var ele_Title = await elementHandle.QuerySelectorAsync("//h4");
                var title = await ele_Title.InnerTextAsync();
                var ele_Image = await elementHandle.QuerySelectorAsync("//img");
                var img = await ele_Image.GetAttributeAsync("src");
                categoryArticles.Add(new ArticlePayload
                {
                    Title = title,
                    Category = categoryUrl.Key,
                    Url = articleUrl,
                    FeatureImage = img
                });
            }

            var ele_Media = await page.QuerySelectorAllAsync("//ul[contains(@class,'list-media')]//div[@class='media']");
            foreach (var elementHandle in ele_Media)
            {
                var ele_Url = await elementHandle.QuerySelectorAsync("//div[@class='media-body']/a");
                var articleUrl = await ele_Url.GetAttributeAsync("href");
                var ele_Title = await elementHandle.QuerySelectorAsync("//h2[@class='media-title']");
                var title = await ele_Title.InnerTextAsync();
                var ele_Image = await elementHandle.QuerySelectorAsync("//div/a/img");
                var image = await ele_Image.GetAttributeAsync("src");
                var ele_Date = await elementHandle.QuerySelectorAsync("//span[contains( @class,'media-date')]");
                var date = GetDateTime(await ele_Date.InnerTextAsync());
                if (!await IsValidArticle(categoryArticles.Count, string.Empty, date))
                {
                    isConditionMet = true;
                    break;
                }

                categoryArticles.Add(new ArticlePayload
                {
                    Category = categoryUrl.Key,
                    Url = articleUrl,
                    Title = title,
                    FeatureImage = image
                });
            }

            pageNumber++;
        } while (isConditionMet == false);
    }

    private async Task CrawlVideos(IPage page, ConcurrentBag<ArticlePayload> categoryArticles, KeyValuePair<string, string> categoryUrl)
    {
        int pageNumber = 1;
        var isConditionMet = false;
        do
        {
            var urlByPage = Url.Combine(categoryUrl.Value, $"p-{pageNumber}/");
            await page.GotoAsync(urlByPage, new PageGotoOptions {Timeout = 60000});
            await page.Wait(500);

            var ele_Top = await page.QuerySelectorAsync("//div[@class='mb-3']");
            if (ele_Top is not null)
            {
                var ele_Url = await ele_Top.QuerySelectorAsync("//a");
                var topUrl = await ele_Url.GetAttributeAsync("href");
                if (topUrl.Contains(".html"))
                {
                    var ele_Image = await ele_Top.QuerySelectorAsync("//a/article//img");
                    var image = await ele_Image.GetAttributeAsync("src");
                    var ele_Title = await ele_Top.QuerySelectorAsync("//a/article//h2");
                    var title = await ele_Title.InnerTextAsync();
                    categoryArticles.Add(new ArticlePayload
                    {
                        Category = categoryUrl.Key,
                        Title = title,
                        FeatureImage = image,
                        Url = topUrl
                    });
                }
                else
                {
                    return;
                }
            }

            var ele_t3News = await page.QuerySelectorAllAsync("//div[contains(@class,'t3-news')]//article[contains(@class,'card')]");
            foreach (var elementHandle in ele_t3News)
            {
                var ele_Url = await elementHandle.QuerySelectorAsync("//..");
                var videoUrl = await ele_Url.GetAttributeAsync("href");
                var ele_Tirle = await elementHandle.QuerySelectorAsync("//h4");
                var title = await ele_Tirle.InnerTextAsync();
                var ele_Image = await elementHandle.QuerySelectorAsync("//img");
                var image = await ele_Image.GetAttributeAsync("src");
                categoryArticles.Add(new ArticlePayload
                {
                    Category = categoryUrl.Key,
                    Title = title,
                    FeatureImage = image,
                    Url = videoUrl
                });
            }


            var ele_Media = await page.QuerySelectorAllAsync("//ul[contains(@class,'list-media')]//div[@class='media']");
            foreach (var elementHandle in ele_Media)
            {
                var ele_Url = await elementHandle.QuerySelectorAsync("//div[@class='media-body']/a");
                var articleUrl = await ele_Url.GetAttributeAsync("href");
                var ele_Title = await elementHandle.QuerySelectorAsync("//h3[@class='media-title']");
                var title = await ele_Title.InnerTextAsync();
                var ele_Image = await elementHandle.QuerySelectorAsync("//a//img");
                var image = await ele_Image.GetAttributeAsync("src");
                var ele_Date = await elementHandle.QuerySelectorAsync("//span[contains( @class,'media-date')]");
                var date = GetDateTime(await ele_Date.InnerTextAsync());
                if (!await IsValidArticle(categoryArticles.Count, string.Empty, date))
                {
                    isConditionMet = true;
                    break;
                }

                categoryArticles.Add(new ArticlePayload
                {
                    Category = categoryUrl.Key,
                    Url = articleUrl,
                    Title = title,
                    FeatureImage = image
                });
            }

            pageNumber++;
        } while (isConditionMet == false);
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async articlesPayload =>
        {
            var browserContext = await PlaywrightHelper.InitBrowser(GlobalConfig.CrawlConfig.UserDataDirRoot, string.Empty,
                0, string.Empty, string.Empty, new List<CrawlerAccountCookie>(), false);
            using (browserContext.Playwright)
            {
                await using (browserContext.Browser)
                {
                    var page = await browserContext.BrowserContext.NewPageAsync();
                    await page.UnloadResource();

                    foreach (var articlePayload in articlesPayload)
                    {
                        var articlePage = await browserContext.BrowserContext.NewPageAsync();
                        await articlePage.UnloadResource();
                        RETRY:
                        try
                        {
                            await articlePage.GotoAsync(articlePayload.Url);
                            await articlePage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                            await articlePage.Wait(500);

                            // 2 case main-post and main-view
                            var ele_Title = await articlePage.QuerySelectorAsync("//div[contains(@class,'main-post')]//h1") ??
                                            await articlePage.QuerySelectorAsync("//div[contains(@class,'main-view')]//h1[contains(@class,'media-title')]");

                            if (ele_Title is not null)
                            {
                                articlePayload.Title = await ele_Title.InnerTextAsync();
                            }

                            var ele_ShortDescription = await articlePage.QuerySelectorAsync("//div[contains(@class,'main-post')]//div[contains(@class,'sapo_detail')]") ??
                                                       await articlePage.QuerySelectorAsync("//div[contains(@class,'main-view')]//div[contains(@class,'media-body')]//ul");
                            if (ele_ShortDescription is not null)
                            {
                                articlePayload.ShortDescription = await ele_ShortDescription.InnerTextAsync();
                                articlePayload.ShortDescription = articlePayload.ShortDescription.RemoveHrefFromA();
                            }

                            var ele_Content = await articlePage.QuerySelectorAsync("//div[contains(@class,'main-post')]//div[@id='main_detail']") ??
                                              await articlePage.QuerySelectorAsync("//div[contains(@class,'main-view')]//div[contains(@class,'post-content')]");
                            if (ele_Content is not null)
                            {
                                articlePayload.Content = await ele_Content.InnerHTMLAsync();
                            }
                            else
                            {
                                var content = string.Empty;
                                var ele_Chat = await articlePage.QuerySelectorAsync("//div[contains(@class,'main-post')]//div[contains(@class,'chat-message')]");
                                if (ele_Chat is not null)
                                {
                                    content = await ele_Chat.InnerHTMLAsync();
                                    content += Environment.NewLine;
                                }

                                var ele_PostContent = await articlePage.QuerySelectorAsync("//div[contains(@class,'main-post')]//div[contains(@class,'post-content')]");
                                if (ele_PostContent is not null)
                                {
                                    content += await ele_PostContent.InnerHTMLAsync();
                                }

                                articlePayload.Content = content;
                            }

                            if (articlePayload.Content.IsNullOrWhiteSpace())
                            {
                                var ele_MediaContent = await articlePage.QuerySelectorAsync("//div[@class='media-body']");
                                if (ele_MediaContent is not null)
                                {
                                    articlePayload.Content = await ele_MediaContent.InnerHTMLAsync();
                                }
                            }

                            if (!articlePayload.CreatedAt.HasValue)
                            {
                                var ele_Date = await articlePage.QuerySelectorAsync("//span[contains(@class,'post-date')]");
                                if (ele_Date is not null)
                                {
                                    var date = GetDateTime(await ele_Date.InnerTextAsync());
                                    articlePayload.CreatedAt = date;
                                }
                            }

                            var ele_Tags = await articlePage.QuerySelectorAllAsync("//div[contains(@class,'tag-box')]//a");
                            if (ele_Tags.IsNotNullOrEmpty())
                            {
                                articlePayload.Tags = new List<string>();
                                foreach (var ele_Tag in ele_Tags)
                                {
                                    var tag = await ele_Tag.InnerTextAsync();
                                    tag = tag.Replace("#", string.Empty);
                                    articlePayload.Tags.Add(tag);
                                }
                            }

                            articlePayload.Content = articlePayload.Content.RemoveHrefFromA();

                            System.Console.WriteLine($"CRAWL Details: {articlePayload.Category} -> {articlePayload.Url}");
                        }
                        catch (Exception e)
                        {
                            await e.Log(string.Empty, string.Empty);
                            goto RETRY;
                        }
                        finally
                        {
                            await articlePage.CloseAsync();
                        }
                    }

                    await page.CloseAsync();
                }
            }
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        var articlePayload = new ConcurrentBag<ArticlePayload>();
        articlePayload.AddRange(crawlArticlePayload.ArticlesPayload);
        return articlePayload;
    }


    private DateTime GetDateTime(string dateTimeText)
    {
        dateTimeText = dateTimeText.Replace("GMT+7", string.Empty).Trim();

        DateTime dateTime;
        var isValid = DateTime.TryParseExact(dateTimeText, "HH:mm dd/MM/yyyy", DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None, out dateTime);
        if (isValid)
        {
            return dateTime;
        }

        isValid = DateTime.TryParseExact(dateTimeText, "dd/MM/yyyy HH:mm", DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None, out dateTime);
        if (isValid)
        {
            return dateTime;
        }

        return DateTime.UtcNow;
    }
}