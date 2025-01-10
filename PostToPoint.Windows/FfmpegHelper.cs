using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    internal class FfmpegHelper
    {
        public static void ShortenVideo(string longVideoFilename, string shortVideoFilename)
        {
            ArgumentNullException.ThrowIfNull(longVideoFilename);
            ArgumentNullException.ThrowIfNull(shortVideoFilename);

            if (string.IsNullOrWhiteSpace(longVideoFilename))
            {
                throw new ArgumentException("Input video filename cannot be empty", nameof(longVideoFilename));
            }

            if (string.IsNullOrWhiteSpace(shortVideoFilename))
            {
                throw new ArgumentException("Output video filename cannot be empty", nameof(shortVideoFilename));
            }

            if (!File.Exists(longVideoFilename))
            {
                throw new FileNotFoundException("Input video file not found", longVideoFilename);
            }

            // Find ffmpeg executable path. If it's in current directory: take it. Otherwise, search in PATH
            var ffmpegPath = FindFfmpegPath();

            // Execute ffmpeg command to split the audio from the video
            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{longVideoFilename}\" -t 59 -c:v libx264 -c:a aac \"{shortVideoFilename}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Debug.WriteLine("## FFMPEG log: ");
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
                    throw new ApplicationException($"FFmpeg process exited with code {process.ExitCode}");
                }
            }
            else
            {
                throw new ApplicationException("Error starting ffmpeg process.");
            }
        }

        private static string FindFfmpegPath()
        {
            // First try current directory
            var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe");
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
            var ffmpegPath = paths.Select(x => Path.Combine(x, "ffmpeg.exe"))
                               .FirstOrDefault(File.Exists);

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new FileNotFoundException("ffmpeg.exe not found in PATH or in current directory");
            }

            return ffmpegPath;
        }
    }
}
