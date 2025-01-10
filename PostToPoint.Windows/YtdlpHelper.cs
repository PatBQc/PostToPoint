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
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(path);

            if (string.IsNullOrWhiteSpace(uri))
            {
                throw new ArgumentException("URI cannot be empty", nameof(uri));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty", nameof(path));
            }

            // Find yt-dlp.exe executable path. If it's in current directory: take it. Otherwise, search in PATH
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

                if (process.ExitCode != 0)
                {
                    throw new ApplicationException($"yt-dlp process exited with code {process.ExitCode}");
                }
            }
            else
            {
                throw new ApplicationException("Error starting yt-dlp process.");
            }
        }

        private static string FindYtdlpPath()
        {
            // First try current directory
            var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), "yt-dlp.exe");
            if (File.Exists(currentDirPath))
            {
                return currentDirPath;
            }

            // Then try PATH
            var environmentPath = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(environmentPath))
            {
                throw new InvalidOperationException("PATH environment variable is not set");
            }

            var paths = environmentPath.Split(';');
            var ytdlpPath = paths.Select(x => Path.Combine(x, "yt-dlp.exe"))
                               .FirstOrDefault(File.Exists);

            if (string.IsNullOrEmpty(ytdlpPath))
            {
                throw new FileNotFoundException("yt-dlp.exe not found in PATH or in current directory");
            }

            return ytdlpPath;
        }
    }
}
