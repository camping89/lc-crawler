using Microsoft.Playwright;

namespace LC.Crawler.Core.Playwrights
{
    public class PlaywrightContext
    {
        public IPlaywright Playwright { get; set; }
        public IBrowser Browser { get; set; }
        public IBrowserContext BrowserContext { get; set; }
    }

}