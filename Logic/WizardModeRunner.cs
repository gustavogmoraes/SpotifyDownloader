using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SpotifyDownloader.Infrastructure;

namespace SpotifyDownloader.Logic
{
    public static class WizardModeRunner
    {
        public static void RunInWizardMode()
        {
            Console.WriteLine($"=========== Welcome to Song Downloader ===========\n");
            while (true)
            {
                Console.WriteLine(StartMenu);
                var input = Console.ReadKey();
                if (!MenuOptions.Contains(input.KeyChar))
                {
                    Console.WriteLine("Please type one of the options listed above\n");
                    Console.ReadKey();
                    Console.Clear();
                    continue;
                }

                Console.Clear();
                switch (input.KeyChar)
                {
                    case '1': // Download songs one by one passing the name or search string
                        while (true)
                        {
                            Console.WriteLine(OneByOneMenu);

                            var menu1Input = Console.ReadLine();
                            if (string.IsNullOrEmpty(menu1Input))
                            {
                                Console.WriteLine("Please type some search string\n");
                                Console.ReadKey();
                                Console.Clear();
                                continue;
                            }

                            Console.WriteLine($"You typed '{menu1Input.Trim()}'\n" +
                                              "Searching for it on database");
                        }

                    case '2': // Download songs passing Spotify public playlist link
                        Console.WriteLine(SpotifyPlaylistMenu);

                        var menu2Input = Console.ReadLine();
                        if (string.IsNullOrEmpty(menu2Input))
                        {
                            Console.WriteLine("Please type some search string\n");
                            Console.ReadKey();
                            Console.Clear();
                            continue;
                        }

                        Console.WriteLine($"You typed '{menu2Input.Trim()}'\n" +
                                          "Do you want to set output directory? If yes insert it, if don't just press enter");
                        var playlistLink = menu2Input.Trim();

                        var defaultOutput = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Program.GetBaseFolderName(Session.Id));
                        menu2Input = Console.ReadLine();
                        var output = string.IsNullOrEmpty(menu2Input) 
                            ? defaultOutput 
                            : Path.Combine(menu2Input, Program.GetBaseFolderName(Session.Id));

                        Console.WriteLine("Do you want to run in parallel?(S/N)\n");

                        menu2Input = Console.ReadLine();
                        var runInParallel = !string.IsNullOrEmpty(menu2Input) && menu2Input.Trim().ToLowerInvariant() != "n";

                        Console.WriteLine("Starting");
                        Thread.Sleep(TimeSpan.FromSeconds(2));
                        Console.Clear();
                        using (var musicDownloader = new MusicDownloader(output))
                        {
                            musicDownloader.DownloadSpotifyPlaylist(playlistLink, runInParallel);
                        }

                        break;
                }
            }
        }

        private const string StartMenu =
            "Please select operation mode:\n" +
            "1. Download songs one by one passing the name or search string\n" +
            "2. Download songs passing Spotify public playlist link\n";

        private const string OneByOneMenu =
            "Please inform the name of the song or search string\n" +
            "Passing in the following format will get better matches at the search and probably bring more satisfying results\n" +
            "Format: <Artist name> - <Song name> <Remix or version tag>\n" +
            "Example: Post Malone - Congratulation\n";

        private const string SpotifyPlaylistMenu =
            "Please inform the playlist link\n";

        private static readonly char[] MenuOptions = { '1', '2' };
    }
}
