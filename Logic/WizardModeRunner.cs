using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        private static readonly char[] MenuOptions = { '1', '2' };
    }
}
