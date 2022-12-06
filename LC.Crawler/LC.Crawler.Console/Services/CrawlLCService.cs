using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Helpers;
using Volo.Abp.DependencyInjection;

namespace LC.Crawler.Console.Services;

public class CrawlLCService : ITransientDependency
{
    public async Task<CrawlResult?> Execute(ICrawlLCService instance, CrawlerDataSourceItem item, CrawlerCredentialEto credential)
    {
        try
        {
            item.Url = item.Url.Trim();
            System.Console.WriteLine($"Get instance for {item.Url}");
            // var instances = GetInstance(item.Url);
            CrawlResult? crawlResult = null;

            var result = await instance.Execute(item, credential);
            if (result is null) return crawlResult;

            crawlResult = result;
            if (result.CrawlArticlePayload is not null)
            {
                crawlResult.CrawlArticlePayload = result.CrawlArticlePayload;
            }
            else if (result.CrawlEcommercePayload is not null)
            {
                crawlResult.CrawlEcommercePayload = result.CrawlEcommercePayload;
            }

            return crawlResult;
        }
        catch (Exception e)
        {
            await e.Log(string.Empty, item.Url);
            throw;
        }
    }

    private List<ICrawlLCService> GetInstance(string url)
    {
        if (url.Contains("sieuthisongkhoe"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlSieuThiSongKhoeService(),
                new CrawlSieuThiSongKhoeArticleService()
            };
        }

        if (url.Contains("aladin.com.vn"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlAladinService()
            };
        }

        if (url.Contains("suckhoedoisong.vn"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlSucKhoeDoiSongService()
            };
        }

        if (url.Contains("nhathuoclongchau.com"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlLongChauService(),
                new CrawlLongChauArticleService()
            };
        }

        if (url.Contains("alobacsi.com"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlAloBacSiService()
            };
        }

        if (url.Contains("blogsuckhoe.com"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlBlogSucKhoeService()
            };
        }

        if (url.Contains("suckhoegiadinh.com.vn"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlSucKhoeGiaDinhService()
            };
        }

        if (url.Contains("songkhoe.medplus.vn"))
        {
            return new List<ICrawlLCService>
            {
                new CrawlSongKhoeMedPlusService()
            };
        }

        return new List<ICrawlLCService>();
    }
    
}