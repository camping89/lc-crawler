using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Web;
using Dasync.Collections;
using HtmlAgilityPack;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using LC.Crawler.Client.Entities.AloBacSi;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using Newtonsoft.Json;
using RestSharp;

namespace LC.Crawler.Console.Services.CrawlByAPI;

public class CrawlAloBacSiApiService : CrawlLCArticleApiBaseService
{
    private string _mainUrl = "https://alobacsi.com";

    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(string url)
    {
        var crawlArticlePayload = new ConcurrentBag<ArticlePayload>();
        await GlobalConfig.CrawlConfig.AlobacsiConfig.Categories.ParallelForEachAsync(async category =>
        {
            crawlArticlePayload.AddRange(await GetArticlesPayload(category, category.Name));
        });

        System.Console.WriteLine($"Total Articles {crawlArticlePayload.Count}");
        using var autoResetEvent = new AutoResetEvent(false);
        autoResetEvent.WaitOne(5000);

        return new CrawlArticlePayload
        {
            Url = url,
            ArticlesPayload = crawlArticlePayload.ToList()
        };
    }

    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(
        CrawlArticlePayload crawlArticlePayload)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        var count = 0;
        var total = crawlArticlePayload.ArticlesPayload.Count;
        await crawlArticlePayload.ArticlesPayload.Partition(GlobalConfig.CrawlConfig.Crawl_BatchSize)
            .ParallelForEachAsync(async crawlArticles =>
            {
                var articles = await GetCrawlSKDSArticles(crawlArticles.ToList());
                articlePayloads.AddRange(articles);
                count += GlobalConfig.CrawlConfig.Crawl_BatchSize;
                System.Console.WriteLine($"{count}/{total}");
            }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articlePayloads;
    }

    private async Task<List<ArticlePayload>> GetCrawlSKDSArticles(List<ArticlePayload> crawlArticles)
    {
        var articles = new List<ArticlePayload>();
        foreach (var articlePayload in crawlArticles)
        {
            var count = 0;
            while (true)
            {
                try
                {
                    articlePayload.ShortDescription = articlePayload.ShortDescription.RemoveHrefFromA();
                    var htmlString = await GetRawData(articlePayload.Url);

                    var doc = new HtmlDocument();
                    doc.LoadHtml(htmlString);

                    var titleNode = doc.DocumentNode.SelectSingleNode("//div[@class='detail_content--useful']/h1") ??
                                    doc.DocumentNode.SelectSingleNode(
                                        "//div[contains(@class,'main-view')]//h1[contains(@class,'media-title')]");

                    if (titleNode is not null)
                    {
                        articlePayload.Title = titleNode.InnerText;
                    }

                    var shortDescriptionNode =
                        doc.DocumentNode.SelectSingleNode(
                            "//p[@class='video-des']") ??
                        doc.DocumentNode.SelectSingleNode(
                            "//div[contains(@class,'main-view')]//div[contains(@class,'media-body')]//ul");
                    if (shortDescriptionNode is not null)
                    {
                        articlePayload.ShortDescription = shortDescriptionNode.InnerText;
                        articlePayload.ShortDescription = articlePayload.ShortDescription.RemoveHrefFromA();
                    }

                    var contentNode =
                        doc.DocumentNode.SelectSingleNode(
                            "//div[@class='detail_block']") ??
                        doc.DocumentNode.SelectSingleNode(
                            "//div[contains(@class,'main-view')]//div[contains(@class,'post-content')]");
                    if (contentNode is not null)
                    {
                        articlePayload.Content = contentNode.InnerHtml;
                    }
                    else
                    {
                        var content = string.Empty;
                        var chatNode =
                            doc.DocumentNode.SelectSingleNode(
                                "//div[contains(@class,'main-post')]//div[contains(@class,'chat-message')]");
                        if (chatNode is not null)
                        {
                            content = chatNode.InnerHtml;
                            content += Environment.NewLine;
                        }

                        var postContentNode =
                            doc.DocumentNode.SelectSingleNode(
                                "//div[contains(@class,'main-post')]//div[contains(@class,'post-content')]");
                        if (postContentNode is not null)
                        {
                            content += postContentNode.InnerHtml;
                        }

                        articlePayload.Content = content;
                    }

                    if (articlePayload.Content.IsNullOrWhiteSpace())
                    {
                        var mediaContentNode = doc.DocumentNode.SelectSingleNode("//div[@class='media-body']");
                        if (mediaContentNode is not null)
                        {
                            articlePayload.Content = mediaContentNode.InnerHtml;
                        }
                    }

                    if (!articlePayload.CreatedAt.HasValue)
                    {
                        var dateNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class,'post-date')]");
                        if (dateNode is not null)
                        {
                            var date = GetDateTime(dateNode.InnerText);
                            articlePayload.CreatedAt = date;
                        }
                        else
                        {
                            articlePayload.CreatedAt = DateTime.UtcNow;
                        }
                    }

                    var tagNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'tag-box')]//a");
                    if (tagNodes.IsNotNullOrEmpty())
                    {
                        articlePayload.Tags = new List<string>();
                        foreach (var tagNode in tagNodes)
                        {
                            var tag = tagNode.InnerText;
                            tag = tag.Replace("#", string.Empty);
                            articlePayload.Tags.Add(tag);
                        }
                    }

                    articlePayload.Content = articlePayload.Content.RemoveHrefFromA();

                    System.Console.WriteLine(JsonConvert.SerializeObject(articlePayload));

                    // Retry 5 times
                    if (!articlePayload.Content.IsNotNullOrEmpty() && count < 5)
                    {
                        count++;
                    }
                    else
                    {
                        articles.Add(articlePayload);
                        break;
                    }
                }
                catch (Exception e)
                {
                    await e.Log(string.Empty, string.Empty);
                }
            }
        }

        return articles;
    }

    private async Task<List<ArticlePayload>> GetArticlesPayload(Category category, string mainCategory)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        if (category.SubCategories.IsNotNullOrEmpty())
        {
            await category.SubCategories.ParallelForEachAsync(async subCategory =>
            {
                var categoryName = $"{mainCategory} -> {subCategory.Name}";
                articlePayloads.AddRange(await GetArticlesPayload(subCategory, categoryName));
            });
        }

        var articlesCategory = new List<ArticlePayload>();
        var htmlString = string.Empty;
        var pageNumber = 1;
        var isValidArticle = true;
        try
        {
            htmlString = await GetRawData(category.Url);
            if (htmlString.IsNotNullOrWhiteSpace())
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlString);

                if (category.Url.Contains("video", StringComparison.InvariantCultureIgnoreCase))
                {
                    
                }
                
                var topArticleNode = doc.DocumentNode.SelectSingleNode("//div[@class='handbook-content']");
                if (topArticleNode is not null)
                {
                    var topUrl = topArticleNode.SelectSingleNode("//div[@class='news_home']//a").Attributes["href"]
                        .Value;
                    if (topUrl.Contains(".html"))
                    {
                        var image = topArticleNode.SelectSingleNode("//div[@class='news_home']//a/img")
                            .Attributes["src"].Value;
                        var titleAttribute = topArticleNode.SelectSingleNode("//div[@class='news_home']//a")
                            .Attributes["title"];
                        string title;
                        if (titleAttribute is not null)
                        {
                            title = titleAttribute.Value;
                        }
                        else
                        {
                            title = topArticleNode.SelectSingleNode("//div[@class='news_home']//a/img")
                                .Attributes["alt"].Value;
                        }
                        articlesCategory.Add(new ArticlePayload
                        {
                            Category = mainCategory,
                            Title = title,
                            FeatureImage = image,
                            Url = topUrl
                        });
                    }
                    else
                    {
                        isValidArticle = false;
                        
                    }
                }

                var t3NewNodes =
                    doc.DocumentNode.SelectNodes(
                        "//div[@class='handbook-content']/div[@class='doctor_row-3']/div[@class='doctor_row-3-block']/div[@class='doctor_row-3-one']/p/a");
                if (t3NewNodes.IsNotNullOrEmpty())
                {
                    foreach (var node in t3NewNodes)
                    {
                        var t3NewDoc = new HtmlDocument();
                        t3NewDoc.LoadHtml(node.InnerHtml);

                        var videoUrl = node.Attributes["href"].Value;
                        var title = node.Attributes["title"].Value;
                        var image = t3NewDoc.DocumentNode.SelectSingleNode("//img").Attributes["src"].Value;
                        articlesCategory.Add(new ArticlePayload
                        {
                            Category = mainCategory,
                            Title = title,
                            FeatureImage = image,
                            Url = videoUrl
                        });
                    }
                }

                var mediaNewsNode = doc.DocumentNode.SelectSingleNode("//div[@class='medical-new-images']//a");
                if (mediaNewsNode is not null)
                {
                    var url = mediaNewsNode.GetAttributeValue("href",string.Empty);
                    var title = mediaNewsNode.GetAttributeValue("title",string.Empty);
                    var image = mediaNewsNode.SelectSingleNode("//img").GetAttributeValue("src",string.Empty);
                    articlesCategory.Add(new ArticlePayload
                    {
                        Category = mainCategory,
                        Title = title,
                        FeatureImage = image,
                        Url = url
                    });
                }

                var mediaOldNewsNodes = doc.DocumentNode.SelectNodes("//div[@class='medical-old-images']//a");
                if (mediaOldNewsNodes is not null)
                {
                    foreach (var mediaOldNewsNode in mediaOldNewsNodes)
                    {
                        var url = mediaOldNewsNode.GetAttributeValue("href",string.Empty);
                        var title = mediaOldNewsNode.GetAttributeValue("title",string.Empty);
                        var image = mediaOldNewsNode.SelectSingleNode("//img").GetAttributeValue("src",string.Empty);
                        articlesCategory.Add(new ArticlePayload
                        {
                            Category = mainCategory,
                            Title = title,
                            FeatureImage = image,
                            Url = url
                        });
                    }
                }

                isValidArticle = await GetArticles(mainCategory, doc, articlesCategory, isValidArticle);

                do
                {
                    var articles = await GetArticles("video", 2, pageNumber);
                    if (articles is not null)
                    {
                        if (articles.error == 1)
                        {
                            isValidArticle = false;
                        }
                        else
                        {
                            var docDetails = new HtmlDocument();
                            docDetails.LoadHtml(articles.data);
                        
                            isValidArticle = await GetArticles(mainCategory, docDetails, articlesCategory, isValidArticle);
                        }

                        pageNumber += 1;
                    }
                    else
                    {
                        break;
                    }
                    
                } while (isValidArticle);
            }
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, string.Empty);
            System.Console.WriteLine(mainCategory);
            System.Console.WriteLine(pageNumber);
            System.Console.WriteLine(category.Url);
        }
        // do
        // {
        //     try
        //     {
        //         var requestUrl = string.Format(category.Url, pageNumber);
        //         htmlString = await GetRawData(requestUrl);
        //
        //         if (htmlString.IsNotNullOrWhiteSpace())
        //         {
        //             var doc = new HtmlDocument();
        //             doc.LoadHtml(htmlString);
        //
        //             if (category.Url.Contains("video", StringComparison.InvariantCultureIgnoreCase))
        //             {
        //                 var topArticleNode = doc.DocumentNode.SelectSingleNode("//div[@class='handbook-content']");
        //                 if (topArticleNode is not null)
        //                 {
        //                     var topUrl = topArticleNode.SelectSingleNode("//div[@class='news_home']//a").Attributes["href"]
        //                         .Value;
        //                     if (topUrl.Contains(".html"))
        //                     {
        //                         var image = topArticleNode.SelectSingleNode("//div[@class='news_home']//a/img")
        //                             .Attributes["src"].Value;
        //                         var title = topArticleNode.SelectSingleNode("//div[@class='news_home']//a").Attributes["title"]
        //                             .Value;
        //                         articlesCategory.Add(new ArticlePayload
        //                         {
        //                             Category = mainCategory,
        //                             Title = title,
        //                             FeatureImage = image,
        //                             Url = topUrl
        //                         });
        //                     }
        //                     else
        //                     {
        //                         isValidArticle = false;
        //                         break;
        //                     }
        //                 }
        //
        //                 var t3NewNodes =
        //                     doc.DocumentNode.SelectNodes(
        //                         "//div[@class='handbook-content']/div[@class='doctor_row-3']/div[@class='doctor_row-3-block']/div[@class='doctor_row-3-one']/p/a");
        //                 if (t3NewNodes.IsNotNullOrEmpty())
        //                 {
        //                     foreach (var node in t3NewNodes)
        //                     {
        //                         var t3NewDoc = new HtmlDocument();
        //                         t3NewDoc.LoadHtml(node.InnerHtml);
        //
        //                         var videoUrl = node.Attributes["href"].Value;
        //                         var title = node.Attributes["title"].Value;
        //                         var image = t3NewDoc.DocumentNode.SelectSingleNode("//img").Attributes["src"].Value;
        //                         articlesCategory.Add(new ArticlePayload
        //                         {
        //                             Category = mainCategory,
        //                             Title = title,
        //                             FeatureImage = image,
        //                             Url = videoUrl
        //                         });
        //                     }
        //                 }
        //
        //                 var mediaNewsNode = doc.DocumentNode.SelectSingleNode("//div[@class='medical-new-images']//a");
        //                 if (mediaNewsNode is not null)
        //                 {
        //                     var url = mediaNewsNode.GetAttributeValue("href",string.Empty);
        //                     var title = mediaNewsNode.GetAttributeValue("title",string.Empty);
        //                     var image = mediaNewsNode.SelectSingleNode("//img").GetAttributeValue("src",string.Empty);
        //                     articlesCategory.Add(new ArticlePayload
        //                     {
        //                         Category = mainCategory,
        //                         Title = title,
        //                         FeatureImage = image,
        //                         Url = url
        //                     });
        //                 }
        //
        //                 var mediaOldNewsNodes = doc.DocumentNode.SelectNodes("//div[@class='medical-old-images']//a");
        //                 foreach (var mediaOldNewsNode in mediaOldNewsNodes)
        //                 {
        //                     var url = mediaOldNewsNode.GetAttributeValue("href",string.Empty);
        //                     var title = mediaOldNewsNode.GetAttributeValue("title",string.Empty);
        //                     var image = mediaOldNewsNode.SelectSingleNode("//img").GetAttributeValue("src",string.Empty);
        //                     articlesCategory.Add(new ArticlePayload
        //                     {
        //                         Category = mainCategory,
        //                         Title = title,
        //                         FeatureImage = image,
        //                         Url = url
        //                     });
        //                 }
        //
        //                 var mediaNodes =
        //                     doc.DocumentNode.SelectNodes("//div[@class='cate_chat-images']//a");
        //                 if (mediaNodes.IsNotNullOrEmpty())
        //                 {
        //                     foreach (var node in mediaNodes)
        //                     {
        //                         // var mediaDoc = new HtmlDocument();
        //                         // mediaDoc.LoadHtml(node.InnerHtml);
        //                         //
        //                         // var articleUrl = mediaDoc.DocumentNode.SelectSingleNode("//div[@class='media-body']/a")
        //                         //     .Attributes["href"]
        //                         //     .Value;
        //                         // var title = mediaDoc.DocumentNode.SelectSingleNode("//h3[@class='media-title']")
        //                         //     .InnerText;
        //                         // var image = mediaDoc.DocumentNode.SelectSingleNode("//a//img").Attributes["src"].Value;
        //                         var url = node.GetAttributeValue("href",string.Empty);
        //                         var title = node.GetAttributeValue("title",string.Empty);
        //                         var image = node.SelectSingleNode("//img").GetAttributeValue("src",string.Empty);
        //                         
        //                         var htmlStringDetails  = await GetRawData(url);
        //                         var docDetails = new HtmlDocument();
        //                         docDetails.LoadHtml(htmlStringDetails);
        //                         
        //                         var date = GetDateTime(docDetails.DocumentNode.SelectSingleNode("//p[@class='time']").InnerText);
        //                         if (!await IsValidArticle(articlesCategory.Count, string.Empty, date))
        //                         {
        //                             isValidArticle = false;
        //                             break;
        //                         }
        //
        //                         articlesCategory.Add(new ArticlePayload
        //                         {
        //                             Category = mainCategory,
        //                             Url = url,
        //                             Title = title,
        //                             FeatureImage = image,
        //                             CreatedAt = date
        //                         });
        //                     }
        //                 }
        //             }
        //             else
        //             {
        //                 var topArticleNode =
        //                     doc.DocumentNode.SelectSingleNode("//div[contains(@class,'top-article')]/a");
        //                 if (topArticleNode is not null)
        //                 {
        //                     var topArticleUrl = topArticleNode.Attributes["href"].Value;
        //                     if (topArticleUrl.Contains(".html"))
        //                     {
        //                         var img = topArticleNode
        //                             .SelectSingleNode("//div[contains(@class,'top-article')]/a//img").Attributes["src"]
        //                             .Value;
        //                         var title = topArticleNode
        //                             .SelectSingleNode("//div[contains(@class,'top-article')]/a//h2").InnerText.Trim();
        //                         articlesCategory.Add(new ArticlePayload
        //                         {
        //                             Title = title,
        //                             Category = mainCategory,
        //                             Url = topArticleUrl,
        //                             FeatureImage = img
        //                         });
        //                     }
        //                     else
        //                     {
        //                         isValidArticle = false;
        //                         break;
        //                     }
        //                 }
        //
        //                 var t3NewNodes =
        //                     doc.DocumentNode.SelectNodes("//div[@class='t3-news']//article[@class='card']");
        //                 if (t3NewNodes.IsNotNullOrEmpty())
        //                 {
        //                     foreach (var node in t3NewNodes)
        //                     {
        //                         var t3NewDoc = new HtmlDocument();
        //                         t3NewDoc.LoadHtml(node.InnerHtml);
        //
        //                         var articleUrl = t3NewDoc.DocumentNode
        //                             .SelectSingleNode("//div[contains(@class,'card-body')]/a")
        //                             .Attributes["href"].Value;
        //                         var title = t3NewDoc.DocumentNode.SelectSingleNode("//h4").InnerText;
        //                         var img = t3NewDoc.DocumentNode.SelectSingleNode("//img").Attributes["src"].Value;
        //                         articlesCategory.Add(new ArticlePayload
        //                         {
        //                             Title = title,
        //                             Category = mainCategory,
        //                             Url = articleUrl,
        //                             FeatureImage = img
        //                         });
        //                     }
        //                 }
        //
        //                 var mediaNodes =
        //                     doc.DocumentNode.SelectNodes("//ul[contains(@class,'list-media')]//div[@class='media']");
        //                 if (mediaNodes.IsNotNullOrEmpty())
        //                 {
        //                     foreach (var node in mediaNodes)
        //                     {
        //                         var mediaDoc = new HtmlDocument();
        //                         mediaDoc.LoadHtml(node.InnerHtml);
        //                         var articleUrl = mediaDoc.DocumentNode.SelectSingleNode("//div[@class='media-body']/a")
        //                             .Attributes["href"]
        //                             .Value;
        //                         var titleNode = mediaDoc.DocumentNode.SelectSingleNode("//h3[@class='media-title']") ??
        //                                         mediaDoc.DocumentNode.SelectSingleNode("//h2[@class='media-title']");
        //                         var title = titleNode.InnerText;
        //                         var image = mediaDoc.DocumentNode.SelectSingleNode("//a//img").Attributes["src"].Value;
        //                         var date = GetDateTime(
        //                             mediaDoc.DocumentNode.SelectSingleNode("//span[contains( @class,'media-date')]")
        //                                 .InnerText);
        //                         if (!await IsValidArticle(articlesCategory.Count, string.Empty, date))
        //                         {
        //                             isValidArticle = false;
        //                             break;
        //                         }
        //
        //                         articlesCategory.Add(new ArticlePayload
        //                         {
        //                             Category = mainCategory,
        //                             Url = articleUrl,
        //                             Title = title,
        //                             FeatureImage = image,
        //                             CreatedAt = date
        //                         });
        //                     }
        //                 }
        //
        //                 if (!isValidArticle)
        //                 {
        //                     break;
        //                 }
        //             }
        //
        //             var nextPageNode = doc.DocumentNode.SelectSingleNode("//nav/ul[contains(@class,'pagination')]");
        //             if (nextPageNode is not null && !nextPageNode.InnerText.Contains("Sau", StringComparison.InvariantCultureIgnoreCase))
        //             {
        //                 isValidArticle = false;
        //                 break;
        //             }
        //         }
        //
        //         pageNumber += 1;
        //     }
        //     catch (Exception e)
        //     {
        //         await e.Log(string.Empty, string.Empty);
        //         System.Console.WriteLine(mainCategory);
        //         System.Console.WriteLine(pageNumber);
        //         System.Console.WriteLine(category.Url);
        //     }
        // } while (htmlString.IsNotNullOrWhiteSpace());

        if (articlesCategory.IsNotNullOrEmpty())
        {
            foreach (var article in articlesCategory)
            {
                System.Console.WriteLine(article.Url);
            }

            articlePayloads.AddRange(articlesCategory);
        }

        return articlePayloads.ToList();
    }

    private async Task<bool> GetArticles(string mainCategory, HtmlDocument doc, List<ArticlePayload> articlesCategory, bool isValidArticle)
    {
        var mediaNodes =
            doc.DocumentNode.SelectNodes("//div[@class='cate_chat-images']//a");
        if (mediaNodes.IsNotNullOrEmpty())
        {
            foreach (var node in mediaNodes)
            {
                var url = node.GetAttributeValue("href", string.Empty);
                var title = node.GetAttributeValue("title", string.Empty);
                var image = node.SelectSingleNode("//img").GetAttributeValue("src", string.Empty);

                string htmlStringDetails;
                do
                {
                    htmlStringDetails = await GetRawData(url);
                    
                } while (htmlStringDetails.IsNullOrWhiteSpace());
                
                var docDetails = new HtmlDocument();
                docDetails.LoadHtml(htmlStringDetails);

                var ele_time = docDetails.DocumentNode.SelectSingleNode("//p[@class='time']");
                if (ele_time is null)
                {
                    Debug.WriteLine("Test");
                }

                var date = GetDateTime(docDetails.DocumentNode.SelectSingleNode("//p[@class='time']").InnerText);
                if (!await IsValidArticle(articlesCategory.Count, string.Empty, date))
                {
                    isValidArticle = false;
                    break;
                }

                articlesCategory.Add(new ArticlePayload
                {
                    Category = mainCategory,
                    Url = url,
                    Title = title,
                    FeatureImage = image,
                    CreatedAt = date
                });
            }
        }

        return isValidArticle;
    }

    private DateTime GetDateTime(string dateTimeText)
    {
        dateTimeText = dateTimeText.Replace("GMT+7", string.Empty).Trim().Replace("Ngày đăng: ", string.Empty).Trim();

        var isValid = DateTime.TryParseExact(dateTimeText, "HH:mm dd/MM/yyyy", DateTimeFormatInfo.InvariantInfo,
            DateTimeStyles.None, out var dateTime);
        if (isValid)
        {
            return dateTime;
        }

        isValid = DateTime.TryParseExact(dateTimeText, "dd/MM/yyyy HH:mm", DateTimeFormatInfo.InvariantInfo,
            DateTimeStyles.None, out dateTime);
        if (isValid)
        {
            return dateTime;
        }

        return DateTime.UtcNow;
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

    private async Task<Articles?> GetArticles(string key, int categoryId, int page)
    {
        var client = new RestClient("https://alobacsi.com");
        var request = new RestRequest("/api/posts-loadmore", Method.POST)
        {
            AlwaysMultipartFormData = true
        };
        request.AddParameter("key", key);
        request.AddParameter("page", page);
        request.AddParameter("category_id", categoryId);
        var response = await client.ExecuteAsync(request);
        return JsonConvert.DeserializeObject<Articles>(response.Content);
    }
}