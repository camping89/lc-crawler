using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using RestSharp;

namespace LC.Crawler.Console.Services;

public class CrawlSucKhoeDoiSongApiService : CrawlLCArticleApiBaseService
{
    protected override async Task<CrawlArticlePayload> GetCrawlArticlePayload(string url)
    {
        foreach (var category in GlobalConfig.CrawlConfig.SucKhoeDoiSongConfig.Categories)
        {
            string htmlString;
            var pageNumber = 1;
            do
            {
                var requestUrl = string.Format(category.Url, pageNumber);
                htmlString = await GetRawData(requestUrl);
                pageNumber = pageNumber + 1;
            } while (htmlString.IsNotNullOrWhiteSpace());
            
        }
        
        return await Task.Factory.StartNew(() => new CrawlArticlePayload());
    }

    private async Task<string> GetRawData(string url)
    {
        var client = new RestClient(url);
        var request = new RestRequest();
        request.AddHeader("x-requested-with", "XMLHttpRequest");
        request.AddHeader("Content-Type", "application/text; charset=utf-8");
        request.AddHeader("accept", "text/html, */*; q=0.01");
        request.AddHeader("accept-encoding", "gzip, deflate, br");

        var response = await client.GetAsync<string>(request);
        return response;
    }
}