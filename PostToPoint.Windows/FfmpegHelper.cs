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
            // Find ffmpeg executable path.  If it's in current directory: take it.  Otherwise, search in PATH
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
            }
            else
            {
                throw new ApplicationException("Error starting ffmpeg process.");
            }
        }

        private static string FindFfmpegPath()
        {
            string ffmpegPath = "";

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe");
            }

            if (!File.Exists(ffmpegPath))
            {
                var enviromentPath = System.Environment.GetEnvironmentVariable("PATH");

                var paths = enviromentPath.Split(';');
                ffmpegPath = paths.Select(x => Path.Combine(x, "ffmpeg.exe"))
                                   .Where(x => File.Exists(x))
                                   .FirstOrDefault();
            }

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new FileNotFoundException("ffmpeg.exe not found in PATH or in current directory");
            }

            return ffmpegPath;
        }
    }
}
