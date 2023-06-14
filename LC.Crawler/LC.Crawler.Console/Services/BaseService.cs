using System.Reflection;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using LC.Crawler.Core.Extensions;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;

namespace LC.Crawler.Console.Services;

public class BaseService
{
    protected GlobalConfig GlobalConfig { get; init; }
    private ILog Logger { get; set; }
    
    protected CrawlerProxy CrawlerProxy = new();
    
    protected BaseService()
    {
        var config = InitConfig();
        GlobalConfig = config;
        Logger = LogManager.GetLogger(typeof(Program));
    }
    
    protected CrawlerProxy GetCrawlProxy()
    {
        string[] lines = File.ReadAllLines(@"D:\Workspace\lc-crawler\proxy.txt");
        var random = new Random();
        int index = random.Next(lines.Length);
        var line = lines[index];
        var listItems = line.Split(':');
        var crawlerProxy = new CrawlerProxy
        {
            Ip = listItems[0],
            Port = listItems[1].ToIntOrDefault(),
            Username = listItems[2],
            Password = listItems[3]
        };

        return crawlerProxy;
    }

    
    protected void InitLogConfig(CrawlerCredentialEto credential)
    {
        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
        if (credential?.CrawlerAccount != null)
        {
            GlobalContext.Properties["fname"] = credential.CrawlerAccount.Username;
        }
        else
        {
            GlobalContext.Properties["fname"] = "Crawler";
        }
            
        XmlConfigurator.Configure(logRepository, new FileInfo("Configurations/log4net.config"));
    }

    private static GlobalConfig InitConfig()
    {
        var configRoot = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("Configurations/globalconfigs.json").Build();
        var section = configRoot.GetSection(nameof(GlobalConfig));
        var config = section.Get<GlobalConfig>();

        return config;
    }
}