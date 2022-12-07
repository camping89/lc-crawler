using System.Collections.Concurrent;
using System.Web;
using HtmlAgilityPack;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using RestSharp;

namespace LC.Crawler.Console.Services.CrawlByAPI;

public class CrawlSieuThiSongKhoeArticleApiService : CrawlLCArticleApiBaseService
{
    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        foreach (var category in GlobalConfig.CrawlConfig.SieuThiSongKhoeConfig.Categories)
        {
            articlePayloads.AddRange(await CrawlArticles(category));
        }

        return articlePayloads;
    }

    private async Task<List<ArticlePayload>> CrawlArticles(Category category)
    {
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
                    
                    var articles = doc.DocumentNode.SelectNodes("//div[@class='blog-list']//div[@class='blog-item']");
                    if (articles is null || !articles.Any())
                    {
                        break;
                    }

                    foreach (var article in articles)
                    {
                        var articleDoc = new HtmlDocument();
                        articleDoc.LoadHtml(article.InnerHtml);

                        var articlePayload = new ArticlePayload
                        {
                            Category = category.Name
                        };
                        var ele_Url = articleDoc.DocumentNode.SelectSingleNode("//a");
                        if (ele_Url is not null)
                        {
                            articlePayload.Url = ele_Url.Attributes["href"].Value;
                        }

                        var ele_Title = articleDoc.DocumentNode.SelectSingleNode("//h2[@class='title']");
                        if (ele_Title is not null)
                        {
                            articlePayload.Title = ele_Title.InnerText;
                        }

                        var ele_ShortDesc = articleDoc.DocumentNode.SelectSingleNode("//div[@class='des']");
                        if (ele_ShortDesc is not null)
                        {
                            articlePayload.ShortDescription = ele_ShortDesc.InnerHtml.RemoveHrefFromA();
                        }
                        
                        var articleHtml = await GetRawData(articlePayload.Url);
                        System.Console.WriteLine($"CRAWL CHECKING DETAILS PAGE: {requestUrl} -- {articlePayload.Url}");
                        var articleDetailDoc = new HtmlDocument();
                        articleDetailDoc.LoadHtml(articleHtml);
                        
                        // Dont get the product in the article, this issue from sieuthisongkhoe
                        var isProduction = IsProduction(articleDetailDoc);
                        if (isProduction)
                        {
                            continue;
                        }
                        
                        var ele_CreatedAt = articleDetailDoc.DocumentNode.SelectSingleNode("//meta[@property='article:published_time']");
                        var createdAt = string.Empty;
                        var formatCreatedAt = DateTime.UtcNow;
                        if (ele_CreatedAt is not null)
                        {
                            createdAt = ele_CreatedAt.Attributes["content"].Value;
                        }

                        if (createdAt.IsNotNullOrEmpty())
                        {
                            formatCreatedAt = DateTime.Parse(createdAt);
                            if (!await IsValidArticle(articlesCategory.Count, string.Empty, formatCreatedAt))
                            {
                                isValidArticle = false;
                                break;
                            }
                        }

                        articlePayload.CreatedAt = formatCreatedAt;
                        
                        var ele_Content = articleDetailDoc.DocumentNode.SelectSingleNode("//div[@class='news-detail-content']");
                        if (ele_Content is not null)
                        {
                            var content = ele_Content.InnerHtml;
                            
                            var contentDoc = new HtmlDocument();
                            contentDoc.LoadHtml(content);
                
                            // Remove Xem them
                            var ele_ReadMore = contentDoc.DocumentNode.SelectSingleNode("//strong[contains(text(), 'Xem thêm:')]");
                            if (ele_ReadMore is not null)
                            {
                                content = content.SafeReplace(ele_ReadMore.InnerHtml, string.Empty);
                            }
                
                            articlePayload.Content = content.RemoveHrefFromA();
                        }

                        var ele_Thum = articleDetailDoc.DocumentNode.SelectSingleNode("//div[@class='news-detail-head']//img");
                        if (ele_Thum is not null)
                        {
                            articlePayload.FeatureImage = ele_Thum.Attributes["src"].Value;
                        }
                        
                        articlesCategory.Add(articlePayload);
                    }
                }
                else
                {
                    isValidArticle = false;
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

        return articlesCategory;
    }
    
    private bool IsProduction(HtmlDocument articleDetailDoc)
    {
        var categories     = new List<string>();
        var ele_Categories = articleDetailDoc.DocumentNode.SelectNodes("//nav[@class='woocommerce-breadcrumb']/span/a[not(text() = 'Trang Chủ')]");
        if (ele_Categories is not null && ele_Categories.Any())
        {
            foreach (var ele_Category in ele_Categories)
            {
                categories.Add(ele_Category.InnerText );
            }
        }

        return categories.Any(_ => _ == "Sản Phẩm");
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