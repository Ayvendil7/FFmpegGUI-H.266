using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Конвертер_FFMpeg
{
    public partial class MainWindow : Window
{
    private string? _inputFilePath;
    private TimeSpan _totalDuration;

        public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSelectFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Видеофайлы|*.mp4;*.avi;*.mkv;*.mov" };
        if (dialog.ShowDialog() == true)
        {
            _inputFilePath = dialog.FileName;
            SelectedFileTextBlock.Text = $"Выбран файл: {Path.GetFileName(_inputFilePath)}";
        }
    }

    private async void OnConvertClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_inputFilePath))
        {
            MessageBox.Show("Выберите файл для конвертации!");
            return;
        }

                var videoCodec = (VideoCodecComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
        var audioCodec = (AudioCodecComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
        var preset = (PresetComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
        var videoBitrateText = VideoBitrateTextBox.Text;
        var audioBitrateText = AudioBitrateTextBox.Text;
        var resolution = ResolutionTextBox.Text;

        if (string.IsNullOrEmpty(videoCodec) || string.IsNullOrEmpty(audioCodec))
        {
            MessageBox.Show("Выберите видеокодек и аудиокодек!");
            return;
        }

                        var codecName = videoCodec.Split(' ')[0]; // Извлекаем первое слово для имени файла
        var outputFilePath = Path.Combine(
            Path.GetDirectoryName(_inputFilePath) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(_inputFilePath)}_{codecName}.mp4"
        );

        // ✅ Проверка существования файла
        if (File.Exists(outputFilePath))
        {
            var result = MessageBox.Show(
                "Файл уже существует. Перезаписать?",
                "Подтверждение",
                MessageBoxButton.YesNo
            );
            if (result != MessageBoxResult.Yes)
                return;
        }

        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;
        LogTextBlock.Text = "Конвертация началась...";

        await Task.Run(() => RunFFmpegConversion(_inputFilePath, outputFilePath, videoCodec, audioCodec, videoBitrateText, audioBitrateText, resolution, preset ?? "medium"));
    }

            private void RunFFmpegConversion(string inputPath, string outputPath, string videoCodec, string audioCodec, 
        string videoBitrate, string audioBitrate, string resolution, string preset)
    {
        try
        {
            // Сначала получаем продолжительность видео
            GetVideoDuration(inputPath);

            // Маппинг кодеков на команды FFmpeg
            var videoCodecArg = videoCodec switch
            {
                var s when s.Contains("H.266") => "libvvenc",
                var s when s.Contains("H.265") => "libx265",
                var s when s.Contains("AV1") => "libaom-av1",
                var s when s.Contains("VP9") => "libvpx-vp9",
                _ => "libx265"
            };

            var audioCodecArg = audioCodec switch
            {
                "Opus" => "libopus",
                "AAC" => "aac",
                "Vorbis" => "libvorbis",
                _ => "libopus"
            };

            // Создаём команду FFmpeg
            var arguments = $"-i \"{inputPath}\" -c:v {videoCodecArg} -b:v {videoBitrate} -preset {preset} -c:a {audioCodecArg} -b:a {audioBitrate}";
            
            // Добавляем разрешение, если указано
            if (!string.IsNullOrWhiteSpace(resolution))
            {
                arguments += $" -s {resolution}";
            }
            
            arguments += $" -y \"{outputPath}\"";

            var processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ParseProgress(e.Data);
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Visibility = Visibility.Collapsed;
                    ProgressBar.Value = 100;
                    LogTextBlock.Text = "✓ Конвертация завершена успешно!";
                    MessageBox.Show("Конвертация успешно завершена!", "Успех");
                });
            }
            else
            {
                throw new Exception($"FFmpeg завершился с кодом ошибки: {process.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Visibility = Visibility.Collapsed;
                LogTextBlock.Text = $"❌ Ошибка: {ex.Message}";
                MessageBox.Show($"Ошибка конвертации: {ex.Message}\n\nУбедитесь, что FFmpeg установлен и доступен через PATH.", "Ошибка");
            });
        }
    }

    private void GetVideoDuration(string inputPath)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{inputPath}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process != null)
        {
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out double seconds))
            {
                _totalDuration = TimeSpan.FromSeconds(seconds);
            }
        }
    }

    private void ParseProgress(string data)
    {
        // Парсим время из вывода FFmpeg
        var timeMatch = Regex.Match(data, @"time=([\d:.]+)");
        if (timeMatch.Success && _totalDuration.TotalSeconds > 0)
        {
            var timeParts = timeMatch.Groups[1].Value.Split(':');
            if (timeParts.Length == 3 &&
                int.TryParse(timeParts[0], out int hours) &&
                int.TryParse(timeParts[1], out int minutes) &&
                double.TryParse(timeParts[2], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double seconds))
            {
                var currentTime = new TimeSpan(hours, minutes, (int)seconds);
                var progress = (currentTime.TotalSeconds / _totalDuration.TotalSeconds) * 100;
                var remaining = _totalDuration - currentTime;

                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Value = Math.Min(progress, 100);
                    LogTextBlock.Text = $"Прогресс: {progress:F1}%\n" +
                        $"Время: {currentTime:hh\\:mm\\:ss} / {_totalDuration:hh\\:mm\\:ss}\n" +
                        $"Осталось: {remaining:hh\\:mm\\:ss}";
                });
            }
        }
    }
    }
}