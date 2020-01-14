using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutomationBase;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;

namespace SpotifyDownloader
{
    public class Program
    {
        private static string DownloadDirectory { get; set; }

        private static ChromeDriver Driver { get; set; }

        private static List<ChromeDriver> ParallelDrivers { get; set; }

        public static void Main(string[] args)
        {
            var sessionId = DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss");  
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            Console.WriteLine($"Started at: {DateTime.Now:HH:mm:ss}\n" +
                              $"Session Id: {sessionId}\n");

            var indexOfL = args.ToList().IndexOf("-l");
            var playlistLink = args[indexOfL + 1];
            Console.WriteLine($"Got playlist link '{playlistLink}'\n");

            var folderName = $"SpotifyDownloader_{sessionId}";
            var defaultDownloadLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName);
            var indexOfO = args.ToList().IndexOf("-o");

            var outputDirectory = indexOfO != -1 ? Path.Combine(args[indexOfO + 1], folderName) : null;
            Console.WriteLine(outputDirectory != null 
                ? $"Got output directory '{outputDirectory}'\n"
                : $"No output directory passed via -o param, files will be downloaded at '{DownloadDirectory}'\n");

            var indexOfP = args.ToList().IndexOf("-p");
            var runInParallel = indexOfP != -1;
            Console.WriteLine($"Application {(runInParallel ? "will" : "won't")} run in parallel {(runInParallel ? $"using {Environment.ProcessorCount} logical processors" : string.Empty)}\n");

            DownloadDirectory = outputDirectory ?? defaultDownloadLocation;

            CheckUpdateChromeDriver();

            Directory.CreateDirectory(DownloadDirectory);

            Console.WriteLine("Instantiating driver\n");
            Driver = GetDriver(DownloadDirectory);

            var songsNames = GetSongsNamesFromPlaylistLink(playlistLink);
            RefineSongsNames(songsNames);

            DownloadSongs(songsNames, runInParallel);
            RenameFiles();

            // Disposing all the used resources
            Driver.Dispose();
            ParallelDrivers?.ForEach(x => x.Dispose());
            Process.GetProcessesByName("chromedriver.exe").ToList().ForEach(x => x.Kill());

            stopWatch.Stop();
            Console.WriteLine($"Total time: {stopWatch.Elapsed}");
        }

        private static void RenameFiles()
        {
            var files = Directory.GetFiles(DownloadDirectory);
            foreach(var fileName in files)
            {
                File.Move(fileName, fileName.Replace(" my-free-mp3s.com ", string.Empty));

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            };

        }

        private static void RefineSongsNames(IList<string> songsNames)
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

        private static List<List<T>> SplitList<T>(List<T> majorList, int size)
        {
            var list = new List<List<T>>();
            for (int i = 0; i < majorList.Count; i += size)
            {
                list.Add(majorList.GetRange(i, Math.Min(size, majorList.Count - i)));
            }

            return list;
        }

        private static void DownloadSongs(IList<string> songsNames, bool runInParallel = false)
        {
            Console.WriteLine("Starting songs downloads\n");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var count = 0;
            var total = songsNames.Count;

            if (runInParallel)
            {
                // Max number of processing lists should be the same as the logical processors count
                var listSize = Convert.ToInt32(Math.Round((songsNames.Count / (decimal)Environment.ProcessorCount), MidpointRounding.ToPositiveInfinity));
                var processingList = SplitList(songsNames.ToList(), listSize);
                
                Parallel.ForEach(processingList, songNameList =>
                {
                    var driver = GetDriver(DownloadDirectory, false);
                    if (ParallelDrivers == null)
                    {
                        ParallelDrivers = new List<ChromeDriver>();
                    }

                    ParallelDrivers.Add(driver);

                    songNameList.ForEach(songName => DownloadSong(ref count, total, driver, songName));
                });
            }
            else
            {
                songsNames.ToList().ForEach(songName => DownloadSong(ref count, total, Driver, songName));
            }

            Console.WriteLine("\nWaiting for downloads to finish");
            while (Directory.GetFiles(DownloadDirectory).Any(x => x.EndsWith("crdownload"))) { }

            stopWatch.Stop();
            Console.WriteLine("\nDone downloading\n" +
                              $"Download time: {stopWatch.Elapsed}\n");
        }

        private static void DownloadSong(ref int count, int total, ChromeDriver driver, string songName)
        {
            driver.Navigate().GoToUrl("https://myfreemp3c.com/");
            driver.WaitForPageToLoad();

            driver.WaitForElementToBeDisplayed(By.Id("query"));
            var elQuery = driver.FindElement(By.Id("query"));
            elQuery.SendKeys(songName);

            var elSubmit = driver.FindElement(By.CssSelector("body > div.wrapper > div.container > div > span > button"));
            elSubmit.Click();

            driver.WaitForPageToLoad();

            //ChromeDriverHelper.WaitSpecificTime(TimeSpan.FromSeconds(4));

            driver.WaitForElementToBeDisplayed(By.Id("result"));
            var elResult = driver.FindElement(By.Id("result"));

            driver.WaitForElementToBeDisplayed(By.CssSelector("#result > div.list-group"));
            var elResultList = elResult.FindElement(By.CssSelector("#result > div.list-group"));

            driver.WaitForElementToBeDisplayed(By.ClassName("list-group-item"));
            var elListItems = elResultList.FindElements(By.ClassName("list-group-item"));

            if (elListItems.Count == 1 && (elListItems.FirstOrDefault()?.Text.ToLowerInvariant().Contains("your request was not found")).GetValueOrDefault())
            {
                Console.WriteLine($"Song '{songName}' not found to download");

                return;
            }

            var items = new Dictionary<string, string>();
            elListItems.Take(10).ToList().ForEach(item =>
            {
                var key = string.Join(" - ", item.FindElements(By.CssSelector("#navi")).Select(x => x.Text));
                if (items.ContainsKey(key)) return;

                var value = driver.FindElement(By.CssSelector("#result > div.list-group > li:nth-child(1) > a.name"))
                    .GetAttribute("href");
                items.Add(key, value);
            });

            driver.Navigate().GoToUrl(items.FirstOrDefault().Value);

            count++;
            Console.WriteLine($"Downloading song {count}/{total} '{songName}' ");
        }

        private static void DownloadSongFromYoutube(string songName)
        {
            //https://www.yt-download.org/pt/@api/button/mp3/64FgiowQZh8
        }

        private static string GetYoutubeQueryString(string songName)
        {
            // .Net Core default URL enconding is already RFC2396
            return System.Web.HttpUtility.UrlEncode(songName);
        }

        private static string GetSongsYoutubeLinks(string songName)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var url = $"https://www.youtube.com.br/results?search_query={GetYoutubeQueryString(songName)}";
            Driver.Navigate().GoToUrl(url);

            Driver.WaitForPageToLoad();
            Driver.WaitForElementToBeDisplayed(By.CssSelector("a#video-title.yt-simple-endpoint.style-scope.ytd-video-renderer"));

            var contents = Driver.FindElements(By.CssSelector("a#video-title.yt-simple-endpoint.style-scope.ytd-video-renderer"));

            var link = contents.FirstOrDefault()?.GetAttribute("href");

            stopwatch.Stop();
            Console.WriteLine($"Got link on Youtube\n");

            return link;
        }

        private static IList<string> GetSongsNamesFromPlaylistLink(string playlistLink)
        {
            Console.WriteLine("Navigating to playlist link\n");
            Driver.Navigate().GoToUrl(playlistLink);

            Console.WriteLine("Getting songs names");
            var tracklistRows = Driver.FindElements(By.ClassName("tracklist-row")).ToList();

            var songsNames = tracklistRows.Select(row =>
            {
                var songName = row.FindElement(By.CssSelector("div.tracklist-name.ellipsis-one-line")).Text;
                var artists = row.FindElement(By.CssSelector("span.TrackListRow__artists.ellipsis-one-line")).Text;

                return $"{artists.Split(",").FirstOrDefault()?.Trim()} - {songName}";
            }).ToList();

            Console.WriteLine($"Got {songsNames.Count} songs names\n");

            return songsNames;
        }

        private static ChromeDriver GetDriver(string downloadDirectory, bool killOtherProcesses = true)
        {
            return ChromeDriverHelper.GetDriverBuilder()
                .AllowRunningInsecureContent()
                //.DisablePopupBlocking()
                .Headless()
                .SetDownloadPath(downloadDirectory)
                .Build(killOtherProcesses);
        }

        private static void CheckUpdateChromeDriver()
        {
            Console.WriteLine($"Checking ChromeDriver files");
            ChromeDriverHelper.CheckUpdateChromeDriver();
            Console.WriteLine("ChromeDriver working\n");
        }
    }
}
