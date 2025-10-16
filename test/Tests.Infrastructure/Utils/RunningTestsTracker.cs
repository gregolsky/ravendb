using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Tests.Infrastructure.Utils
{
    /// <summary>
    /// Tracks tests currently running in this process. Used for diagnostics on failures.
    /// </summary>
    internal static class RunningTestsTracker
    {
        private static readonly ConcurrentDictionary<string, byte> Running = new();

        public static void Add(string testName)
        {
            if (string.IsNullOrWhiteSpace(testName))
                return;
            Running[testName] = 1;
        }

        public static void Remove(string testName)
        {
            if (string.IsNullOrWhiteSpace(testName))
                return;
            Running.TryRemove(testName, out _);
        }

        public static string[] GetRunningExcept(string currentTest, int max = 50)
        {
            var list = Running.Keys
                .Where(x => string.Equals(x, currentTest, StringComparison.OrdinalIgnoreCase) == false)
                .OrderBy(x => x)
                .ToList();

            if (list.Count <= max)
                return list.ToArray();

            return list.Take(max).Concat(new[] {$"... and {list.Count - max} more"}).ToArray();
        }

        public static int GetRunningCount()
        {
            return Running.Count;
        }
    }
}
