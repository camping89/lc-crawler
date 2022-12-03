using System.Collections.Concurrent;
using System.Globalization;
using System.Web;
using HtmlAgilityPack;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using RestSharp;

namespace LC.Crawler.Console.Services;

public class CrawlBlogSucKhoeApiService : CrawlLCArticleApiBaseService
{
    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        foreach (var category in GlobalConfig.CrawlConfig.BlogSucKhoeConfig.Categories)
        {
            articlePayloads.AddRange(await CrawlArticles(category, category.Name));
        }
        
        return articlePayloads;
    }

    private async Task<ConcurrentBag<ArticlePayload>> CrawlArticles(Category category, string parentCategory)
    {
        ConcurrentBag<ArticlePayload> articlePayloads = new ConcurrentBag<ArticlePayload>();

        if (category.SubCategories is not null && category.SubCategories.Any())
        {
            foreach (var subCategory in category.SubCategories)
            {
                articlePayloads.AddRange(await CrawlArticles(subCategory, parentCategory));
            }
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

                    var articles = doc.DocumentNode.SelectNodes("//div[@id='content']/div[contains(@class,'post')]");
                    if (articles is not null && articles.Any())
                    {
                        foreach (var article in articles)
                        {
                            var articleDoc = new HtmlDocument();
                            articleDoc.LoadHtml(article.InnerHtml);
                            // get url (get from list articles)
                            var ele_Url = articleDoc.DocumentNode.SelectSingleNode("//h2[@class='title']/a");
                            var articleUrl = string.Empty;
                            if (ele_Url is not null)
                            {
                                articleUrl = ele_Url.Attributes["href"].Value;
                            }

                            var articleHtml = await GetRawData(articleUrl);
                            System.Console.WriteLine($"CRAWL CHECKING DETAILS PAGE: {requestUrl} -- {articleUrl}");
                            var articleDetailDoc = new HtmlDocument();
                            articleDetailDoc.LoadHtml(articleHtml);

                            var ele_CreatedAt = articleDetailDoc.DocumentNode.SelectSingleNode("//div[@id='content']//div[@class='postmeta-primary']/span[@class='meta_date']");
                            var createdAt = string.Empty;
                            var formatCreatedAt = DateTime.Today;
                            if (ele_CreatedAt is not null)
                            {
                                createdAt = ele_CreatedAt.InnerText;
                            }

                            if (createdAt.IsNotNullOrEmpty())
                            {
                                formatCreatedAt = DateTime.ParseExact(createdAt, "dd/MM/yyyy", CultureInfo.CurrentCulture);
                                if (!await IsValidArticle(articlesCategory.Count, string.Empty, formatCreatedAt))
                                {
                                    isValidArticle = false;
                                    break;
                                }
                            }

                            // get image (get from list articles)
                            var articleImageSrc = string.Empty;
                            var ele_Image = articleDoc.DocumentNode.SelectSingleNode("//img");
                            if (ele_Image is not null)
                            {
                                articleImageSrc = ele_Image.Attributes["src"].Value;
                            }

                            // get short desc (get from list articles)
                            var ele_ShortDesc = articleDoc.DocumentNode.SelectSingleNode("//div[@class='entry clearfix']");
                            var articleShortDesc = string.Empty;
                            if (ele_ShortDesc is not null)
                            {
                                articleShortDesc = ele_ShortDesc.InnerText;
                                articleShortDesc = articleShortDesc.SafeReplace("more »", string.Empty) + " ...";
                                articleShortDesc = articleShortDesc.RemoveHrefFromA();
                            }

                            // get title (get from list articles)
                            string articleTitle = string.Empty;
                            var ele_Title = articleDoc.DocumentNode.SelectSingleNode("//h2[@class='title']");
                            if (ele_Title is not null)
                            {
                                articleTitle = ele_Title.InnerText;
                            }

                            // get content (get from article details)
                            var ele_Content = articleDetailDoc.DocumentNode.SelectSingleNode("//div[@id='content']//div[@class='entry clearfix']");
                            var content = string.Empty;
                            if (ele_Content is not null)
                            {
                                var ele_Contents =
                                    articleDetailDoc.DocumentNode.SelectNodes("//div[@id='content']//div[@class='entry clearfix']//following-sibling::p[4]");
                                if (ele_Contents is not null && ele_Contents.Any())
                                {
                                    foreach (var elementHandle in ele_Contents)
                                    {
                                        if (elementHandle != ele_Contents.Last())
                                        {
                                            content = $"{content}\n{elementHandle.InnerHtml}";
                                        }
                                    }
                                }
                                

                                content = content.Trim();

                                content = content.RemoveHrefFromA();
                            }

                            // get tags (get from article details)
                            var ele_Hashtags = articleDetailDoc.DocumentNode.SelectNodes("//span[@class='meta_tags']/a");
                            var tags = new List<string>();
                            if (ele_Hashtags is not null && ele_Hashtags.Any())
                            {
                                foreach (var eleHashtag in ele_Hashtags)
                                {
                                    var tag = eleHashtag.InnerText;
                                    tags.Add(tag);
                                }
                            }

                            string cateName = category.Name;
                            if (parentCategory != category.Name)
                            {
                                cateName = $"{parentCategory} -> {category.Name}";
                            }

                            articlesCategory.Add(new ArticlePayload
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
        return articlePayloads;
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