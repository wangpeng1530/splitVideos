using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Threading;

namespace splitVideos
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ClipButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath = FilePathTextBox.Text;
            string fileName = FileNameTextBox.Text;
            string clipTime = ClipTimeTextBox.Text;

            string fullInputFilePath = Path.Combine(filePath, fileName);

            if (!File.Exists(fullInputFilePath))
            {
                MessageBox.Show("输入文件路径不存在。");
                return;
            }

            if (Path.GetExtension(fullInputFilePath).ToLower() != ".mp4")
            {
                MessageBox.Show("输入文件不是mp4文件。");
                return;
            }

            string outputFileName = Path.GetFileNameWithoutExtension(fileName) + "_clip.mp4";
            string outputFilePath = Path.Combine(filePath, outputFileName);

            string ffmpegCommand = $"-i \"{fullInputFilePath}\" -ss {clipTime} -c copy \"{outputFilePath}\"";

            TimeSpan videoDuration = GetVideoDuration(fullInputFilePath);
            ExecuteFFmpegCommand(ffmpegCommand, videoDuration);

        }

        private async void ExecuteFFmpegCommand(string command, TimeSpan totalDuration)
        {
            string ffmpegPath = @"D:\ffmpeg-master-latest-win64-gpl-shared\bin\ffmpeg.exe";
            if (!File.Exists(ffmpegPath) && !IsInPath(ffmpegPath))
            {
                MessageBox.Show("ffmpeg 未安装或未添加到系统 PATH 环境变量中。");
                return;
            }

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (sender, e) => UpdateProgress(e.Data, totalDuration);
            process.ErrorDataReceived += (sender, e) => UpdateProgress(e.Data, totalDuration);

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 等待进程退出
                await Task.Run(() => process.WaitForExit());

                // 读取输出和错误流
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    MessageBox.Show("剪辑成功！");
                }
                else
                {
                    MessageBox.Show($"剪辑失败：{error}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行 ffmpeg 命令时发生异常：{ex.Message}");
            }
        }

        private void UpdateProgress(string data, TimeSpan totalDuration)
        {
            if (string.IsNullOrEmpty(data))
                return;

            Debug.WriteLine($"ffmpeg output: {data}");

            // 解析 ffmpeg 输出中的进度信息
            if (data.Contains("time="))
            {
                var timeIndex = data.IndexOf("time=");
                var timeString = data.Substring(timeIndex + 5, 11); // 格式为 "00:00:00.00"
                Debug.WriteLine($"Parsed time string: {timeString}");
                if (TimeSpan.TryParse(timeString, out TimeSpan currentTime))
                {
                    double progress = currentTime.TotalSeconds / totalDuration.TotalSeconds * 100;
                    Debug.WriteLine($"Current time: {currentTime}, Progress: {progress}%");
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = progress;
                            Debug.WriteLine($"ProgressBar value updated to: {ProgressBar.Value}");
                        }, DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Exception in Dispatcher.Invoke: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine("Failed to parse time string.");
                }
            }
            else
            {
                Debug.WriteLine($"No time information found in data: {data}");
            }
        }




        private bool IsInPath(string fileName)
        {
            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                if (File.Exists(Path.Combine(path, fileName)))
                {
                    return true;
                }
            }
            return false;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = Path.GetDirectoryName(files[0]);
                    string fileName = Path.GetFileName(files[0]);

                    FilePathTextBox.Text = filePath;
                    FileNameTextBox.Text = fileName;
                }
            }
        }
        private TimeSpan GetVideoDuration(string filePath)
        {
            string ffmpegPath = @"D:\ffmpeg-master-latest-win64-gpl-shared\bin\ffmpeg.exe";
            if (!File.Exists(ffmpegPath) && !IsInPath(ffmpegPath))
            {
                MessageBox.Show("ffmpeg 未安装或未添加到系统 PATH 环境变量中。");
                return TimeSpan.Zero;
            }

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = new Process
            {
                StartInfo = processStartInfo
            };

            process.Start();
            string output = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var durationMatch = System.Text.RegularExpressions.Regex.Match(output, @"Duration: (\d{2}):(\d{2}):(\d{2})\.(\d{2})");
            if (durationMatch.Success)
            {
                int hours = int.Parse(durationMatch.Groups[1].Value);
                int minutes = int.Parse(durationMatch.Groups[2].Value);
                int seconds = int.Parse(durationMatch.Groups[3].Value);
                int milliseconds = int.Parse(durationMatch.Groups[4].Value) * 10;
                return new TimeSpan(0, hours, minutes, seconds, milliseconds);
            }

            return TimeSpan.Zero;
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
    }
}
