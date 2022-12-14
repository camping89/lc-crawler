using System.Collections.Concurrent;
using System.Globalization;
using System.Web;
using Flurl;
using HtmlAgilityPack;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using RestSharp;

namespace LC.Crawler.Console.Services.CrawlByAPI;

public class CrawlAladinArticleApiService : CrawlLCArticleApiBaseService
{
    protected override async Task<ConcurrentBag<ArticlePayload>> GetArticlePayload(CrawlArticlePayload crawlArticlePayload)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        foreach (var category in GlobalConfig.CrawlConfig.AladinConfig.Categories)
        {
            articlePayloads.AddRange(await CrawlArticles(category, crawlArticlePayload.Url));
        }

        return articlePayloads;
    }

    private async Task<ConcurrentBag<ArticlePayload>> CrawlArticles(Category category, string mainUrl)
    {
        var articlesCategory = new ConcurrentBag<ArticlePayload>();
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

                    var ele_Articles = doc.DocumentNode.SelectNodes("//div[@id='content']/div[@class='news-item']");
                    if (ele_Articles is not null && ele_Articles.Any())
                    {
                        foreach (var eleArticle in ele_Articles)
                        {
                            var articlePayload = new ArticlePayload
                            {
                                Category = category.Name
                            };
                            var articleDoc = new HtmlDocument();
                            articleDoc.LoadHtml(eleArticle.InnerHtml);

                            var ele_Url = articleDoc.DocumentNode.SelectSingleNode("//a");
                            if (ele_Url is not null)
                            {
                                var articleUrl = ele_Url.Attributes["href"].Value;
                                articlePayload.Url = Url.Combine(mainUrl, articleUrl);
                            }

                            var ele_Title = articleDoc.DocumentNode.SelectSingleNode("//a/h3");
                            if (ele_Title is not null)
                            {
                                var title = ele_Title.InnerText;

                                articlePayload.Title = title;
                            }

                            var ele_ShortDescription = articleDoc.DocumentNode.SelectSingleNode("//p");
                            if (ele_ShortDescription is not null)
                            {
                                var shortDescription = ele_ShortDescription.InnerText;

                                articlePayload.ShortDescription = shortDescription;
                            }

                            var ele_Image = articleDoc.DocumentNode.SelectSingleNode("//a/img");
                            if (ele_Image is not null)
                            {
                                var image = ele_Image.Attributes["src"].Value;
                                articlePayload.FeatureImage = image;
                            }

                            // Crawl Details
                            var articleString = await GetRawData(articlePayload.Url);
                            var articleDetails = new HtmlDocument();
                            articleDetails.LoadHtml(articleString);
                            System.Console.WriteLine($"Crawl Details: {category.Name} - {articlePayload.Url}");
                            var ele_DateTime = articleDetails.DocumentNode.SelectSingleNode("//div[contains(@class,'page_date')]//em");
                            if (ele_DateTime is not null)
                            {
                                var dateTimeString = ele_DateTime.InnerText;
                                dateTimeString = dateTimeString.Replace("C???p nh???t", string.Empty).Trim();
                                dateTimeString = dateTimeString.Split(",")[0].Trim();

                                var isValid = DateTime.TryParseExact(dateTimeString, "dd-MM-yyyy", DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None, out var dateTime);
                                if (isValid)
                                {
                                    if (!await IsValidArticle(articlesCategory.Count, string.Empty, dateTime))
                                    {
                                        isValidArticle = false;
                                        break;
                                    }

                                    articlePayload.CreatedAt = dateTime;

                                    var ele_Content = articleDetails.DocumentNode.SelectSingleNode("//div[@class='entry-content']");
                                    if (ele_Content is not null)
                                    {
                                        var content = ele_Content.InnerHtml;
                                        
                                        var contentDoc = new HtmlDocument();
                                        contentDoc.LoadHtml(content);
                                        
                                        var ele_Remove = contentDoc.DocumentNode.SelectSingleNode("//span[@class='golink']");
                                        if (ele_Remove is not null)
                                        {
                                            var html = ele_Remove.InnerHtml;
                                            content = content.SafeReplace(html, string.Empty);
                                        }

                                        ele_Remove = contentDoc.DocumentNode.SelectSingleNode("//a[contains( text(),'Vi??n u???ng h??? tr??? gi???c ng??? ngon Mamori')]");
                                        if (ele_Remove is not null)
                                        {
                                            var html = ele_Remove.InnerHtml;
                                            content = content.SafeReplace(html, string.Empty);
                                        }

                                        ele_Remove = contentDoc.DocumentNode.SelectSingleNode(
                                            "//a[contains(@href,'https://nikenko.vn/danh-muc-san-pham/bo-nao-va-tang-cuong-tri-nho/vien-uong-ho-tro-ngu-ngon-mamori-glycine-l-theanine')]");
                                        if (ele_Remove is not null)
                                        {
                                            var html = ele_Remove.InnerHtml;
                                            content = content.SafeReplace(html, string.Empty);
                                        }

                                        ele_Remove = contentDoc.DocumentNode.SelectSingleNode("//p[contains(text(),'tham kh???o c??c s???n ph???m b???')]");
                                        if (ele_Remove is not null)
                                        {
                                            var html = ele_Remove.InnerHtml;
                                            content = content.SafeReplace(html, string.Empty);
                                        }

                                        ele_Remove = contentDoc.DocumentNode.SelectSingleNode("//p[contains(text(),'Link ?????t mua s???n ph???m')]");
                                        if (ele_Remove is not null)
                                        {
                                            var html = ele_Remove.InnerHtml;
                                            content = content.SafeReplace(html, string.Empty);
                                        }

                                        ele_Remove = contentDoc.DocumentNode.SelectSingleNode("//em[contains(text(),'Link ?????t mua s???n ph???m')]");
                                        if (ele_Remove is not null)
                                        {
                                            var html = ele_Remove.InnerHtml;
                                            content = content.SafeReplace(html, string.Empty);
                                        }

                                        ele_Remove = contentDoc.DocumentNode.SelectSingleNode("//p[contains(text(),'Mua ngay s???n ph???m gi?? ??u ????i')]");
                                        if (ele_Remove is not null)
                                        {
                                            var html = ele_Remove.InnerHtml;
                                            content = content.SafeReplace(html, string.Empty);
                                        }

                                        articlePayload.Content = content.RemoveHrefFromA();

                                        articlesCategory.Add(articlePayload);
                                    }
                                }
                            }
                        }

                        if (!isValidArticle)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
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
        } while (htmlString.IsNotNullOrWhiteSpace());

        return articlesCategory;
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