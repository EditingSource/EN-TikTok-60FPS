//
// VideoInterpolator - C#/.NET Wrapper for RIFE (nihui/rife-ncnn-vulkan)
// 2025 Editing News, Evilfy.
// Distributed under the terms of the GNU General Public License v3.0.
// See the LICENSE file for details.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VideoProcessing
{
    /// <summary>
    /// Represents the result of an external process execution.
    /// </summary>
    public class ProcessExecutionResult
    {
        /// <summary>
        /// The exit code of the process. 0 typically indicates success.
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// The standard output (stdout) captured during execution.
        /// </summary>
        public string StandardOutput { get; }

        /// <summary>
        /// The standard error (stderr) captured during execution.
        /// </summary>
        public string StandardError { get; }

        /// <summary>
        /// Returns true if the process exited with code 0.
        /// </summary>
        public bool Success => ExitCode == 0;

        public ProcessExecutionResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Exit Code: {ExitCode}");
            if (!string.IsNullOrWhiteSpace(StandardOutput))
            {
                sb.AppendLine("Standard Output:");
                sb.AppendLine(StandardOutput);
            }
            if (!string.IsNullOrWhiteSpace(StandardError))
            {
                sb.AppendLine("Standard Error:");
                sb.AppendLine(StandardError);
            }
            return sb.ToString();
        }
    }

#if !NET5_0_OR_GREATER
    /// <summary>
    /// Provides an extension method for asynchronously waiting for a process to exit on older .NET Framework versions.
    /// </summary>
    internal static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            if (process.HasExited) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object?>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);

            if (cancellationToken != default)
            {
                cancellationToken.Register(() =>
                {
                    // Attempt to close gracefully, then kill forcefully
                    if (!process.HasExited)
                    {
                        try { process.CloseMainWindow(); } catch { /* ignore */ }
                        if (!process.WaitForExit(100))
                        {
                            try { process.Kill(); } catch { /* ignore */ }
                        }
                    }
                    tcs.TrySetCanceled(cancellationToken);
                });
            }
            return tcs.Task;
        }
    }
#endif

    /// <summary>
    /// The main class for performing video interpolation using RIFE and FFmpeg.
    /// </summary>
    public class VideoInterpolator
    {
        #region Public Properties

        // --- Executable Paths ---

        /// <summary>
        /// Gets the full path to the rife-ncnn-vulkan executable.
        /// </summary>
        public string RifeExecutablePath { get; }

        /// <summary>
        /// Gets the full path to the ffmpeg executable.
        /// </summary>
        public string FfmpegExecutablePath { get; }

        /// <summary>
        /// Gets the full path to the ffprobe executable.
        /// </summary>
        public string FfprobeExecutablePath { get; }

        // --- RIFE Settings ---

        /// <summary>
        /// Gets or sets the RIFE model name to use.
        /// </summary>
        public string RifeModelName { get; set; } = "rife-v4.6";

        /// <summary>
        /// Gets or sets the GPU ID for RIFE to use. Specify 'auto' for automatic selection.
        /// </summary>
        public string RifeGpuId { get; set; } = "auto";

        /// <summary>
        /// Gets or sets the thread settings for processing: [Load]:[Process]:[Save].
        /// </summary>
        public string RifeThreads { get; set; } = "1:2:2";

        /// <summary>
        /// Gets or sets a value indicating whether to enable spatial TTA (Test-Time Augmentation) mode.
        /// </summary>
        public bool RifeEnableSpatialTta { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to enable UHD mode for processing high-resolution videos.
        /// </summary>
        public bool RifeEnableUhdMode { get; set; } = false;

        // --- FFmpeg and Processing Settings ---

        /// <summary>
        /// Gets or sets the codec for encoding the final video.
        /// </summary>
        public string OutputVideoCodec { get; set; } = "libx264";

        /// <summary>
        /// Gets or sets additional parameters for the codec (e.g., "-crf 20").
        /// </summary>
        public string OutputVideoCodecParams { get; set; } = "-crf 20";

        /// <summary>
        /// Gets or sets the pixel format for the output video.
        /// </summary>
        public string OutputPixelFormat { get; set; } = "yuv420p";

        /// <summary>
        /// Gets or sets the format for intermediate frames (e.g., "png" or "jpg").
        /// </summary>
        public string IntermediateFrameFormat { get; set; } = "png";

        /// <summary>
        /// Gets or sets the compression level for PNG frames. -1 for default.
        /// </summary>
        public int PngCompressionLevel { get; set; } = -1;

        /// <summary>
        /// Gets or sets a value indicating whether to enable verbose console output for debugging.
        /// </summary>
        public bool VerboseLogging { get; set; } = false;

        #endregion

        private string _tempSessionDirectory = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoInterpolator"/> class.
        /// </summary>
        /// <param name="rifeExecutablePath">Path to rife-ncnn-vulkan.exe.</param>
        /// <param name="ffmpegExecutablePath">Path to ffmpeg.exe.</param>
        /// <param name="ffprobeExecutablePath">Optional path to ffprobe.exe. If not provided, an attempt will be made to find it next to ffmpeg.</param>
        /// <exception cref="FileNotFoundException">Thrown if RIFE or FFmpeg executables are not found.</exception>
        public VideoInterpolator(string rifeExecutablePath, string ffmpegExecutablePath, string? ffprobeExecutablePath = null)
        {
            if (string.IsNullOrWhiteSpace(rifeExecutablePath) || !File.Exists(rifeExecutablePath))
                throw new FileNotFoundException("RIFE executable not found.", rifeExecutablePath);
            if (string.IsNullOrWhiteSpace(ffmpegExecutablePath) || !File.Exists(ffmpegExecutablePath))
                throw new FileNotFoundException("FFmpeg executable not found.", ffmpegExecutablePath);

            RifeExecutablePath = Path.GetFullPath(rifeExecutablePath);
            FfmpegExecutablePath = Path.GetFullPath(ffmpegExecutablePath);

            // Auto-detect ffprobe path
            if (!string.IsNullOrWhiteSpace(ffprobeExecutablePath) && File.Exists(ffprobeExecutablePath))
            {
                FfprobeExecutablePath = Path.GetFullPath(ffprobeExecutablePath);
            }
            else
            {
                string probePath = Path.Combine(Path.GetDirectoryName(FfmpegExecutablePath) ?? string.Empty, "ffprobe.exe");
                if (File.Exists(probePath))
                {
                    FfprobeExecutablePath = probePath;
                    LogInternal($"FFprobe was not specified, but was found next to FFmpeg: {FfprobeExecutablePath}");
                }
                else
                {
                    // As a fallback, use ffmpeg, although this might not work for some commands.
                    FfprobeExecutablePath = FfmpegExecutablePath;
                    LogInternal("WARNING: ffprobe.exe not found. Using ffmpeg.exe path as a fallback, which may cause issues.", isError: true);
                }
            }
            LogInternal("VideoInterpolator instance created successfully.");
        }

        /// <summary>
        /// Asynchronously interpolates a video file, doubling its frame rate.
        /// </summary>
        /// <param name="inputVideoPath">Path to the source video file.</param>
        /// <param name="outputVideoPath">Path to save the processed video.</param>
        /// <param name="showConsoleWindows">If true, console windows will be shown for each launched process (RIFE, FFmpeg).</param>
        /// <param name="useNvdecForDecoding">If true, NVDEC hardware decoding will be used (requires an NVIDIA GPU).</param>
        /// <param name="progressCallback">An optional delegate to receive real-time stdout/stderr output from the processes.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A <see cref="ProcessExecutionResult"/> object containing the outcome of the operation.</returns>
        public async Task<ProcessExecutionResult> InterpolateVideoAsync(
             string inputVideoPath,
             string outputVideoPath,
             bool showConsoleWindows = false,
             bool useNvdecForDecoding = false,
             Action<string>? progressCallback = null,
             CancellationToken cancellationToken = default)
        {
            LogInternal($"Starting interpolation for '{Path.GetFileName(inputVideoPath)}'");

            if (!File.Exists(inputVideoPath))
                return new ProcessExecutionResult(-1, "", $"Input file not found: {inputVideoPath}");
            if (Path.GetFullPath(inputVideoPath).Equals(Path.GetFullPath(outputVideoPath), StringComparison.OrdinalIgnoreCase))
                return new ProcessExecutionResult(-1, "", "Input and output file paths cannot be the same.");

            SetupTempDirectory();
            string inputFramesDir = Path.Combine(_tempSessionDirectory, "input_frames");
            string outputFramesDir = Path.Combine(_tempSessionDirectory, "output_frames");

            try
            {
                Directory.CreateDirectory(inputFramesDir);
                Directory.CreateDirectory(outputFramesDir);

                // 1. Get the original video FPS
                var (originalFps, fpsResult) = await GetOriginalVideoFpsAsync(inputVideoPath, progressCallback, cancellationToken).ConfigureAwait(false);
                if (originalFps == null)
                    return new ProcessExecutionResult(fpsResult.ExitCode, fpsResult.StandardOutput, $"Failed to determine FPS. FFprobe error: {fpsResult.StandardError}");

                double newFps = originalFps.Value * 2;
                LogInternal($"Original FPS: {originalFps.Value:F3}, Target FPS: {newFps:F3}");

                // 2. Get the audio stream start time for precise synchronization
                var (audioStartTime, _) = await GetAudioStartTimeAsync(inputVideoPath, progressCallback, cancellationToken).ConfigureAwait(false);

                // 3. Extract frames from the video
                string framePattern = $"frame_%08d.{IntermediateFrameFormat.ToLowerInvariant()}";
                var extractResult = await ExtractFramesAsync(inputVideoPath, inputFramesDir, framePattern, useNvdecForDecoding, showConsoleWindows, progressCallback, cancellationToken).ConfigureAwait(false);
                if (!extractResult.Success) return extractResult;

                // 4. Perform frame interpolation using RIFE
                var rifeResult = await RunRifeInterpolationAsync(inputFramesDir, outputFramesDir, showConsoleWindows, progressCallback, cancellationToken).ConfigureAwait(false);
                if (!rifeResult.Success) return rifeResult;

                // 5. Assemble the final video from the interpolated frames
                string rifeOutputFramePattern = $"%08d.{IntermediateFrameFormat.ToLowerInvariant()}";
                var encodeResult = await EncodeFinalVideoAsync(outputVideoPath, outputFramesDir, rifeOutputFramePattern, newFps, inputVideoPath, audioStartTime, showConsoleWindows, progressCallback, cancellationToken).ConfigureAwait(false);
                if (!encodeResult.Success) return encodeResult;

                string successMessage = $"Interpolation completed successfully. Output saved to: {outputVideoPath}";
                LogInternal(successMessage);
                return new ProcessExecutionResult(0, successMessage, "");
            }
            catch (OperationCanceledException)
            {
                LogInternal("Operation was canceled by the user.", isError: true);
                return new ProcessExecutionResult(-99, "", "Operation was canceled.");
            }
            catch (Exception ex)
            {
                LogInternal($"An unexpected error occurred: {ex.Message}", isError: true);
                return new ProcessExecutionResult(-1, "", $"Critical error: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // Clean up temporary files
                CleanupTempDirectory();
                LogInternal($"Interpolation process finished for '{Path.GetFileName(inputVideoPath)}'.");
            }
        }

        #region Private Helper Methods

        private void LogInternal(string message, bool isError = false)
        {
            if (VerboseLogging)
            {
                Console.WriteLine($"[Interpolator][{(isError ? "ERROR" : "INFO")}] {message}");
            }
        }

        private string QuotePath(string path) => path.Contains(" ") ? $"\"{path}\"" : path;

        private async Task<ProcessExecutionResult> RunProcessAsync(
            string executablePath,
            string arguments,
            bool showWindow,
            string? workingDirectory = null,
            Action<string>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            LogInternal($"Running process: {Path.GetFileName(executablePath)} with args: {arguments}");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                RedirectStandardOutput = !showWindow,
                RedirectStandardError = !showWindow,
                UseShellExecute = false,
                CreateNoWindow = !showWindow,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
            };

            using var process = new Process { StartInfo = processStartInfo };

            var stdOutBuilder = new StringBuilder();
            var stdErrBuilder = new StringBuilder();

            if (!showWindow)
            {
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        progressCallback?.Invoke(args.Data);
                        stdOutBuilder.AppendLine(args.Data);
                    }
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        progressCallback?.Invoke($"[stderr] {args.Data}");
                        stdErrBuilder.AppendLine(args.Data);
                    }
                };
            }

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                LogInternal($"FAILED TO START PROCESS: {ex.Message}", isError: true);
                return new ProcessExecutionResult(-1, "", $"Failed to start process: {ex.Message}");
            }

            if (!showWindow)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            // A short delay to ensure all stream data has been read
            if (!showWindow)
            {
                await Task.Delay(150, CancellationToken.None).ConfigureAwait(false);
            }

            LogInternal($"Process finished with exit code {process.ExitCode}", isError: process.ExitCode != 0);
            return new ProcessExecutionResult(process.ExitCode, stdOutBuilder.ToString(), stdErrBuilder.ToString());
        }

        private async Task<(double? Fps, ProcessExecutionResult Result)> GetOriginalVideoFpsAsync(string videoPath, Action<string>? cb, CancellationToken ct)
        {
            LogInternal("Getting original video FPS...");
            string args = $"-v error -select_streams v:0 -show_entries stream=r_frame_rate -of default=noprint_wrappers=1:nokey=1 {QuotePath(videoPath)}";
            var result = await RunProcessAsync(FfprobeExecutablePath, args, false, progressCallback: cb, cancellationToken: ct).ConfigureAwait(false);

            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                string fpsString = result.StandardOutput.Trim();
                if (fpsString.Contains("/"))
                {
                    var parts = fpsString.Split('/');
                    if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double num) &&
                        double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double den) && den != 0)
                    {
                        return (num / den, result);
                    }
                }
                else if (double.TryParse(fpsString, NumberStyles.Any, CultureInfo.InvariantCulture, out double fpsVal))
                {
                    return (fpsVal, result);
                }
            }
            LogInternal("Failed to get or parse FPS.", isError: true);
            return (null, result);
        }

        private async Task<(double? StartTime, ProcessExecutionResult Result)> GetAudioStartTimeAsync(string videoPath, Action<string>? cb, CancellationToken ct)
        {
            LogInternal("Getting audio stream start time...");
            string args = $"-v error -select_streams a:0 -show_entries stream=start_time -of default=noprint_wrappers=1:nokey=1 {QuotePath(videoPath)}";
            var result = await RunProcessAsync(FfprobeExecutablePath, args, false, progressCallback: cb, cancellationToken: ct).ConfigureAwait(false);

            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                string startTimeStr = result.StandardOutput.Trim();
                if (double.TryParse(startTimeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double startTime) && startTime > 0)
                {
                    LogInternal($"Detected audio start time offset: {startTime.ToString(CultureInfo.InvariantCulture)}s.");
                    return (startTime, result);
                }
            }
            LogInternal("Audio start time is 0 or could not be determined. No offset will be applied.");
            return (null, result);
        }

        private async Task<ProcessExecutionResult> ExtractFramesAsync(string inputPath, string outputDir, string pattern, bool useNvdec, bool showCmd, Action<string>? cb, CancellationToken ct)
        {
            LogInternal("Extracting frames...");
            string hwaccelArg = useNvdec ? "-hwaccel nvdec " : "";
            var args = new StringBuilder();
            args.Append($"-y -nostdin {hwaccelArg}-i {QuotePath(inputPath)} -vsync vfr ");
            if (IntermediateFrameFormat.Equals("png", StringComparison.OrdinalIgnoreCase) && PngCompressionLevel >= 0)
            {
                args.Append($"-compression_level {PngCompressionLevel} ");
            }
            args.Append(QuotePath(Path.Combine(outputDir, pattern)));

            var result = await RunProcessAsync(FfmpegExecutablePath, args.ToString(), showCmd, _tempSessionDirectory, cb, ct).ConfigureAwait(false);
            if (result.Success) LogInternal("Frames extracted successfully.");
            else LogInternal("Error during frame extraction.", isError: true);
            return result;
        }

        private async Task<ProcessExecutionResult> RunRifeInterpolationAsync(string inputDir, string outputDir, bool showCmd, Action<string>? cb, CancellationToken ct)
        {
            LogInternal("Starting RIFE interpolation...");
            var argsList = new List<string>
            {
                "-i", QuotePath(inputDir),
                "-o", QuotePath(outputDir),
                "-f", IntermediateFrameFormat.ToLowerInvariant()
            };
            if (VerboseLogging) argsList.Add("-v");
            if (!string.IsNullOrWhiteSpace(RifeModelName)) argsList.AddRange(new[] { "-m", RifeModelName });
            if (!string.IsNullOrWhiteSpace(RifeGpuId) && RifeGpuId.ToLowerInvariant() != "auto") argsList.AddRange(new[] { "-g", RifeGpuId });
            if (!string.IsNullOrWhiteSpace(RifeThreads)) argsList.AddRange(new[] { "-j", RifeThreads });
            if (RifeEnableSpatialTta) argsList.Add("-x");
            if (RifeEnableUhdMode) argsList.Add("-u");

            var result = await RunProcessAsync(RifeExecutablePath, string.Join(" ", argsList), showCmd, Path.GetDirectoryName(RifeExecutablePath), cb, ct).ConfigureAwait(false);
            if (result.Success) LogInternal("RIFE interpolation finished.");
            else LogInternal("Error during RIFE interpolation.", isError: true);
            return result;
        }

        private async Task<ProcessExecutionResult> EncodeFinalVideoAsync(string outputPath, string framesDir, string pattern, double fps, string originalVideoPath, double? audioOffset, bool showCmd, Action<string>? cb, CancellationToken ct)
        {
            LogInternal("Encoding final video...");
            var outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir != null && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var args = new StringBuilder();
            args.Append("-y -nostdin ");
            if (audioOffset.HasValue && audioOffset.Value > 0)
            {
                LogInternal($"Applying video offset of {audioOffset.Value.ToString(CultureInfo.InvariantCulture)}s to match audio start time.");
                args.Append($"-itsoffset {audioOffset.Value.ToString(CultureInfo.InvariantCulture)} ");
            }
            args.Append($"-framerate {fps.ToString(CultureInfo.InvariantCulture)} -i {QuotePath(Path.Combine(framesDir, pattern))} ");
            args.Append($"-i {QuotePath(originalVideoPath)} ");
            args.Append("-map 0:v:0 -map 1:a? -map 1:s? "); // Map video from frames, audio and subtitles from original
            args.Append($"-c:v {OutputVideoCodec} {OutputVideoCodecParams} -pix_fmt {OutputPixelFormat} ");
            args.Append("-c:a copy -c:s copy ");
            args.Append("-map_metadata 1 ");
            args.Append("-loglevel warning ");
            args.Append(QuotePath(outputPath));

            var result = await RunProcessAsync(FfmpegExecutablePath, args.ToString(), showCmd, _tempSessionDirectory, cb, ct).ConfigureAwait(false);
            if (result.Success) LogInternal("Final video encoded successfully.");
            else LogInternal("Error during final video encoding.", isError: true);
            return result;
        }

        private void SetupTempDirectory()
        {
            string appBasePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            _tempSessionDirectory = Path.Combine(appBasePath, "TempProcessingData", "Session_" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(_tempSessionDirectory);
            LogInternal($"Temporary directory created: {_tempSessionDirectory}");
        }

        private void CleanupTempDirectory()
        {
            if (string.IsNullOrEmpty(_tempSessionDirectory) || !Directory.Exists(_tempSessionDirectory)) return;

            // A simple safety check to avoid deleting something important by accident
            if (_tempSessionDirectory.Contains("TempProcessingData") && _tempSessionDirectory.Contains("Session_"))
            {
                try
                {
                    Directory.Delete(_tempSessionDirectory, true);
                    LogInternal($"Temporary directory '{_tempSessionDirectory}' deleted.");
                }
                catch (Exception ex)
                {
                    LogInternal($"Failed to delete temporary directory '{_tempSessionDirectory}': {ex.Message}", isError: true);
                }
            }
            else
            {
                LogInternal($"WARNING: Temporary directory path '{_tempSessionDirectory}' seems suspicious, cleanup skipped.", isError: true);
            }
        }

        #endregion
    }
}