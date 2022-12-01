namespace LC.Crawler.Client.Configurations;

public class CrawlConfig
{
    public string RootUrl { get; set; }
    public string FairRootUrl { get; set; }
    public string UserDataDirRoot { get; set; }
    public int ActionDelay { get; set; }
    public int TypingDelay { get; set; }
    public int ScrollTimeout { get; set; }
    public int Crawl_MaxThread { get; set; }
    public int Crawl_BatchSize { get; set; }
    public bool ValidateArticleByDateTime { get; set; }
    public int TotalArticles { get; set; }
    public int DaysIntervalArticles { get; set; }
    public SucKhoeDoiSongConfig SucKhoeDoiSongConfig { get; set; }
    public LongChauArticleConfig LongChauArticleConfig { get; set; }
    public BlogSucKhoeConfig BlogSucKhoeConfig { get; set; }
    public SongKhoeMedPlusConfig SongKhoeMedPlusConfig { get; set; }
    public SieuThiSongKhoeConfig SieuThiSongKhoeConfig { get; set; }
    public SucKhoeGiaDinhConfig SucKhoeGiaDinhConfig { get; set; }
    public AladinConfig AladinConfig { get; set; }
    public SiteConfig AlobacsiConfig { get; set; }
}

public class GlobalConfig
{
    public CrawlConfig CrawlConfig { get; set; }
}

public class SiteConfig
{
    public IList<Category> Categories { get; set; }
}

public class LongChauArticleConfig
{
    public IList<Category> Categories { get; set; }
}

public class BlogSucKhoeConfig
{
    public IList<Category> Categories { get; set; }
}

public class SongKhoeMedPlusConfig
{
    public IList<Category> Categories { get; set; }
}

public class SieuThiSongKhoeConfig
{
    public IList<Category> Categories { get; set; }
}

public class SucKhoeGiaDinhConfig
{
    public IList<Category> Categories { get; set; }
}

public class AladinConfig
{
    public IList<Category> Categories { get; set; }
}

public class Category
{
    public string Name { get; set; }
    public string Url { get; set; }
    public IList<Category> SubCategories { get; set; }
}