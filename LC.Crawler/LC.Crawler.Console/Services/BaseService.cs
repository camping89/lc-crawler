using System.Reflection;
using LC.Crawler.Client.Configurations;
using LC.Crawler.Client.Entities;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;

namespace LC.Crawler.Console.Services;

public class BaseService
{
    protected GlobalConfig GlobalConfig { get; init; }
    private ILog Logger { get; set; }
    
    protected BaseService()
    {
        var config = InitConfig();
        GlobalConfig = config;
        Logger = LogManager.GetLogger(typeof(Program));
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