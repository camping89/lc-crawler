using System.Collections.Concurrent;
using System.Globalization;
using System.Web;
using Dasync.Collections;
using Flurl;
using HtmlAgilityPack;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using Newtonsoft.Json;
using RestSharp;

namespace LC.Crawler.Console.Services;

public class CrawlSucKhoeDoiSongApiService : CrawlLCArticleApiBaseService
{
    private const string MainUrl = "https://suckhoedoisong.vn";

    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(string url)
    {
        var crawlArticlePayload = new ConcurrentBag<ArticlePayload>();
        // await GlobalConfig.CrawlConfig.SucKhoeDoiSongConfig.Categories.ParallelForEachAsync(async category =>
        // {
        //     crawlArticlePayload.AddRange(await GetArticlesPayload(category, category.Name));
        // });

        foreach (var category in GlobalConfig.CrawlConfig.SucKhoeDoiSongConfig.Categories)
        {
            crawlArticlePayload.AddRange(await GetArticlesPayload(category, category.Name));
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
            var articles = await GetCrawlSKDSArticles(crawlArticles.ToList());
            articlePayloads.AddRange(articles);
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articlePayloads;
    }

    private async Task<List<ArticlePayload>> GetCrawlSKDSArticles(List<ArticlePayload> crawlArticles)
    {
        var articles = new List<ArticlePayload>();
        foreach (var articlePayload in crawlArticles)
        {
            try
            {
                articlePayload.ShortDescription = articlePayload.ShortDescription.RemoveHrefFromA();
                var htmlString = await GetRawData(articlePayload.Url);

                var doc = new HtmlDocument();
                doc.LoadHtml(htmlString);

                var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'detail-content')]");
                var content = contentNode.InnerHtml;

                // remove related box news
                var ele_RelatedBoxNews = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'VCSortableInPreviewMode') and @type='RelatedNewsBox']");
                if (ele_RelatedBoxNews is not null)
                {
                    var relatedBoxNews = ele_RelatedBoxNews.InnerHtml;
                    if (relatedBoxNews.IsNotNullOrWhiteSpace())
                    {
                        content = content.SafeReplace(relatedBoxNews, string.Empty);
                    }
                    
                }

                // remove related one news
                var ele_RelatedOneNews = doc.DocumentNode.SelectNodes("//div[contains(@class,'VCSortableInPreviewMode') and @type='RelatedOneNews']");
                if (ele_RelatedOneNews.IsNotNullOrEmpty())
                {
                    foreach (var ele_RelatedOneNew in ele_RelatedOneNews)
                    {
                        var relatedOneNew = ele_RelatedOneNew.InnerHtml;
                        content = content.SafeReplace(relatedOneNew, string.Empty);
                    }
                }

                // remove ads div
                var adsIds = new List<string>() {"zone-krlv706p", "zone-krlutq8c"};
                foreach (var adsId in adsIds)
                {
                    var ele_Ads = doc.DocumentNode.SelectSingleNode($"//div[@id='{adsId}']");
                    if (ele_Ads is not null)
                    {
                        var ads = ele_Ads.InnerHtml;
                        content = content.SafeReplace(ads, string.Empty);
                    }
                }

                var ele_ReadMores = doc.DocumentNode.SelectNodes(
                    "//*[contains(text(),'xem thêm') or contains(text(),'Xem thêm') or contains(text(),'XEM THÊM') or contains(text(),'xem tiếp') or contains(text(),'Xem tiếp') or contains(text(),'XEM TIẾP')]");
                if (ele_ReadMores.IsNotNullOrEmpty())
                {
                    foreach (var elementHandle in ele_ReadMores)
                    {
                        var readMore = elementHandle.InnerHtml;
                        content = content.SafeReplace(readMore, string.Empty);
                    }
                }

                var ele_Videos = doc.DocumentNode.SelectNodes(
                    "//div[contains(@class,'VCSortableInPreviewMode') and boolean(@type='BoxTable') = false or contains(@class,'inread') or contains(@data-id,'stream')]");
                if (ele_Videos.IsNotNullOrEmpty())
                {
                    foreach (var elementHandle in ele_Videos)
                    {
                        var video = elementHandle.InnerHtml;
                        content = content.SafeReplace(video, string.Empty);
                    }
                }

                // add header to content
                articlePayload.Content = content.RemoveHrefFromA();

                var ele_Tags = doc.DocumentNode.SelectNodes("//ul[contains(@class,'detail-tag-list')]/li/a");
                if (ele_Tags.IsNotNullOrEmpty())
                {
                    articlePayload.Tags = new List<string>();
                    foreach (var ele_Tag in ele_Tags)
                    {
                        var tag = ele_Tag.InnerText;
                        articlePayload.Tags.Add(tag);
                    }
                }

                System.Console.WriteLine(JsonConvert.SerializeObject(articlePayload));
                
                articles.Add(articlePayload);
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
        }

        return articles;
    }

    private async Task<List<ArticlePayload>> GetArticlesPayload(Category category, string mainCategory)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        if (category.SubCategories is not null && category.SubCategories.Any())
        {
            foreach (var subCategory in category.SubCategories)
            {
                articlePayloads.AddRange(await GetArticlesPayload(subCategory, mainCategory));
            }
        }

        var articlesCategory = new List<ArticlePayload>();
        string htmlString = string.Empty;
        var pageNumber = 1;
        bool isValidArticle = true;
        do
        {
            try
            {
                var requestUrl = string.Format(category.Url, pageNumber);
                htmlString = await GetRawData(requestUrl);

                if (htmlString.IsNotNullOrWhiteSpace())
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(htmlString);

                    var articles = doc.DocumentNode.SelectNodes("//div[@class='box-category-item']");
                    foreach (var article in articles)
                    {
                        var articleDoc = new HtmlDocument();
                        articleDoc.LoadHtml(article.InnerHtml);

                        var urlNode = articleDoc.DocumentNode.SelectSingleNode("//a[@class='box-category-link-with-avatar']");
                        var articleUrl = urlNode.Attributes["href"].Value;
                        if (!articleUrl.Contains(MainUrl))
                        {
                            articleUrl = Url.Combine(MainUrl, articleUrl);
                        }

                        var titleNode = articleDoc.DocumentNode.SelectSingleNode("//a[@data-type='title']");
                        var title = titleNode.InnerText;

                        var imgNode = articleDoc.DocumentNode.SelectSingleNode("//img");
                        var imgUrl = imgNode.Attributes["src"].Value;

                        var categoryNode = articleDoc.DocumentNode.SelectSingleNode("//a[@class='box-category-category']");
                        var categoryName = categoryNode.Attributes["title"].Value.Trim();
                        if (mainCategory.ToUpper() != categoryName.ToUpper())
                        {
                            categoryName = $"{mainCategory} -> {categoryName}";
                        }

                        var dateTimeNode = articleDoc.DocumentNode.SelectSingleNode("//span[contains(@class,'time-ago')]");
                        var dateTimeString = dateTimeNode.InnerText;
                        var dateTime = GetDateTime(dateTimeString);
                        if (!await IsValidArticle(articlesCategory.Count, string.Empty, dateTime))
                        {
                            isValidArticle = false;
                            break;
                        }

                        var descriptionNode = articleDoc.DocumentNode.SelectSingleNode("//p[@data-type='sapo']");
                        var description = descriptionNode.InnerText;

                        var articlePayload = new ArticlePayload
                        {
                            Category = categoryName,
                            Title = title,
                            Url = articleUrl,
                            CreatedAt = dateTime,
                            FeatureImage = imgUrl,
                            ShortDescription = description
                        };

                        System.Console.WriteLine($"Category {articlePayload.Category} - {requestUrl} - Article Url {articlePayload.Url}");

                        articlesCategory.Add(articlePayload);
                    }

                    if (!isValidArticle)
                    {
                        break;
                    }
                }
                else
                {
                    System.Console.WriteLine("Test");
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
        } while (htmlString.IsNotNullOrWhiteSpace());

        articlePayloads.AddRange(articlesCategory);

        return articlePayloads.ToList();
    }

    private DateTime GetDateTime(string dateTimeString)
    {
        var dateTime = DateTime.ParseExact(dateTimeString, "dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

        return dateTime;
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
}