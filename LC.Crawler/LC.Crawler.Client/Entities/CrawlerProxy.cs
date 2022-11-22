using JetBrains.Annotations;

namespace LC.Crawler.Client.Entities;

public class CrawlerProxy
{
    public Guid Id { get; set; }
    [NotNull]
    public virtual string Ip { get; set; }

    public virtual int Port { get; set; }

    [NotNull]
    public virtual string Protocol { get; set; }

    [CanBeNull]
    public virtual string Username { get; set; }

    [CanBeNull]
    public virtual string Password { get; set; }

    public virtual DateTime? PingedAt { get; set; }

    public virtual bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; }
}