using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutomationBase;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SpotifyDownloader.Infrastructure;

namespace SpotifyDownloader.Logic
{
    public class MusicDownloader : IDisposable
    {
        public MusicDownloader(string outputDirectory)
        {
            Console.WriteLine($"Setting output directory '{outputDirectory}'\n");
            DownloadDirectory = outputDirectory;

            IOHandler.CreateDirectory(DownloadDirectory);
            IOHandler.SetDirectoryPermission(DownloadDirectory);

            ChromeDriverHandler.CheckUpdateChromeDriver();

            Console.WriteLine("Instantiating main driver\n");
            Task.Run(() => { MainDriver = ChromeDriverHandler.GetDriver(DownloadDirectory); });
        }

        public void DownloadSpotifyPlaylist(string playlistLink, bool runInParallel)
        {
            Console.WriteLine($"Got playlist link '{playlistLink}'\n");
            Console.WriteLine($"Application {(runInParallel ? "will" : "won't")} run in parallel {(runInParallel ? $"using {Environment.ProcessorCount} logical processors" : string.Empty)}\n");

            Console.WriteLine("Waiting for main driver\n");
            while(MainDriver == null) {  }

            var songsNames = GetSongsNamesFromPlaylistLink(playlistLink).ToList();
            RefineSongsNames(songsNames);

            Count = 0;
            Total = songsNames.Count;

            ConcurrentBag<string> processingPool = null;
            var processed = new List<string>();

            if (!runInParallel) 
            {
                songsNames.ToList().ForEach(songName => DownloadSongFromMyFreeMp3(MainDriver, songName, runInParallel, processed));
            }
            else
            {
                processingPool = songsNames.ToConcurrentBag();
                var arr = new string[songsNames.Count];

                songsNames.CopyTo(arr);
                processed = arr.ToList();
                ParallelDrivers = new List<ChromeDriver>();

                // Number of concurrent drivers should be the same as the logical processors count
                Task.Run(() => Parallel.For(0, Environment.ProcessorCount, i =>
                {
                    var driver = ChromeDriverHandler.GetDriver(DownloadDirectory, killOtherProcesses: false);
                    ParallelDrivers.Add(driver);

                    while (!processingPool.IsEmpty)
                    {
                        processingPool.TryTake(out var songName);
                        DownloadSongFromMyFreeMp3(driver, songName, runInParallel, processed);
                    }
                }));
            }

            var youtubeWritten = false;
            while (processed.Count > 0 || 
                   Directory.GetFiles(DownloadDirectory).Any(x => x.EndsWith("crdownload")) ||
                   FromYoutubeDownloads?.Count > 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                if (processed.Count == 0 &&
                    FromYoutubeDownloads?.Count > 0 &&
                    !youtubeWritten)
                {
                    Console.WriteLine("\nWaiting for youtube downloads\n" +
                                      $"{string.Join('\n', FromYoutubeDownloads)}\n" +
                                      "Please be calm, those really take a long time");
                    youtubeWritten = true;
                }
            }

            IOHandler.RenameFilesRemovingString(DownloadDirectory, "my-free-mp3s.com");
        }

        private int Count { get; set; }

        private int Total { get; set; }

        private void DownloadSongFromMyFreeMp3(ChromeDriver driver, string songName, bool runInParallel, List<string> processed)
        {
            driver.Navigate().GoToUrl("https://myfreemp3c.com/");
            driver.WaitForPageToLoad();

            driver.WaitForElementToBeDisplayed(By.Id("query"));
            var elQuery = driver.FindElement(By.Id("query"));
            elQuery.SendKeys(songName);

            var elSubmit = driver.FindElement(By.CssSelector("body > div.wrapper > div.container > div > span > button"));
            elSubmit.Click();

            driver.WaitForPageToLoad();

            driver.WaitForElementToBeDisplayed(By.Id("result"));
            var elResult = driver.FindElement(By.Id("result"));

            driver.WaitForElementToBeDisplayed(By.CssSelector("#result > div.list-group"));
            var elResultList = elResult.FindElement(By.CssSelector("#result > div.list-group"));

            driver.WaitForElementToBeDisplayed(By.ClassName("list-group-item"));
            var elListItems = elResultList.FindElements(By.ClassName("list-group-item"));

            var tryYoutube = false;
            if (elListItems.Count == 1 && (elListItems.FirstOrDefault()?.Text.ToLowerInvariant().Contains("your request was not found")).GetValueOrDefault())
            {
                tryYoutube = true;
                
                Console.WriteLine($"Song '{songName}' not found to download\n" +
                                  "Will try downloading from youtube\n");

                Task.Run(() => DownloadSongFromYoutube(driver, songName));

                if (runInParallel)
                {
                    processed.Remove(songName);
                }

                return;
            }

            var items = new Dictionary<string, string>();
            elListItems.Take(10).ToList().ForEach(item =>
            {
                var key = string.Join(" - ", item.FindElements(By.CssSelector("#navi")).Select(x => x.Text));
                if (items.ContainsKey(key)) return;

                var value = driver.FindElement(By.CssSelector("#result > div.list-group > li:nth-child(1) > a.name")).GetAttribute("href");
                items.Add(key, value);
            });

            driver.Navigate().GoToUrl(items.FirstOrDefault().Value);

            if (runInParallel && !tryYoutube)
            {
                processed.Remove(songName);
            }

            Count++;
            Console.WriteLine($"Downloading song {Count}/{Total} '{songName}' ");

            if (processed.Count == 0)
            {
                Console.WriteLine("\nWaiting for downloads to finish");
            }
        }

        private void DownloadSongFromYoutube(ChromeDriver driver, string songName)
        {
            if (FromYoutubeDownloads == null)
            {
                FromYoutubeDownloads = new List<string>();
            }

            FromYoutubeDownloads.Add(songName);

            var url = $"https://www.youtube.com.br/results?search_query={GetYoutubeQueryString(songName)}";
            driver.Navigate().GoToUrl(url);

            driver.WaitForPageToLoad();
            driver.WaitForElementToBeDisplayed(By.CssSelector("a#video-title.yt-simple-endpoint.style-scope.ytd-video-renderer"));

            var contents = driver.FindElements(By.CssSelector("a#video-title.yt-simple-endpoint.style-scope.ytd-video-renderer"));

            var link = contents.FirstOrDefault()?.GetAttribute("href");
            var videoId = link?.Split("v=")?.LastOrDefault()?.Trim();

            driver.Navigate().GoToUrl($"https://www.yt-download.org/pt/@api/button/mp3/{videoId}");
            driver.WaitForPageToLoad();

            Count++;
            Console.WriteLine($"Downloading song {Count}/{Total} '{songName}' from Youtube. This may delay the processing\n");

            driver.WaitForElementToBeDisplayed(By.ClassName("download-result"), TimeSpan.FromMinutes(5));
            var elDownload = driver.FindElementsByClassName("link").FirstOrDefault();

            elDownload?.Click();

            FromYoutubeDownloads.Remove(songName);
        }   

        private string GetYoutubeQueryString(string songName)
        {
            // .Net Core default URL enconding is already RFC2396
            return System.Web.HttpUtility.UrlEncode(songName);
        }

        public void Dispose()
        {
            Console.WriteLine("\nDisposing all the used resources\n");

            MainDriver?.Quit();
            ParallelDrivers?.ForEach(x => x.Quit());

            MainDriver = null;
            ParallelDrivers = null;

            var processes = Process.GetProcessesByName("chromedriver").ToList();
            processes.ForEach(x => x.Kill());
        }

        private IList<string> GetSongsNamesFromPlaylistLink(string playlistLink)
        {
            Console.WriteLine("Navigating to playlist link\n");
            MainDriver.Navigate().GoToUrl(playlistLink);

            Console.WriteLine("Getting songs names");
            var tracklistRows = MainDriver.FindElements(By.ClassName("tracklist-row")).ToList();

            var songsNames = tracklistRows.Select(row =>
            {
                var songName = row.FindElement(By.CssSelector("div.tracklist-name.ellipsis-one-line")).Text;
                var artists = row.FindElement(By.CssSelector("span.TrackListRow__artists.ellipsis-one-line")).Text;

                return $"{artists.Split(",").FirstOrDefault()?.Trim()} - {songName.Trim()}";
            }).ToList();

            Console.WriteLine($"Got {songsNames.Count} songs names");

            return songsNames;
        }

        private void RefineSongsNames(IList<string> songsNames)
        {
            for (var i = 0; i < songsNames.Count; i++)
            {
                var splitted = songsNames[i].Split("-");
                if (splitted.Length > 2)
                {
                    songsNames[i] = $"{splitted[0]} - {splitted[1]}";
                }
            }
        }   

        private string DownloadDirectory { get; set; }

        private ChromeDriver MainDriver { get; set; }

        private List<string> FromYoutubeDownloads { get; set; }

        private List<ChromeDriver> ParallelDrivers { get; set; } 
    }
}
