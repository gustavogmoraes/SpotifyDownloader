using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;

namespace SpotifyDownloader.Infrastructure
{
    public static class Extensions
    {
        public static List<List<T>> SplitList<T>(this IList<T> majorList, int size)
        {
            var enumeratedMajorList = majorList.ToList();
            var listOfLists = new List<List<T>>();

            for (int i = 0; i < enumeratedMajorList.Count; i += size)
            {
                listOfLists.Add(enumeratedMajorList.GetRange(i, Math.Min(size, majorList.Count - i)));
            }

            return listOfLists;
        }

        public static ConcurrentBag<T> ToConcurrentBag<T>(this IList<T> list)
        {
            var concurrentBag = new ConcurrentBag<T>();
            list.ToList().ForEach(x => concurrentBag.Add(x));

            return concurrentBag;
        }

        public static IEnumerable<Process> GetChildProcesses(this Process process)
        {
            List<Process> children = new List<Process>();
            var mos = new ManagementObjectSearcher(string.Format("Select * From Win32_Process Where ParentProcessID={0}", process.Id));

            foreach (ManagementObject mo in mos.Get())
            {
                children.Add(Process.GetProcessById(Convert.ToInt32(mo["ProcessID"])));
            }

            return children;
        }
    }
}
