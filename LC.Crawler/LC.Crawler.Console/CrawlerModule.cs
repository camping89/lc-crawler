using Volo.Abp.Autofac;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.RabbitMQ;

namespace Veek.DataProvider.Crawler.Console;

[DependsOn(
    typeof(AbpEventBusRabbitMqModule),
    typeof(AbpAutofacModule)
)]
public class CrawlerModule: AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Configure<AbpRabbitMqEventBusOptions>(options =>
        // {
        //     options.ClientName = "Veek.DataProvider.Consumer";
        //     options.ExchangeName = "Veek.DataProvider.Exchange";
        // });
        // Configure<AbpRabbitMqOptions>(options =>
        // {
        //     // options.Connections.Default.UserName = "user";
        //     // options.Connections.Default.Password = "pass";
        //     options.Connections.Default.HostName = "103.163.214.53";
        //     options.Connections.Default.Port = 5672;
        // });
    }
}