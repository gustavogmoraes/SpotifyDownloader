using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using AutomationBase;

namespace SpotifyDownloader.Infrastructure
{
    public static class IOHandler
    {
        public static void CreateDirectory(string directory)
        {
            Directory.CreateDirectory(directory);
        }

        public static void RenameFilesRemovingString(string directoryPath, string stringToRemove)
        {
            var files = Directory.GetFiles(directoryPath);
            foreach (var fileName in files)
            {
                File.Move(fileName, fileName.Replace(stringToRemove, string.Empty).Trim());

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }

        public static void SetDirectoryPermission(string downloadDirectory)
        {
            var os = DevOpsHelper.GetOsPlatform();
            if (os == OSPlatform.Windows)
            {
                var directoryInfo = new DirectoryInfo(downloadDirectory);
                var security = directoryInfo.GetAccessControl();

                var domain = Environment.UserDomainName;
                var username = Environment.UserName;
                security.AddAccessRule(new FileSystemAccessRule($@"{domain}\{username}", FileSystemRights.FullControl, AccessControlType.Allow));

                directoryInfo.SetAccessControl(security);
            }
            else if (new[] { OSPlatform.OSX, OSPlatform.Linux }.Contains(os))
            {
                ShellHelper.Bash($"sudo chown {Environment.UserName} {downloadDirectory}");
            }
        }
    }
}
