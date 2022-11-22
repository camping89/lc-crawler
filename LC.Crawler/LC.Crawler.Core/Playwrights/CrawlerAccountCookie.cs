namespace LC.Crawler.Core.Playwrights;

public class CrawlerAccountCookie
{
    public string Domain { get; set; }
    public float? Expires { get; set; }
    public bool HttpOnly { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public bool Secure { get; set; }
    public string Value { get; set; }
}