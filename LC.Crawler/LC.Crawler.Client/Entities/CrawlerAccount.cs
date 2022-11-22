using JetBrains.Annotations;
using LC.Crawler.Core.Enums;
using LC.Crawler.Core.Playwrights;

namespace LC.Crawler.Client.Entities;

public class CrawlerAccount
{
    public Guid Id { get; set; }
    [NotNull]
    public virtual string Username { get; set; }

    [NotNull]
    public virtual string Password { get; set; }

    [NotNull]
    public virtual string TwoFactorCode { get; set; }

    public virtual AccountType AccountType { get; set; }

    public virtual AccountStatus AccountStatus { get; set; }

    [NotNull]
    public virtual string Email { get; set; }

    [NotNull]
    public virtual string EmailPassword { get; set; }

    public virtual bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; }
    
    public List<CrawlerAccountCookie> Cookies { get; set; }
}