using System;
using System.Collections.Generic;
using System.Text;
using AutomationBase;
using OpenQA.Selenium.Chrome;

namespace SpotifyDownloader.Infrastructure
{
    public static class ChromeDriverHandler
    {
        public static ChromeDriver GetDriver(string downloadDirectory, bool headless = true, bool killOtherProcesses = true)
        {
            var builder = ChromeDriverHelper.GetDriverBuilder()
                .AllowRunningInsecureContent()
                .SetDownloadPath(downloadDirectory);

            if (headless)
            {
                //builder.Headless();
            }

            return builder.Build(killOtherProcesses);
        }

        public static void CheckUpdateChromeDriver()
        {
            Console.WriteLine("Checking ChromeDriver files");
            ChromeDriverHelper.CheckUpdateChromeDriver();
            Console.WriteLine("ChromeDriver working wonderfully\n");
        }
    }
}
