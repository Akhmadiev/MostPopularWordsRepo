namespace TestTask
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;

    public class Main
    {
        private static ConcurrentDictionary<string, int> result;

        private static ConcurrentBag<string> input;
        private static ConcurrentBag<string> output;

        private static AutoResetEvent evtParse;
        private static AutoResetEvent evtAggreate;

        private static Regex regex;

        private static string logFile;

        private static object objLock;

        /// <summary>
        /// Get 10 most popular words
        /// </summary>
        public string GetMostPopularWords()
        {
            result = new ConcurrentDictionary<string, int>();

            evtParse = new AutoResetEvent(false);
            evtAggreate = new AutoResetEvent(false);

            objLock = new object();

            regex = new Regex(@"\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var currentDirectory = Directory.GetCurrentDirectory();

            var files = Directory.GetFiles($"{currentDirectory}\\files", "*.txt");
            logFile = $"{currentDirectory}\\logs\\error.txt";

            for (var i = 0; i < Environment.ProcessorCount - 1; i++)
            {
                new Thread(Parse) { IsBackground = true, Name = i.ToString() }.Start();
            }

            new Thread(Aggregate) { IsBackground = true }.Start();

            foreach (var file in files)
            {
                var count = File.ReadLines(file).Count();

                input = new ConcurrentBag<string>(new List<string>(count));
                output = new ConcurrentBag<string>(new List<string>(count));

                using (var reader = new StreamReader(file))
                {
                    while (!reader.EndOfStream)
                    {
                        input.Add(reader.ReadLine());
                        evtParse.Set();
                    }
                }

                if (!input.IsEmpty)
                {
                    AddRows();
                }

                if (!output.IsEmpty)
                {
                    AddResult();
                }
            }

            return string.Join(", ", result.OrderByDescending(x => x.Value).Select(x => x.Key).Take(10));
        }

        /// <summary>
        /// Parse from line to row
        /// </summary>
        private static void Parse()
        {
            try
            {
                while (true)
                {
                   evtParse.WaitOne();

                    AddRows();
                }
            }
            catch(Exception e)
            {
                lock(objLock)
                {
                    LogError(e.ToString());
                }
            }
        }

        /// <summary>
        ///  Add value to output bag (ConcurrentBag<string>)
        /// </summary>
        private static void AddRows()
        {
            while (!input.IsEmpty)
            {
                input.TryTake(out string text);

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var mathces = regex.Matches(text);
                foreach (Match match in mathces)
                {
                    output.Add(match.Value);
                    evtAggreate.Set();
                }
            }
        }

        /// <summary>
        /// Aggregate data from output bag
        /// </summary>
        private static void Aggregate()
        {
            try
            {
                while (true)
                {
                    evtAggreate.WaitOne();

                    AddResult();
                }
            }
            catch(Exception e)
            {
                lock (objLock)
                {
                    LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Add values to result dictionary
        /// </summary>
        private static void AddResult()
        {
            while (!output.IsEmpty)
            {
                output.TryTake(out string value);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.AddOrUpdate(value, 1, (x, oldValue) => oldValue + 1);
                }
            }
        }

        /// <summary>
        /// Write error in log file
        /// </summary>
        private static void LogError(string message)
        {
            using (var streamWriter = new StreamWriter(logFile, true))
            {
                streamWriter.WriteLine($"{DateTime.Now}");
                streamWriter.WriteLine(message);
            }
        }
    }
}