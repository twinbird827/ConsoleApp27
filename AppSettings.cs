using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp27
{
    public static class AppSettings
    {
        public static int Option => int.Parse(ConfigurationManager.AppSettings["option"]);

        public static int Lock => int.Parse(ConfigurationManager.AppSettings["lock"]);

        public static string Shukusen => ConfigurationManager.AppSettings["shukusen"];

        public static IEnumerable<string> IgnoreDirectories => ToArray(ConfigurationManager.AppSettings["ignore_directories"]);

        public static IEnumerable<string> IgnoreFiles => ToArray(ConfigurationManager.AppSettings["ignore_files"]);

        private static IEnumerable<string> ToArray(string value)
        {
            return value.Split(':');
        }

        public static SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, Lock);

    }
}
