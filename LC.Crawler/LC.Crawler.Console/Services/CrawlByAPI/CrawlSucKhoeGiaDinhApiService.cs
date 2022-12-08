using System.Collections.Concurrent;
using System.Diagnostics;
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

namespace LC.Crawler.Console.Services.CrawlByAPI;

public class CrawlSucKhoeGiaDinhApiService : CrawlLCArticleApiBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(string url)
    {
        var crawlArticlePayload = new ConcurrentBag<ArticlePayload>();

        foreach (var category in GlobalConfig.CrawlConfig.SucKhoeGiaDinhConfig.Categories)
        {
            crawlArticlePayload.AddRange(await GetArticlesPayload(category, url));
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
            var articles = await CrawlArticles(crawlArticles.ToList());
            articlePayloads.AddRange(articles);
        }, GlobalConfig.CrawlConfig.Crawl_MaxThread);

        return articlePayloads;
    }

    private async Task<List<ArticlePayload>> CrawlArticles(List<ArticlePayload> articles)
    {
        foreach (var articlePayload in articles)
        {
            try
            {
                var htmlString = await GetRawData(articlePayload.Url, Method.GET);

                var doc = new HtmlDocument();
                doc.LoadHtml(htmlString);
                
                var ele_ShortDesc = doc.DocumentNode.SelectSingleNode("//p[@class='lead']");
                if (ele_ShortDesc is not null)
                {
                    articlePayload.ShortDescription = ele_ShortDesc.InnerText.Trim('\r').Trim('\n').Trim();
                }
                
                var ele_Content = doc.DocumentNode.SelectSingleNode("//div[@class='detail_tin']//div[@class='content-detail__right']/div[@id='content']");
                if (ele_Content is not null)
                {
                    articlePayload.Content = ele_Content.InnerHtml;

                    var element_ReadMore =
                        doc.DocumentNode.SelectSingleNode("//div[@class='detail_tin']//div[@class='content-detail__right']/div[@id='content']//p[contains(text(), 'Xem thêm:')]");
                    if (element_ReadMore is not null)
                    {
                        var readMore = element_ReadMore.InnerHtml;

                        articlePayload.Content = articlePayload.Content.SafeReplace(readMore, string.Empty);
                    }

                    var element_Author = doc.DocumentNode.SelectNodes("//div[@class='detail_tin']//div[@class='content-detail__right']/div[@id='content']//p[@align='right']");
                    if (element_Author is not null && element_Author.Any())
                    {
                        foreach (var elementHandle in element_Author)
                        {
                            var author = elementHandle.InnerHtml;
                            articlePayload.Content = articlePayload.Content.SafeReplace(author, string.Empty);
                        }
                    }
                    
                    articlePayload.Content = articlePayload.Content.RemoveHrefFromA();
                }
                
                var ele_Tags = doc.DocumentNode.SelectNodes("//div[@class='box-tag']//a[@class='item_tag']");
                if (ele_Tags is not null && ele_Tags.Any())
                {
                    articlePayload.Tags = new List<string>();
                    foreach (var elementHandle in ele_Tags)
                    {
                        var tag = elementHandle.InnerText;
                        articlePayload.Tags.Add(tag);
                    }
                }
                
                System.Console.WriteLine($"Crawl Details: {articlePayload.Url}");
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
        }

        return articles;
    }

    private async Task<List<ArticlePayload>> GetArticlesPayload(Category category, string mainUrl)
    {
        var articlesCategory = new List<ArticlePayload>();
        string htmlString = string.Empty;
        var pageNumber = 1;
        bool isValidArticle = true;
        do
        {
            try
            {
                var requestUrl = string.Format(category.Url, pageNumber);
                var rawData = await GetRawData(requestUrl, Method.POST);
                var sucKhoeGiaDinhApiResponse = JsonConvert.DeserializeObject<SucKhoeGiaDinhApiResponse>(rawData);
                htmlString = HttpUtility.HtmlDecode(sucKhoeGiaDinhApiResponse.htmlRenderListNews);

                if (htmlString.IsNotNullOrWhiteSpace())
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(htmlString);
                    
                    var articles = doc.DocumentNode.SelectNodes("//article");
                    if (articles is not null && articles.Any())
                    {
                        foreach (var article in articles)
                        {
                            var articleDoc = new HtmlDocument();
                            articleDoc.LoadHtml(article.InnerHtml);

                            var ele_CreatedAt = articleDoc.DocumentNode.SelectSingleNode("//div[@class='tag']");
                            var createdAtString = ele_CreatedAt.InnerText;
                            createdAtString = createdAtString.Split("-")[1].Trim();
                            var timeAt = DateTime.Parse($"{createdAtString.Split("|")[0].Trim()}");
                            var dateAt = DateTime.ParseExact($"{createdAtString.Split("|")[1].Trim()}", "dd/MM/yyyy", CultureInfo.CurrentCulture);

                            var createdAt = dateAt.Add(timeAt.TimeOfDay);
                            if (!await IsValidArticle(articlesCategory.Count, string.Empty, createdAt))
                            {
                                isValidArticle = false;
                                break;
                            }
                                    
                            
                            var ele_Url = articleDoc.DocumentNode.SelectSingleNode("//div[@class='content']/h3[@class='title-news']/a");
                            var url = ele_Url.Attributes["href"].Value;
                            url = Url.Combine(mainUrl, url);

                            var title = ele_Url.InnerText.Trim('\r').Trim('\n').Trim();
                            
                            var ele_Image = articleDoc.DocumentNode.SelectSingleNode("//div[@class='thumb-art']/a/img");
                            var imageUrl = ele_Image.Attributes["src"].Value;
                            
                            System.Console.WriteLine($"Category {category.Name} - {requestUrl} - Article Url {url}");
                            articlesCategory.Add(new ArticlePayload
                            {
                                Category = category.Name,
                                Title = title,
                                Url = url,
                                CreatedAt = createdAt,
                                FeatureImage = imageUrl
                            });
                        }

                        if (!isValidArticle)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
            finally
            {
                pageNumber = articlesCategory.Count + 1;
            }
            
        } while (htmlString.IsNotNullOrWhiteSpace());

        return articlesCategory;
    }
    
    private async Task<string> GetRawData(string url, Method method)
    {
        var client = new RestClient(url);
        var request = new RestRequest();

        var response = await client.ExecuteAsync<string>(request,method);
        var content = response.Content;
        content = HttpUtility.HtmlDecode(content);
        using var autoResetEvent = new AutoResetEvent(false);
        autoResetEvent.WaitOne(100);
        return content;
    }
    
}

public class SucKhoeGiaDinhApiResponse
{
    public string error { get; set; }
    public string countItem { get; set; }
    public string htmlRenderListNews { get; set; }
}