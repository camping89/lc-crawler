namespace LC.Crawler.Core.Enums;

public enum CrawlStatus
{
    OK = 100,
    PostUnavailable = 101,
    GroupUnavailable = 102,
        
    AccountBanned = 200,
    BlockedTemporary = 201,
        
    LoginFailed = 400,
    LoginApprovalNeeded = 401,
        
    UnknownFailure = 999
}