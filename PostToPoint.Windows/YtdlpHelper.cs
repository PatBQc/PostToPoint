using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    public class YtdlpHelper
    {
        public static void DownloadVideo(string uri, string path)
        {
            // Find yt-dlp.exe executable path.  If it's in current directory: take it.  Otherwise, search in PATH
            var ytdlp = FindYtdlpPath();

            // Execute yt-dlp.exe command to download the video
            var processStartInfo = new ProcessStartInfo
            {
                FileName = ytdlp,
                Arguments = $"{uri} -o {path}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Debug.WriteLine("## YT-DLP log: ");
            using var process = new Process();
            process.OutputDataReceived += (sender, args) => Debug.WriteLine("    " + args.Data);
            process.ErrorDataReceived += (sender, args) => Debug.WriteLine("    " + args.Data);
            process.StartInfo = processStartInfo;

            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();
            }
            else
            {
                throw new ApplicationException("Error starting yt-dlp process.");
            }
        }

        private static string FindYtdlpPath()
        {
            string ytdlpPath = "";

            if (string.IsNullOrEmpty(ytdlpPath))
            {
                ytdlpPath = Path.Combine(Directory.GetCurrentDirectory(), "yt-dlp.exe");
            }

            if (!File.Exists(ytdlpPath))
            {
                var enviromentPath = System.Environment.GetEnvironmentVariable("PATH");

                var paths = enviromentPath.Split(';');
                ytdlpPath = paths.Select(x => Path.Combine(x, "yt-dlp.exe"))
                                   .Where(x => File.Exists(x))
                                   .FirstOrDefault();
            }

            if (string.IsNullOrEmpty(ytdlpPath))
            {
                throw new FileNotFoundException("yt-dlp.exe not found in PATH or in current directory");
            }

            return ytdlpPath;
        }

    }
}
