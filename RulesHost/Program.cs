/* ----- ----- ----- ----- */
// Program.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Chess_v3.Game.Core.RulesHost
{
    /// <summary>
    /// The rules host's entry point: reads one JSON request per line from standard input, answers
    /// each on the thread pool (requests are independent) and writes one response per line to
    /// standard output, each written whole. The rules' log goes to standard error. Ends when
    /// standard input closes, after the last answers are written.
    /// </summary>
    public static class Program
    {
        public static async Task Main()
        {
            CoreLog.Sink = (message, level) => Console.Error.WriteLine($"[{level}] {message}");

            var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false, NewLine = "\n" };
            var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            var writeLock = new object();
            var pending = new List<Task>();

            string line;
            while ((line = await input.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                string request = line;
                pending.Add(Task.Run(() =>
                {
                    string response = RequestHandler.Handle(request);
                    lock (writeLock)
                    {
                        output.WriteLine(response);
                        output.Flush();
                    }
                }));
                pending.RemoveAll(t => t.IsCompleted);
            }

            await Task.WhenAll(pending);
        }
    }
}
