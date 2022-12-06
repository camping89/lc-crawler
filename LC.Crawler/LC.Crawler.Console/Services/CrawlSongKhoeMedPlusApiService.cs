using System.Collections.Concurrent;
using System.Globalization;
using System.Web;
using Dasync.Collections;
using HtmlAgilityPack;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using RestSharp;

namespace LC.Crawler.Console.Services;

public class CrawlSongKhoeMedPlusApiService : CrawlLCArticleApiBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(string url)
    {
        var crawlArticlePayload = new ConcurrentBag<ArticlePayload>();
        // foreach (var category in GlobalConfig.CrawlConfig.SongKhoeMedPlusConfig.Categories)
        // {
        //     crawlArticlePayload.AddRange(await GetArticles(category, category.Name));
        // }

        await GlobalConfig.CrawlConfig.SongKhoeMedPlusConfig.Categories.ParallelForEachAsync(async category =>
        {
            crawlArticlePayload.AddRange(await GetArticles(category, category.Name));
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

    private async Task<List<ArticlePayload>> CrawlArticles(List<ArticlePayload> articlePayloads)
    {
        var             articles   = new List<ArticlePayload>();
        try
        {
            foreach (var articlePayload in articlePayloads)
            {
                var crawlArticle = await CrawlArticle(articlePayload);
                articles.Add(crawlArticle);
            }
        }
        catch (Exception e)
        {
            System.Console.WriteLine(e);
            throw;
        }
        
        return articles.Where(_ => _.Content is not null).ToList();
    }

    private async Task<ArticlePayload> CrawlArticle(ArticlePayload articleInput)
    {
        try
        {
            var requestUrl = string.Format(articleInput.Url);
            var htmlString = await GetRawData(requestUrl);
            
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlString);
            
            System.Console.WriteLine($"CRAWLING ARTICLE {articleInput.Url}");
            
            if (articleInput.Title is null)
            {
                var ele_Title = doc.DocumentNode.SelectSingleNode("//div[@class='entry-header']//*[@class='jeg_post_title']");
                if (ele_Title is not null)
                {
                    articleInput.Title = ele_Title.InnerText;
                }
            }
            
            var ele_Content = doc.DocumentNode.SelectSingleNode("//div[@id='ftwp-postcontent']"); 
            if (ele_Content is not null)
            {
                var ele_ContentDoc = new HtmlDocument();
                ele_ContentDoc.LoadHtml(ele_Content.InnerHtml);
                
                var ele_ShortDescription = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'articleContainer__content')]");
                if (ele_ShortDescription is not null)
                {
                    articleInput.ShortDescription = ele_ShortDescription.InnerText;
                }
                else
                {
                    var shortDescription = ele_ContentDoc.DocumentNode.SelectSingleNode("//p[1]");
                    if (shortDescription is not null)
                    {
                        articleInput.ShortDescription = shortDescription.InnerText;
                    }

                    if (articleInput.ShortDescription.IsNullOrWhiteSpace())
                    {
                        shortDescription = ele_ContentDoc.DocumentNode.SelectSingleNode("//p[2]");
                        if (shortDescription is not null)
                        {
                            articleInput.ShortDescription = shortDescription.InnerText;
                        }
                    }
                }

                var content = ele_Content.InnerHtml;

                // remove advise
                var ele_Advise = ele_ContentDoc.DocumentNode.SelectSingleNode("//div[contains(@class,'code-block')]");
                if (ele_Advise is not null)
                {
                    content = content.SafeReplace(ele_Advise.InnerHtml, string.Empty);
                }

                // remove "phu luc"
                var ele_Appendix = ele_ContentDoc.DocumentNode.SelectSingleNode("//div[contains(@class,'ftwp-in-post')]");
                if (ele_Appendix is not null)
                {
                    content = content.SafeReplace(ele_Appendix.InnerHtml, string.Empty);
                }

                // remove see more
                var seeMoresKeys = new List<string>()
                {
                    "Xem thêm",
                    "Mời bạn đọc tham khảo thêm các bài viết mới nhất",
                    "Xem thêm bài viết",
                    "Nguồn tham khảo",
                    "bạn có thể xem thêm",
                    "Các bài viết cùng chủ đề có thể bạn quan tâm",
                    "Nguồn tài liệu tham khảo"
                };

                foreach (var seeMoreKey in seeMoresKeys)
                {
                    var ele_SeeMore = ele_ContentDoc.DocumentNode.SelectSingleNode($"//p[contains(text(),'{seeMoreKey}')]/following-sibling::ul");
                    if (ele_SeeMore is null)
                    {
                        ele_SeeMore = ele_ContentDoc.DocumentNode.SelectSingleNode($"//p/strong[contains(text(),'{seeMoreKey}')]/../following-sibling::ul");
                    }

                    if (ele_SeeMore is null)
                    {
                        ele_SeeMore = ele_ContentDoc.DocumentNode.SelectSingleNode($"//b[contains(text(),'{seeMoreKey}')]/../following-sibling::ul");
                    }

                    if (ele_SeeMore is not null)
                    {
                        content = content.SafeReplace(ele_SeeMore.InnerHtml, string.Empty);
                    }

                    var ele_SeeMoreText = ele_ContentDoc.DocumentNode.SelectSingleNode($"//p[contains(text(),'{seeMoreKey}')]");
                    if (ele_SeeMoreText is null)
                    {
                        ele_SeeMoreText = ele_ContentDoc.DocumentNode.SelectSingleNode($"//p/strong[contains(text(),'{seeMoreKey}')]");
                    }
                    if (ele_SeeMoreText is null)
                    {
                        ele_SeeMoreText = ele_ContentDoc.DocumentNode.SelectSingleNode($"//b[contains(text(),'{seeMoreKey}')]");
                    }

                    if (ele_SeeMoreText is not null)
                    {
                        content = content.SafeReplace(ele_SeeMoreText.InnerHtml, string.Empty);
                    }
                }

                // remove source
                var ele_Source = ele_ContentDoc.DocumentNode.SelectSingleNode("//p[contains(text(),'Nguồn:')]");
                if (ele_Source is not null)
                {
                    content = content.SafeReplace(ele_Source.InnerHtml, string.Empty);
                }

                // remove tags
                var ele_TagContainer = ele_ContentDoc.DocumentNode.SelectSingleNode("//div[contains(@class,'jeg_post_tags')]") ??
                                       doc.DocumentNode.SelectSingleNode("//div[contains(@class,'jeg_post_tags')]");
                
                if (ele_TagContainer is not null)
                {
                    var ele_TagContainerDoc = new HtmlDocument();
                    ele_TagContainerDoc.LoadHtml(ele_TagContainer.InnerHtml);
                    
                    content = content.SafeReplace(ele_TagContainer.InnerHtml, string.Empty);
                    var ele_Tags = ele_TagContainerDoc.DocumentNode.SelectNodes("//a");
                    if (ele_Tags.IsNotNullOrEmpty())
                    {
                        articleInput.Tags = new List<string>();
                        foreach (var ele_Tag in ele_Tags)
                        {
                            var tag = ele_Tag.InnerText;
                            articleInput.Tags.Add(tag);
                        }
                    }
                }

                articleInput.Content = content.RemoveHrefFromA();
            }
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, string.Empty);
        }

        return articleInput;
    }

    private async Task<List<ArticlePayload>> GetArticles(Category category, string parentCategory)
    {
        var articlePayloads = new ConcurrentBag<ArticlePayload>();
        if (category.SubCategories is not null && category.SubCategories.Any())
        {
            foreach (var subCategory in category.SubCategories)
            {
                articlePayloads.AddRange(await GetArticles(subCategory, parentCategory));  
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
                    
                    var ele_NotFound = doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'jeg_archive_title') and text() = 'Page Not Found']");
                    if (ele_NotFound is not null)
                    {
                        break;
                    }

                    var ele_Articles = doc.DocumentNode.SelectNodes("//div[contains(@class,'jeg_post')]/article");
                    if (ele_Articles is null || !ele_Articles.Any())
                    {
                        break;
                    }

                    foreach (var eleArticle in ele_Articles)
                    {
                        try
                        {
                            var eleArticleDoc = new HtmlDocument();
                            eleArticleDoc.LoadHtml(eleArticle.InnerHtml);
                            
                            var ele_CreatedAt   = eleArticleDoc.DocumentNode.SelectSingleNode("//div[contains(@class,'jeg_post_meta')]/div[contains(@class,'jeg_meta_date')]");
                            var createdAt       = string.Empty;
                            var formatCreatedAt = DateTime.MinValue;
                            if (ele_CreatedAt is not null)
                            {
                                createdAt = ele_CreatedAt.InnerText.ToUpper();
                                createdAt = createdAt.SafeReplace("THÁNG MƯỜI HAI", "December")
                                    .SafeReplace("THÁNG MƯỜI MỘT", "November")
                                    .SafeReplace("THÁNG MƯỜI",     "October")
                                    .SafeReplace("THÁNG CHÍN",     "September")
                                    .SafeReplace("THÁNG TÁM",      "August")
                                    .SafeReplace("THÁNG BẢY",      "July")
                                    .SafeReplace("THÁNG SÁU",      "June")
                                    .SafeReplace("THÁNG NĂM",      "May")
                                    .SafeReplace("THÁNG TƯ",       "April")
                                    .SafeReplace("THÁNG BA",       "March")
                                    .SafeReplace("THÁNG HAI",      "February")
                                    .SafeReplace("THÁNG MỘT",      "January")
                                    .Trim();
                            }
                            
                            if (createdAt.IsNotNullOrEmpty())
                            {
                                formatCreatedAt = DateTime.ParseExact(createdAt, "d MMMM, yyyy", CultureInfo.CurrentCulture);
                                if (!await IsValidArticle(articlesCategory.Count, string.Empty, formatCreatedAt))
                                {
                                    isValidArticle = false;
                                    break;
                                }
                            }
                            
                            // get url (get from list articles)
                            var ele_Url    = eleArticleDoc.DocumentNode.SelectSingleNode("//div[contains(@class,'jeg_thumb')]/a");
                            var articleUrl = string.Empty;
                            if (ele_Url is not null)
                            {
                                articleUrl = ele_Url.Attributes["href"].Value;
                            }

                            // get image (get from list articles)
                            var articleImageSrc = string.Empty;
                            var ele_Image = eleArticleDoc.DocumentNode.SelectSingleNode("//div[contains(@class,'jeg_thumb')]//img");
                            if (ele_Image is not null)
                            {
                                articleImageSrc = ele_Image.Attributes["data-src"].Value;
                            }

                            // get title (get from list articles)
                            string articleTitle = string.Empty;
                            var ele_Title = eleArticleDoc.DocumentNode.SelectSingleNode("//h3[@class='jeg_post_title']");
                            if (ele_Title is not null)
                            {
                                articleTitle = ele_Title.InnerText;
                            }

                            var categoryName = parentCategory;
                            if (categoryName != category.Name)
                            {
                                categoryName = $"{parentCategory} -> {category.Name}";
                            }
                            var articlePayload = new ArticlePayload()
                            {
                                Url = articleUrl,
                                Title = articleTitle,
                                FeatureImage = articleImageSrc,
                                CreatedAt = formatCreatedAt,
                                Category = categoryName
                            };
                            
                            articlesCategory.Add(articlePayload);

                            System.Console.WriteLine($"Page: {requestUrl} - Url: {articlePayload.Url} - Category: {articlePayload.Category}");
                        }
                        catch (Exception e)
                        {
                            await e.Log(string.Empty, string.Empty);
                        }
                    }
                }
                
                if (!isValidArticle)
                {
                    break;
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