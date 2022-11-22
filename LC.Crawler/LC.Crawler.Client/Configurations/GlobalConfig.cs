﻿namespace LC.Crawler.Client.Configurations;

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
 
}

public class GlobalConfig
{
    public CrawlConfig CrawlConfig { get; set; }
}