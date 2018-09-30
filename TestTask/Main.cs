namespace TestTask
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;

    public class Main
    {
        private static Queue<Dictionary<string, int>> oDict;

        private static Queue<string> files;

        private static object lockAdd;

        private static object lockRead;

        private static Regex regex;

        private static AutoResetEvent evt;

        public string Start()
        {
            lockRead = new object();
            lockAdd = new object();
            evt = new AutoResetEvent(false);

            regex = new Regex(@"\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var procCount = Environment.ProcessorCount;

            oDict = new Queue<Dictionary<string, int>>();

            var threads = new List<Thread>(procCount);

            files = new Queue<string>(Directory.GetFiles($"{Directory.GetCurrentDirectory()}", "*.txt", SearchOption.AllDirectories));

            for (var i = 0; i < procCount; i++)
            {
                var thread = new Thread(Parse) { IsBackground = true };
                thread.Start();

                threads.Add(thread);
            }

            evt.WaitOne();
            var result = oDict.Dequeue();

            while (threads.Any(x => x.IsAlive))
            {
                evt.WaitOne();

                while (oDict.Count > 0)
                {
                    foreach (var dict in oDict.Dequeue())
                    {
                        result.TryGetValue(dict.Key, out int value);
                        if (value != 0)
                        {
                            result[dict.Key] = value + dict.Value;
                        }
                        else
                        {
                            result.Add(dict.Key, dict.Value);
                        }
                    }
                }
            }

            return string.Join(", ", result.OrderByDescending(x => x.Value).Select(x => x.Key).Take(10));
        }

        private static void Parse()
        {
            try
            {
                while (files.Count > 0)
                {
                    var file = "";
                    lock (lockRead)
                    {
                        if (files.Any())
                        {
                            file = files.Dequeue();
                        }
                        else
                        {
                            return;
                        }
                    }

                    var iDict = new Dictionary<string, int>();

                    using (var reader = new StreamReader(file))
                    {
                        while (!reader.EndOfStream)
                        {
                            var mathces = regex.Matches(reader.ReadLine());

                            for (int i = 0; i < mathces.Count; i++)
                            {
                                var match = mathces[i];

                                iDict.TryGetValue(match.Value, out int value);
                                if (value != 0)
                                {
                                    iDict[match.Value] = value + 1;
                                }
                                else
                                {
                                    iDict.Add(match.Value, 1);
                                }
                            }
                        }

                        lock (lockAdd)
                        {
                            oDict.Enqueue(iDict);
                            evt.Set();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e.ToString());
            }
        }


        private static void LogError(string message)
        {
            using (var streamWriter = new StreamWriter($"{Directory.GetCurrentDirectory()}\\logs\\error.txt", true))
            {
                streamWriter.WriteLine($"{DateTime.Now}");
                streamWriter.WriteLine(message);
            }
        }
    }
}