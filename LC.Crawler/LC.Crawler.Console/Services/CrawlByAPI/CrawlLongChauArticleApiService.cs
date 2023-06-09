using System.Collections.Concurrent;
using System.Globalization;
using Dasync.Collections;
using LC.Crawler.Client.Entities;
using LC.Crawler.Client.Entities.LongChau;
using LC.Crawler.Core.Extensions;
using LC.Crawler.Core.Helpers;
using Newtonsoft.Json;
using RestSharp;
using Category = LC.Crawler.Client.Configurations.Category;
using Url = Flurl.Url;

namespace LC.Crawler.Console.Services.CrawlByAPI;

public class CrawlLongChauArticleApiService : CrawlLCArticleApiBaseService
{
    private const string LongChauUrl = "https://nhathuoclongchau.com.vn/";
    
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
                var lcArticleDetail = await GetRawData(articlePayload.Url);
                if (lcArticleDetail is not null)
                {
                    articlePayload.ShortDescription = lcArticleDetail.pageProps.detail.data.shortDescription;
                    articlePayload.Category = lcArticleDetail.pageProps.detail.breadcrumb.Select(_ => _.breadcrumbName).JoinAsString(" -> ");
                    articlePayload.Tags = lcArticleDetail.pageProps.detail.data.tags.Select(_ => _.title).ToList();
                    articlePayload.CreatedAt = lcArticleDetail.pageProps.detail.data.createdAt;

                    if (GlobalConfig.CrawlConfig.ValidateArticleByDateTime)
                    {
                        if (!await IsValidArticle(articlePayload.CreatedAt))
                        {
                            break;
                        }
                    }

                    // remove useless value in content
                    var content = lcArticleDetail.pageProps.detail.data.description;
                    content = content.RemoveHrefFromA();
                    articlePayload.Content = content;

                    System.Console.WriteLine(JsonConvert.SerializeObject(articlePayload));

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
        var offset = 0;
        var limit = 10;
        bool isValidArticle = true;
        while (isValidArticle)
        {
            try
            {
                var requestUrl = string.Format(category.Url, limit, offset);
                var lcArticleApiResponse = await GetArticleResponse(requestUrl);
                if (lcArticleApiResponse is null) break;
                if (lcArticleApiResponse.data.results.IsNullOrEmpty() ||
                    lcArticleApiResponse.data.results.Any(_=>_.id == 0))
                {
                    isValidArticle = false;
                }
                else
                {
                    foreach (var dataResult in lcArticleApiResponse.data.results)
                    {
                        System.Console.WriteLine($"Url: {dataResult.slug}");
                        var articlePayload = new ArticlePayload
                        {
                            Url = Url.Combine(LongChauUrl, dataResult.slug),
                            Title = dataResult.name,
                            FeatureImage = dataResult.primaryImage.url
                        };

                        if (!GlobalConfig.CrawlConfig.ValidateArticleByDateTime)
                        {
                            if (!await IsValidArticle(articlesCategory.Count))
                            {
                                isValidArticle = false;
                                break;
                            }
                        }

                        articlesCategory.Add(articlePayload);
                    }
                    
                }
            }
            catch (Exception e)
            {
                await e.Log(string.Empty, string.Empty);
            }
            finally
            {
                offset += limit;
            }
        }

        articlePayloads.AddRange(articlesCategory);

        return articlePayloads.ToList();
    }

    private async Task<LCArticleDetail> GetRawData(string url)
    {
        // https://nhathuoclongchau.com.vn/_next/data/4qkGK4Rz6hWfGCqbsEv8U/bai-viet/detail/la-e-an-song-duoc-khong-cac-bai-thuoc-tu-la-e.html.json
        var postUrl = url.Split("bai-viet")[1].Trim('/');
        url = url.Replace("bai-viet", "_next/data/4qkGK4Rz6hWfGCqbsEv8U/bai-viet/detail");
        url = $"{url}.json?postSlug={postUrl}";
        // url = "https://nhathuoclongchau.com.vn/_next/data/4qkGK4Rz6hWfGCqbsEv8U/bai-viet/detail/la-e-an-song-duoc-khong-cac-bai-thuoc-tu-la-e.html.json";
        var client = new RestClient(url);
        var request = new RestRequest();

        var response = await client.ExecuteGetAsync<LCArticleDetail>(request);
        using (var autoResetEvent = new AutoResetEvent(false))
        {
            autoResetEvent.WaitOne(100);
        }
        return response.Data;
    }

    private async Task<LCArticleApiResponse> GetArticleResponse(string url)
    {
        var client = new RestClient(url);
        var request = new RestRequest();
        
        var response = await client.ExecuteGetAsync<LCArticleApiResponse>(request);
        using (var autoResetEvent = new AutoResetEvent(false))
        {
            autoResetEvent.WaitOne(100);
        }
        return response.Data;
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

public class LCArticleApiResponse
{
    public int code { get; set; }
    public string message { get; set; }
    public Data data { get; set; }
}

public class Result
{
    public int id { get; set; }
    public string name { get; set; }
    public string slug { get; set; }
    public object redirectUrl { get; set; }
    public string shortDescription { get; set; }
    public PrimaryImage primaryImage { get; set; }
    public LCArticleApiCategory category { get; set; }
}

public class PrimaryImage
{
    public int id { get; set; }
    public string alternativeText { get; set; }
    public string caption { get; set; }
    public string ext { get; set; }
    public string mime { get; set; }
    public string url { get; set; }
    public string name { get; set; }
    public string hash { get; set; }
    public int width { get; set; }
    public int height { get; set; }
}

public class Data
{
    public List<Result> results { get; set; }
    public int totalCount { get; set; }
}

public class LCArticleApiCategory
{
    public bool isPrimary { get; set; }
    public int id { get; set; }
    public string name { get; set; }
    public string fullPathSlug { get; set; }
}