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
        /// --SongName=Name
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

            args = string.Join(string.Empty, args).Split("--");

            var inputMode = args.FirstOrDefault(x => x.Contains("Mode"))?.Split("=").LastOrDefault()?.Trim();

            var mode = ParseMode(inputMode);
            if (!mode.HasValue)
            {
                throw new Exception($"No mode could be parsed for your input {inputMode}");
            }

            var defaultOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), GetBaseFolderName(Session.Id));
            var inputOutput = args.FirstOrDefault(x => x.Contains("Output"))?.Split("=").LastOrDefault()?.Trim();

            var outputPathInformed = !(string.IsNullOrEmpty(inputOutput) || Directory.Exists(inputOutput));
            if (!outputPathInformed)
            {
                Console.WriteLine($"Output parameter was not informed, do not exist or isn't acessible, files gonna be downloaded at '{defaultOutputPath}'");
            }

            var inputPlaylistLink = args.FirstOrDefault(x => x.Contains("PlaylistLink"))?.Replace("PlaylistLink=", string.Empty).Trim();
            var playlistLink = mode.GetValueOrDefault() == Mode.SpotifyPlaylist
                ? inputPlaylistLink
                : null;

            string songName = null;
            switch (mode)
            {
                case Mode.OneByOne:
                {
                    songName = args.FirstOrDefault(x => x.Contains("SongName"))?.Split("=").LastOrDefault()?.Trim();
                    break;
                }

                // Validating playlist link
                case Mode.SpotifyPlaylist:
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

                    break;
                }
            }

            var runInParallel = !string.IsNullOrEmpty(args.FirstOrDefault(x => x.Contains("RunInParallel"))?.Split("=").LastOrDefault()?.Trim());

            return new
            {
                Mode = mode,
                SongName = songName,
                PlaylistLink = playlistLink,
                OutputDirectory = outputPathInformed ? Path.Combine(inputOutput, GetBaseFolderName(Session.Id)) : defaultOutputPath,
                RunInParallel = runInParallel
            };
        }

        public static string GetBaseFolderName(string sessionId)
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

            switch ((Mode) parameters.Mode)
            {
                case Mode.OneByOne:
                    var songName = (string)parameters.SongName;
                    break;
                case Mode.SpotifyPlaylist:
                    using (var musicDownloader = new MusicDownloader(parameters.OutputDirectory))
                    {
                        musicDownloader.DownloadSpotifyPlaylist(parameters.PlaylistLink, parameters.RunInParallel);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            stopWatch.Stop();
            Console.WriteLine($"Total time: {stopWatch.Elapsed}");
        }
    }
}
