using Olive;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System
{
    static class Extensions
    {
        internal static Task<string> Run(this string command, Action<ProcessStartInfo> config = null, int timeout = 2000)
        {
            Console.WriteLine("Running : " + command);

            return Task.Factory.StartNew(() =>
            {
                using (var cmd = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        UseShellExecute = false,
                        Arguments = "/c " + command,
                        Verb = "runas",
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    },
                    EnableRaisingEvents = true
                })
                {
                    var output = new StringBuilder();
                    var error = new StringBuilder();


                    using var outputWaitHandle = new AutoResetEvent(false);
                    using var errorWaitHandle = new AutoResetEvent(false);

                    cmd.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                            outputWaitHandle.Set();
                        else
                            output.Append(e.Data);
                    };
                    cmd.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                            errorWaitHandle.Set();
                        else
                            error.Append(e.Data);
                    };

                    config?.Invoke(cmd.StartInfo);

                    cmd.Start();

                    cmd.BeginOutputReadLine();
                    cmd.BeginErrorReadLine();

                    while (!cmd.WaitForExit(timeout) && !outputWaitHandle.WaitOne(timeout) && !errorWaitHandle.WaitOne(timeout))
                        Thread.Sleep(100);

                    if (error.Length > 0) throw new Exception(error.ToString());

                    return output.ToString();
                }
            });
        }

        private static void Cmd_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        internal static DirectoryInfo SolutionDirectory(this DirectoryInfo child)
        {
            var folders = new List<string>();
            for (var result = child; result != null; result = result.Parent)
            {
                folders.Add(result.FullName);
                if (result.GetFiles("*.sln").Any())
                    return result;
            }

            throw new Exception($"Could not find the solution folder. Looked in [{folders.ToString(",")}]");
        }
    }
}
