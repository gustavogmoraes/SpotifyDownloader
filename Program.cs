using AutomationBase;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SpotifyDownloader.Infrastructure;
using SpotifyDownloader.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SpotifyDownloader
{
    public class Program
    {
        private static string DownloadDirectory { get; set; }

        private static ChromeDriver Driver { get; set; }

        private static List<ChromeDriver> ParallelDrivers { get; set; }

        private static Mode? ParseMode(string inputMode)
        {
            switch (inputMode.ToLowerInvariant())
            {
                case "onebyone":
                    return Mode.OneByOne;
                case "spotifyplaylist":
                    return Mode.SpotifyPlaylist;
                default:
                    return null;
            }
        }

        /// <summary>
        /// --Mode=OneByOne or --Mode=SpotifyPlaylist
        /// --PlaylistLink=Link
        /// --Output:Folder
        /// --RunInParallel
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static dynamic GetParams(string[] args)
        {
            if (args.Length == 0)
            {
                return null;
            }

            var inputMode = args.FirstOrDefault(x => x.Contains("Mode"))?.Split("=").LastOrDefault()?.Trim();

            var mode = ParseMode(inputMode);
            if (!mode.HasValue)
            {
                throw new Exception($"No mode could be parsed for your input {inputMode}");
            }

            var defaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            var inputOutput = args.FirstOrDefault(x => x.Contains("Output"))?.Split("=").LastOrDefault()?.Trim();

            var outputPathInformed = string.IsNullOrEmpty(inputOutput) || Directory.Exists(inputOutput);
            if (!outputPathInformed)
            {
                Console.WriteLine($"Output parameter was not informed, do not exist or isn't acessible, files gonna be downloaded at '{defaultOutputPath}'");
            }

            var inputPlaylistLink = args.FirstOrDefault(x => x.Contains("PlaylistLink"))?.Split("=").LastOrDefault()?.Trim();
            var playlistLink = mode.GetValueOrDefault() == Mode.SpotifyPlaylist
                ? inputPlaylistLink
                : null;

            // Validating playlist link
            if (mode == Mode.SpotifyPlaylist)
            {
                using var httpClient = new HttpClient();
                try
                {
                    _ = httpClient.GetAsync(playlistLink).Result;
                }
                catch
                {
                    throw new Exception("The playlist link informed couldn't be acessed");
                }
            }

            var runInParallel = !string.IsNullOrEmpty(args.FirstOrDefault(x => x.Contains("RunInParallel"))?.Split("=").LastOrDefault()?.Trim());

            return new
            {
                Mode = mode,
                PlaylistLink = playlistLink,
                OutputDirectory = string.IsNullOrEmpty(inputOutput),
                RunInParallel = runInParallel
            };
        }

        private static string GetBaseFolderName(string sessionId)
        {
            return $"SongDownloader_Session_{sessionId}";
        }

        public static void Main(string[] args)
        {
            Session.Id = DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss");
            Console.WriteLine($"Started at: {DateTime.Now:HH:mm:ss}\n" +
                              $"Session Id: {Session.Id}\n");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var parameters = GetParams(args);
            if (parameters == null)
            {
                WizardModeRunner.RunInWizardMode();
                return;
            }


            if (parameters.PlaylistLink != null)
            {

            }

            Console.WriteLine($"Got playlist link '{parameters.PlaylistLink}'\n");

            var folderName = GetBaseFolderName(Session.Id);
            var defaultDownloadLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName);
            

            Console.WriteLine(parameters.OutputDirectory != null 
                ? $"Got output directory '{parameters.OutputDirectory}'\n"
                : $"No output directory passed via -o param, files gonna be downloaded at '{DownloadDirectory}'\n");

            var indexOfP = args.ToList().IndexOf("-p");
            var runInParallel = indexOfP != -1;
            Console.WriteLine($"Application {(runInParallel ? "will" : "won't")} run in parallel {(runInParallel ? $"using {Environment.ProcessorCount} logical processors" : string.Empty)}\n");

            DownloadDirectory = parameters.OutputDirectory ?? defaultDownloadLocation;

            ChromeDriverHandler.CheckUpdateChromeDriver();

            IOHandler.CreateDirectory(DownloadDirectory);
            IOHandler.SetDirectoryPermission(DownloadDirectory);

            Console.WriteLine("Instantiating driver\n");
            Driver = ChromeDriverHandler.GetDriver(DownloadDirectory);

            var songsNames = GetSongsNamesFromPlaylistLink(parameters.PlaylistLink);
            RefineSongsNames(songsNames);

            DownloadSongs(songsNames, runInParallel);
            IOHandler.RenameFilesRemovingString(DownloadDirectory, "my-free-mp3s.com");

            // Disposing all the used resources
            Driver.Dispose();
            ParallelDrivers?.ForEach(x => x.Dispose());
            Process.GetProcessesByName("chromedriver.exe").ToList().ForEach(x => x.Kill());

            stopWatch.Stop();
            Console.WriteLine($"Total time: {stopWatch.Elapsed}");
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
                    var driver = ChromeDriverHandler.GetDriver(DownloadDirectory, true, false);
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

            driver.WaitForElementToBeDisplayed(By.Id("result"));
            var elResult = driver.FindElement(By.Id("result"));

            driver.WaitForElementToBeDisplayed(By.CssSelector("#result > div.list-group"));
            var elResultList = elResult.FindElement(By.CssSelector("#result > div.list-group"));

            driver.WaitForElementToBeDisplayed(By.ClassName("list-group-item"));
            var elListItems = elResultList.FindElements(By.ClassName("list-group-item"));

            if (elListItems.Count == 1 && (elListItems.FirstOrDefault()?.Text.ToLowerInvariant().Contains("your request was not found")).GetValueOrDefault())
            {
                Console.WriteLine($"Song '{songName}' not found to download\n" +
                                  "Will try downloading from youtube\n");

                DownloadSongFromYoutube(driver, ref count, total, songName);

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

            count++;
            Console.WriteLine($"Downloading song {count}/{total} '{songName}' ");
        }

        private static void DownloadSongFromYoutube(ChromeDriver driver, ref int count, int total, string songName)
        {
            var url = $"https://www.youtube.com.br/results?search_query={GetYoutubeQueryString(songName)}";
            driver.Navigate().GoToUrl(url);

            driver.WaitForPageToLoad();
            driver.WaitForElementToBeDisplayed(By.CssSelector("a#video-title.yt-simple-endpoint.style-scope.ytd-video-renderer"));

            var contents = driver.FindElements(By.CssSelector("a#video-title.yt-simple-endpoint.style-scope.ytd-video-renderer"));

            var link = contents.FirstOrDefault()?.GetAttribute("href");
            var videoId = link?.Split("v=")?.LastOrDefault()?.Trim();

            driver.Navigate().GoToUrl($"https://www.yt-download.org/pt/@api/button/mp3/{videoId}");
            driver.WaitForPageToLoad();

            count++;
            Console.WriteLine($"Downloading song {count}/{total} '{songName}' from Youtube. This may delay the processing\n");

            driver.WaitForElementToBeDisplayed(By.ClassName("download-result"), TimeSpan.FromMinutes(5));
            var elDownload = driver.FindElementsByClassName("link").FirstOrDefault();

            elDownload?.Click();
        }

        private static string GetYoutubeQueryString(string songName)
        {
            // .Net Core default URL enconding is already RFC2396
            return System.Web.HttpUtility.UrlEncode(songName);
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

                return $"{artists.Split(",").FirstOrDefault()?.Trim()} - {songName.Trim()}";
            }).ToList();

            Console.WriteLine($"Got {songsNames.Count} songs names\n");

            return songsNames;
        }
    }
}
