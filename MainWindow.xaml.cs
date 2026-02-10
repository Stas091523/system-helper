using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiscordRPC;
using System.Management;

namespace MyDarkApp
{
    public partial class MainWindow : Window
    {
        // ВАЖНО: Укажи здесь текущую версию. 
        // Если на GitHub в version.txt будет 1.0.1, а тут 1.0.0 — сработает обновление.
        private string CurrentVersion = "1.0.1"; 
        
        private List<Process> _allProcesses = new List<Process>();
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private float totalRamMBytes = 0;
        private DiscordRpcClient client;

        public MainWindow()
        {
            InitializeComponent();
            InitCounters();
            LoadApplication();
        }

        private async void LoadApplication()
        {
            // Анимация появления
            var fadeIn = (System.Windows.Media.Animation.Storyboard)Resources["FadeIn"];
            fadeIn.Begin(MainBorder);

            // 1. Проверка обновлений
            StatusText.Text = "Проверка обновлений...";
            await CheckForUpdates();

            // 2. Инициализация Discord
            StatusText.Text = "Подключение к Discord...";
            InitializeDiscord();

            // 3. Загрузка процессов
            StatusText.Text = "Сбор данных о процессах...";
            RefreshProcessList();

            await Task.Delay(1500);
            LoadingGrid.Visibility = Visibility.Collapsed;
        }

        private async Task CheckForUpdates()
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    // Ссылка на твой файл с версией на GitHub
                    string latestVersion = await wc.DownloadStringTaskAsync("https://raw.githubusercontent.com/Stas091523/system-helper/main/version.txt");
                    latestVersion = latestVersion.Trim();

                    if (latestVersion != CurrentVersion)
                    {
                        var result = MessageBox.Show($"Найдено обновление {latestVersion}. Установить сейчас?", "Update", MessageBoxButton.YesNo, MessageBoxImage.Information);
                        if (result == MessageBoxResult.Yes)
                        {
                            // Ссылка на прямой скачивание твоего EXE
                            string downloadUrl = "https://github.com/Stas091523/system-helper/releases/latest/download/idk.exe";
                            InstallUpdate(downloadUrl);
                        }
                    }
                }
            }
            catch { /* Игнорируем ошибки сети */ }
        }

        private void InstallUpdate(string downloadUrl)
        {
            try
            {
                string currentPath = Process.GetCurrentProcess().MainModule.FileName;
                string newPath = currentPath + ".new";
                string updaterPath = Path.Combine(Path.GetDirectoryName(currentPath), "updater.bat");

                using (WebClient wc = new WebClient())
                {
                    // Скачиваем новый файл с припиской .new
                    wc.DownloadFile(downloadUrl, newPath);

                    // Создаем батник, который подождет закрытия, удалит старый EXE и переименует новый
                    string batContent = $@"
@echo off
timeout /t 2 /nobreak > nul
del /f /q ""{currentPath}""
move /y ""{newPath}"" ""{currentPath}""
start """" ""{currentPath}""
del ""%~f0""
";
                    File.WriteAllText(updaterPath, batContent);

                    // Запускаем батник
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });

                    // Выходим из текущей программы
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при установке обновления: " + ex.Message);
            }
        }

        private void InitCounters()
        {
            try {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get()) {
                    totalRamMBytes = float.Parse(obj["TotalVisibleMemorySize"].ToString()) / 1024;
                }
            } catch { totalRamMBytes = 16384; }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => {
                try {
                    CpuBar.Value = cpuCounter.NextValue();
                    float used = totalRamMBytes - ramCounter.NextValue();
                    RamBar.Value = (used / totalRamMBytes) * 100;
                } catch { }
            };
            timer.Start();
        }

        private void InitializeDiscord()
        {
            try {
                client = new DiscordRpcClient("1470707542165422162");
                client.OnReady += (s, e) => Dispatcher.Invoke(() => {
                    DiscordUserText.Text = e.User.Username.ToUpper();
                    AvatarImage.ImageSource = new BitmapImage(new Uri(e.User.GetAvatarURL(User.AvatarFormat.PNG)));
                });
                client.Initialize();
                client.SetPresence(new RichPresence { Details = "System Monitoring", State = "v" + CurrentVersion });
            } catch { }
        }

        private void RefreshProcessList()
        {
            _allProcesses = Process.GetProcesses().OrderBy(p => p.ProcessName).ToList();
            ProcessList.ItemsSource = _allProcesses;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
            ProcessList.ItemsSource = _allProcesses.Where(p => p.ProcessName.ToLower().Contains(SearchBox.Text.ToLower())).ToList();

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshProcessList();
        
        private void Close_Click(object sender, RoutedEventArgs e) 
        { 
            client?.Dispose(); 
            Application.Current.Shutdown(); 
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Kill_Click(object sender, RoutedEventArgs e) 
        {
            if (ProcessList.SelectedItem is Process p) 
            {
                try { p.Kill(); Task.Delay(500).ContinueWith(_ => Dispatcher.Invoke(RefreshProcessList)); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e) 
        { 
            base.OnMouseLeftButtonDown(e); 
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); 
        }
    }
}