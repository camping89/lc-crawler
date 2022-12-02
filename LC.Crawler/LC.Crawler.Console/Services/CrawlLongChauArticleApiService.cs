using System.Collections.Concurrent;
using System.Globalization;
using System.Web;
using Dasync.Collections;
using HtmlAgilityPack;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using Newtonsoft.Json;
using RestSharp;

namespace LC.Crawler.Console.Services;

public class CrawlLongChauArticleApiService : CrawlLCArticleApiBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(string url)
    {
        var crawlArticlePayload = new ConcurrentBag<ArticlePayload>();
        foreach (var category in GlobalConfig.CrawlConfig.LongChauArticleConfig.Categories)
        {
            crawlArticlePayload.AddRange(await GetArticlesPayload(category));
        }

        System.Console.WriteLine($"Total Articles {crawlArticlePayload.Count}");
        using var autoResetEvent = new AutoResetEvent(false);
        autoResetEvent.WaitOne(5000);

        return new CrawlArticlePayload
        {
            Url = url,
            ArticlesPayload = crawlArticlePayload.ToList()
        };
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize).ParallelForEachAsync(async crawlArticles =>
        {
            var articles = await CrawlLongChauArticles(crawlArticles.ToList());
            articlePayloads.AddRange(articles);
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articlePayloads;
    }

    private async Task<List<ArticlePayload>> CrawlLongChauArticles(List<ArticlePayload> articlePayloads)
    {
        foreach (var articlePayload in articlePayloads)
        {
            try
            {
                var htmlString = await GetRawData(articlePayload.Url);
                if (htmlString.IsNotNullOrWhiteSpace())
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(htmlString);

                    var ele_ShortDesc = doc.DocumentNode.SelectSingleNode("//div[contains(@class,' post-detail')]/div/p[@class='short-description']");
                    if (ele_ShortDesc is not null)
                    {
                        var shortDescription = ele_ShortDesc.InnerText;
                        articlePayload.ShortDescription = shortDescription.RemoveHrefFromA();
                    }

                    var categoryName = string.Empty;
                    var ele_Categories = doc.DocumentNode.SelectNodes("//ol[contains(@class, 'breadcrumb')]/li/a[not(text() = 'Trang chủ')]");
                    if (ele_Categories is not null && ele_Categories.Any())
                    {
                        foreach (var ele_Category in ele_Categories)
                        {
                            if (categoryName.IsNotNullOrEmpty())
                            {
                                categoryName += " -> ";
                            }

                            categoryName += ele_Category.InnerText;
                        }
                    }

                    if (categoryName.IsNotNullOrEmpty())
                    {
                        articlePayload.Category = categoryName;
                    }

                    var ele_Content = doc.DocumentNode.SelectSingleNode("//div[contains(@class,' post-detail')]/div[@class='r1-1']");
                    if (ele_Content is not null)
                    {
                        var createdAtStr = string.Empty;
                        var ele_CreatedAt = doc.DocumentNode.SelectSingleNode("//div[@class= 'detail']/p");
                        if (ele_CreatedAt is not null)
                        {
                            createdAtStr = ele_CreatedAt.InnerText;
                        }

                        var content = ele_Content.InnerHtml;
                        
                        var ele_ContentDoc = new HtmlDocument();
                        ele_ContentDoc.LoadHtml(content);
                        var ele_RelatedNews = ele_ContentDoc.DocumentNode.SelectSingleNode("//div[@class='list-title']");
                        var relatedNews = string.Empty;
                        if (ele_RelatedNews is not null)
                        {
                            relatedNews = ele_RelatedNews.InnerHtml;
                        }

                        var hashtag = string.Empty;
                        var ele_Hashtag = ele_ContentDoc.DocumentNode.SelectSingleNode("//div[@class='tag']");
                        if (ele_Hashtag is not null)
                        {
                            hashtag = ele_Hashtag.InnerHtml;
                            
                            var hashtagDoc = new HtmlDocument();
                            hashtagDoc.LoadHtml(hashtag);
                            
                            var ele_Hashtags = hashtagDoc.DocumentNode.SelectNodes("//li");
                            if (ele_Hashtags is not null && ele_Hashtags.Any())
                            {
                                articlePayload.Tags = new List<string>();
                                foreach (var eleHashtag in ele_Hashtags)
                                {
                                    var tag = eleHashtag.InnerText;
                                    articlePayload.Tags.Add(tag);
                                }
                            }
                        }

                        // remove useless value in content
                        content = content.Replace($"<h1>{articlePayload.Title}</h1>", string.Empty);
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

                        content = content.RemoveHrefFromA();
                        articlePayload.Content = content;
                        
                        System.Console.WriteLine(JsonConvert.SerializeObject(articlePayload));
                    }
                }
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
        }

        return articlePayloads;
    }

    private async Task<List<ArticlePayload>> GetArticlesPayload(Category category)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        if (category.SubCategories is not null && category.SubCategories.Any())
        {
            await category.SubCategories.ParallelForEachAsync(async subCategory =>
            {
                articlePayloads.AddRange(await GetArticlesPayload(subCategory));
            });
        }

        var articlesCategory = new List<ArticlePayload>();
        var pageNumber = 1;
        bool isValidArticle = true;
        while (isValidArticle)
        {
            try
            {
                var requestUrl = string.Format(category.Url, pageNumber);
                var htmlString = await GetRawData(requestUrl);

                if (htmlString.IsNotNullOrWhiteSpace())
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(htmlString);

                    var ele_Hover = doc.DocumentNode.SelectNodes("//div[@class='ss-chuyende-content']//div[@class='img-hover']/div");
                    if (ele_Hover is not null && ele_Hover.Any())
                    {
                        foreach (var elementHandle in ele_Hover)
                        {
                            var elementHandleDoc = new HtmlDocument();
                            elementHandleDoc.LoadHtml(elementHandle.InnerHtml);

                            var ele_ArticleUrl = elementHandleDoc.DocumentNode.SelectSingleNode("//a");
                            var articleUrl = ele_ArticleUrl.Attributes["href"].Value;
                            var ele_Image = elementHandleDoc.DocumentNode.SelectSingleNode("//img");
                            var img = ele_Image.Attributes["src"].Value;
                            var ele_Title = elementHandleDoc.DocumentNode.SelectSingleNode("//h3");
                            var title = ele_Title.InnerText;
                            var ele_Date = elementHandleDoc.DocumentNode.SelectSingleNode("//span");
                            var dateString = ele_Date.InnerText;
                            var dateTime = GetArticleCreatedAt(dateString);

                            System.Console.WriteLine($"Url: {articleUrl}");

                            articlesCategory.Add(new ArticlePayload
                            {
                                Url = articleUrl,
                                Title = title,
                                FeatureImage = img,
                                CreatedAt = dateTime
                            });
                        }
                    }

                    var ele_Articles = doc.DocumentNode.SelectNodes("//div[contains(@class, 'chuyende-sub-news')]//article[@class='t-news']");
                    if (ele_Articles is null || !ele_Articles.Any())
                    {
                        ele_Articles = doc.DocumentNode.SelectNodes("//div[contains(@class, 'ss-chuyende-news')]//article[@class='t-news']");
                    }

                    if (ele_Articles is not null && ele_Articles.Any())
                    {
                        foreach (var eleArticle in ele_Articles)
                        {
                            var eleArticleDoc = new HtmlDocument();
                            eleArticleDoc.LoadHtml(eleArticle.InnerHtml);

                            var ele_CreatedAt = eleArticleDoc.DocumentNode.SelectSingleNode("//span[@class='date']");
                            if (ele_CreatedAt is null) continue;

                            var createdAt = GetArticleCreatedAt(ele_CreatedAt.InnerText);
                            if (!await IsValidArticle(articlesCategory.Count, string.Empty, createdAt))
                            {
                                isValidArticle = false;
                                break;
                            }

                            var ele_Link = eleArticleDoc.DocumentNode.SelectSingleNode("//a[@class='title']");
                            if (ele_Link is null) continue;

                            var link = ele_Link.Attributes["href"].Value;

                            var ele_Image = eleArticleDoc.DocumentNode.SelectSingleNode("//img");
                            var image = string.Empty;
                            var title = string.Empty;
                            if (ele_Image is not null)
                            {
                                var att = ele_Image.Attributes["data-src"] ?? ele_Image.Attributes["src"];
                                image = att.Value;
                                title = ele_Image.Attributes["alt"].Value;
                            }

                            System.Console.WriteLine($"Url: {link} - Category {category.Name} - Page {pageNumber}");

                            articlesCategory.Add(new ArticlePayload
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
                        isValidArticle = false;
                    }
                }
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
            finally
            {
                pageNumber += 1;
            }
        }

        articlePayloads.AddRange(articlesCategory);

        return articlePayloads.ToList();
    }

    private async Task<string> GetRawData(string url)
    {
        var client = new RestClient(url);
        var request = new RestRequest();

        var response = await client.ExecuteAsync<string>(request);
        var content = response.Content;
        content = HttpUtility.HtmlDecode(content);
        using var autoResetEvent = new AutoResetEvent(false);
        autoResetEvent.WaitOne(100);
        return content;
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
}