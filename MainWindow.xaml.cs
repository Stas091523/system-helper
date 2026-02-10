using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiscordRPC;
using System.IO;

namespace MyDarkApp
{
    public partial class MainWindow : Window
    {
        private string CurrentVersion = "1.0.0";
        private string VersionUrl = "https://raw.githubusercontent.com/ТВОЙ_НИК/РЕПО/main/version.txt";
        private string ExeDownloadUrl = "https://github.com/ТВОЙ_НИК/РЕПО/releases/latest/download/idk.exe";

        private List<Process> _allProcesses = new List<Process>();
        private DiscordRpcClient? client;
        private PerformanceCounter? cpuCounter;
        private PerformanceCounter? ramCounter;

        public MainWindow()
        {
            InitializeComponent();
            InitCounters();
            LoadApplication();
        }

        private async void LoadApplication()
        {
            var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["FadeIn"];
            sb.Begin(MainBorder);

            StatusText.Text = "Проверка обновлений...";
            await CheckUpdatesSilent();

            StatusText.Text = "Подключение к Discord...";
            InitializeDiscord();

            StatusText.Text = "Сбор данных о процессах...";
            RefreshProcessList();

            await Task.Delay(1500);
            LoadingGrid.Visibility = Visibility.Collapsed;
        }

        private async Task CheckUpdatesSilent()
        {
            try {
                using (HttpClient web = new HttpClient()) {
                    web.Timeout = TimeSpan.FromSeconds(5);
                    string latestVersion = (await web.GetStringAsync(VersionUrl)).Trim();

                    if (latestVersion != CurrentVersion) {
                        var res = MessageBox.Show($"Найдено обновление {latestVersion}. Установить сейчас?", "Update", MessageBoxButton.YesNo, MessageBoxImage.Information);
                        if (res == MessageBoxResult.Yes) {
                            StatusText.Text = "Загрузка новой версии...";
                            byte[] data = await web.GetByteArrayAsync(ExeDownloadUrl);
                            
                            var currentProcess = Process.GetCurrentProcess();
                            string? currentExe = currentProcess.MainModule?.FileName;
                            
                            if (string.IsNullOrEmpty(currentExe)) return;

                            string updateExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_new.exe");
                            
                            File.WriteAllBytes(updateExe, data); // ИСПРАВЛЕНО: было updatePath
                            
                            string fileName = Path.GetFileName(currentExe);
                            string batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.bat");
                            string batContent = $@"
@echo off
timeout /t 2 /nobreak > nul
del ""{currentExe}""
ren ""{updateExe}"" ""{fileName}""
start """" ""{currentExe}""
del ""%~f0""";
                            File.WriteAllText(batPath, batContent);
                            Process.Start(new ProcessStartInfo(batPath) { CreateNoWindow = true, UseShellExecute = true });
                            Application.Current.Shutdown();
                        }
                    }
                }
            } catch { }
        }

        private void InitCounters()
        {
            try {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            } catch { }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => {
                if (cpuCounter != null) CpuBar.Value = cpuCounter.NextValue();
                if (ramCounter != null) {
                    float avail = ramCounter.NextValue();
                    RamBar.Value = Math.Clamp(100 - (avail / 160), 0, 100);
                }
            };
            timer.Start();
        }

        private void InitializeDiscord()
        {
            client = new DiscordRpcClient("1470707542165422162");
            client.OnReady += (s, e) => Dispatcher.Invoke(() => {
                DiscordUserText.Text = e.User.Username.ToUpper();
                try {
                    AvatarImage.ImageSource = new BitmapImage(new Uri(e.User.GetAvatarURL(User.AvatarFormat.PNG)));
                } catch { }
            });
            client.Initialize();
            client.SetPresence(new RichPresence { Details = "Управляет процессами", State = "Версия " + CurrentVersion });
        }

        private void RefreshProcessList()
        {
            _allProcesses = Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.ProcessName)).OrderBy(p => p.ProcessName).ToList();
            if (ProcessList != null) ProcessList.ItemsSource = _allProcesses;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (ProcessList != null) ProcessList.ItemsSource = _allProcesses.Where(p => p.ProcessName.ToLower().Contains(SearchBox.Text.ToLower())).ToList();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshProcessList();
        private void Close_Click(object sender, RoutedEventArgs e) { client?.Dispose(); Application.Current.Shutdown(); }
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        
        private void Kill_Click(object sender, RoutedEventArgs e) {
            if (ProcessList.SelectedItem is Process p) {
                try { p.Kill(); Task.Delay(500).ContinueWith(_ => Dispatcher.Invoke(RefreshProcessList)); }
                catch { }
            }
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); DragMove(); }
    }
}