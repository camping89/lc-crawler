using Newtonsoft.Json;

namespace LC.Crawler.Client.Entities.LongChau;

public class LCArticleDetail
{
    public PageProps pageProps { get; set; }
    public bool __N_SSG { get; set; }
}

public class PageProps
    {
        public List<Menu> menu { get; set; }
        public Header header { get; set; }
        public Footer footer { get; set; }
        public Detail detail { get; set; }
        public string slug { get; set; }
    }
    
    public class Detail
    {
        public List<Breadcrumb> breadcrumb { get; set; }
        public Data data { get; set; }
        public Seo seo { get; set; }
    }
    
    public class Data
    {
        public int id { get; set; }
        public string name { get; set; }
        public DateTime createdAt { get; set; }
        public string slug { get; set; }
        public bool isApproved { get; set; }
        public string redirectUrl { get; set; }
        public bool isFeatured { get; set; }
        public string shortDescription { get; set; }
        public string description { get; set; }
        public string referenceSource { get; set; }
        public PrimaryImage primaryImage { get; set; }
        public object images { get; set; }
        public object approver { get; set; }
        public List<Tag> tags { get; set; }
        public Seo seo { get; set; }
        public Category category { get; set; }
        public ParentCategory parentCategory { get; set; }
        public List<RelatedArticle> relatedArticles { get; set; }
        public string aetiologies { get; set; }
        public List<Category> categories { get; set; }
        public string diagnoseAndTreat { get; set; }
        public string diagnoseAndTreaty { get; set; }
        public string diseaseType { get; set; }
        public string headline { get; set; }
        public string livingAndPreventive { get; set; }
        public string risk { get; set; }
        public List<SliderImage> sliderImages { get; set; }
        public string symptom { get; set; }
        public Attributes attributes { get; set; }
    }

public class SliderImage
{
    
}

public class Category
    {
        public bool isPrimary { get; set; }
        public int id { get; set; }
        public string name { get; set; }
        public string fullPathSlug { get; set; }
        public string note { get; set; }
    }
    
    public class Attributes
    {
        public string slug { get; set; }
        public string compoundName { get; set; }
    }
    
    public class RelatedArticle
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public object redirectUrl { get; set; }
    }
    
    public class ParentCategory
    {
        public int id { get; set; }
        public string name { get; set; }
        public string fullPathSlug { get; set; }
    }
    
    public class Tag
    {
        public int id { get; set; }
        public string title { get; set; }
        public string slug { get; set; }
    }
    
    public class Seo
    {
        public List<MetaSocial> metaSocial { get; set; }
        public string robots { get; set; }
        public string canonical { get; set; }
        public string title { get; set; }

        [JsonProperty("og:title")]
        public string ogtitle { get; set; }
        public string description { get; set; }

        [JsonProperty("og:description")]
        public string ogdescription { get; set; }

        [JsonProperty("og:url")]
        public string ogurl { get; set; }

        [JsonProperty("og:image")]
        public string ogimage { get; set; }
    }
    
    public class MetaSocial
    {
        public string socialNetwork { get; set; }
        public Image image { get; set; }
        public string title { get; set; }
        public string description { get; set; }
    }
    
    public class PrimaryImage
    {
        public int id { get; set; }
        public string alternativeText { get; set; }
        public object caption { get; set; }
        public string ext { get; set; }
        public string mime { get; set; }
        public string url { get; set; }
        public string name { get; set; }
        public string hash { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
    
    public class Breadcrumb
    {
        public string breadcrumbName { get; set; }
        public string path { get; set; }
    }
    
    public class Footer
    {
        public Background background { get; set; }
        public string copyRight { get; set; }
        public List<Item> items { get; set; }
        public CallCenter callCenter { get; set; }
        public Certificated certificated { get; set; }
        public Connect connect { get; set; }
        public App app { get; set; }
        public int totalShops { get; set; }
    }
    
    public class Item
    {
        public string title { get; set; }
        public List<Item> items { get; set; }
        public string text { get; set; }
        public string redirectUrl { get; set; }
        public string phone { get; set; }
        public string note { get; set; }
        public Icon icon { get; set; }
    }
    
    public class App
    {
        public string title { get; set; }
        public Qr qr { get; set; }
        public List<Item> items { get; set; }
    }
    
    public class Qr
    {
        public string url { get; set; }
        public string alt { get; set; }
        public string name { get; set; }
    }
    
    public class Connect
    {
        public string title { get; set; }
        public List<Item> items { get; set; }
    }
    
    public class Certificated
    {
        public string title { get; set; }
        public List<Item> items { get; set; }
    }
    
    public class CallCenter
    {
        public string title { get; set; }
        public List<Item> items { get; set; }
    }
    
    public class Icon
    {
        public string url { get; set; }
        public string alt { get; set; }
        public string name { get; set; }
    }
    
    public class Header
    {
        public Logo logo { get; set; }
        public TopSearch topSearch { get; set; }
    }
    
    public class Logo
    {
        public string url { get; set; }
        public string alt { get; set; }
        public string name { get; set; }
    }
    
    public class TopSearch
    {
        public object icon { get; set; }
        public List<Keyword> keywords { get; set; }
        public Background background { get; set; }
    }
    
    public class Background
    {
        public Web web { get; set; }
        public Mobile mobile { get; set; }
    }
    
    public class Web
    {
        public string url { get; set; }
        public string alt { get; set; }
        public string name { get; set; }
    }
    
    public class Mobile
    {
        public string url { get; set; }
        public string alt { get; set; }
        public string name { get; set; }
    }
    
    public class Keyword
    {
        public int id { get; set; }
        public string keyword { get; set; }
        public string url { get; set; }
    }
    
    public class Menu
    {
        public string __component { get; set; }
        public string fullPathSlug { get; set; }
        public string name { get; set; }
        public Image image { get; set; }
        public List<Child> children { get; set; }
    }
    
    public class Image
    {
        public string url { get; set; }
        public string alt { get; set; }
        public string name { get; set; }
        public int id { get; set; }
        public string alternativeText { get; set; }
        public object caption { get; set; }
        public string ext { get; set; }
        public string mime { get; set; }
        public string hash { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
    
    public class Child
    {
        public string fullPathSlug { get; set; }
        public string name { get; set; }
        public Image image { get; set; }
        public List<Child> children { get; set; }
        public List<object> products { get; set; }
        public List<string> listSku { get; set; }
        public Ingredients ingredients { get; set; }
    }
    
    public class Ingredients
    {
        // public List<Data> data { get; set; }
    }