using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace DiscordDnsApp
{
    public partial class MainWindow : Window
    {
        private const string CommentTag = "DiscordDNS-Bypass";
        private const string DnsPrimary = "1.1.1.1";
        private const string DnsSecondary = "1.0.0.1";

        private static readonly string[] DiscordDomains = new[]
        {
            ".discord.com",
            ".discord.gg",
            ".discordapp.com",
            ".discord.media",
            ".discordapp.net",
            ".discord.dev"
        };

        private readonly SolidColorBrush _green = new(System.Windows.Media.Color.FromRgb(0x57, 0xF2, 0x87));
        private readonly SolidColorBrush _red = new(System.Windows.Media.Color.FromRgb(0xED, 0x42, 0x45));
        private readonly SolidColorBrush _yellow = new(System.Windows.Media.Color.FromRgb(0xFE, 0xE7, 0x5C));
        private readonly SolidColorBrush _blurple = new(System.Windows.Media.Color.FromRgb(0x58, 0x65, 0xF2));

        private bool _isActive;
        private Process? _gdpiProcess;
        private WinForms.NotifyIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();
            Log("Uygulama baslatildi.");
            RefreshStatus();
        }

        // --- GoodbyeDPI Path ---

        private string GetGoodbyeDpiDir()
        {
            // Look relative to the project root (two levels up from exe, or next to exe)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Try: <project>/goodbyedpi/x86_64/
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var gdpiDir = Path.Combine(projectRoot, "goodbyedpi", "x86_64");
            if (File.Exists(Path.Combine(gdpiDir, "goodbyedpi.exe")))
                return gdpiDir;

            // Try: <baseDir>/../../../goodbyedpi/x86_64/ (publish folder)
            for (int i = 1; i <= 6; i++)
            {
                var up = baseDir;
                for (int j = 0; j < i; j++)
                    up = Path.GetFullPath(Path.Combine(up, ".."));
                gdpiDir = Path.Combine(up, "goodbyedpi", "x86_64");
                if (File.Exists(Path.Combine(gdpiDir, "goodbyedpi.exe")))
                    return gdpiDir;
            }

            // Fallback: hardcoded project path
            gdpiDir = @"C:\Users\UeanoS\discord-dns\goodbyedpi\x86_64";
            if (File.Exists(Path.Combine(gdpiDir, "goodbyedpi.exe")))
                return gdpiDir;

            return string.Empty;
        }

        // --- GoodbyeDPI Process Management ---

        private bool IsGoodbyeDpiRunning()
        {
            // Check our managed process
            if (_gdpiProcess != null && !_gdpiProcess.HasExited)
                return true;

            // Check if any goodbyedpi.exe is running system-wide
            return Process.GetProcessesByName("goodbyedpi").Length > 0;
        }

        private bool StartGoodbyeDpi()
        {
            if (IsGoodbyeDpiRunning())
            {
                Dispatcher.Invoke(() => Log("GoodbyeDPI zaten calisiyor."));
                return true;
            }

            var gdpiDir = GetGoodbyeDpiDir();
            if (string.IsNullOrEmpty(gdpiDir))
            {
                Dispatcher.Invoke(() => Log("HATA: GoodbyeDPI bulunamadi!"));
                return false;
            }

            var exePath = Path.Combine(gdpiDir, "goodbyedpi.exe");
            Dispatcher.Invoke(() => Log($"GoodbyeDPI baslatiliyor..."));

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-5 --set-ttl 5 --dns-addr 77.88.8.8 --dns-port 1253 --dnsv6-addr 2a02:6b8::feed:0ff --dnsv6-port 1253",
                    WorkingDirectory = gdpiDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _gdpiProcess = Process.Start(psi);

                if (_gdpiProcess != null && !_gdpiProcess.HasExited)
                {
                    Dispatcher.Invoke(() => Log($"GoodbyeDPI baslatildi (PID: {_gdpiProcess.Id})."));
                    return true;
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log($"HATA: GoodbyeDPI baslatilamadi: {ex.Message}"));
            }
            return false;
        }

        private void StopGoodbyeDpi()
        {
            // Kill our managed process
            if (_gdpiProcess != null && !_gdpiProcess.HasExited)
            {
                try
                {
                    _gdpiProcess.Kill();
                    _gdpiProcess.WaitForExit(5000);
                    Dispatcher.Invoke(() => Log("GoodbyeDPI durduruldu."));
                }
                catch { }
                _gdpiProcess = null;
            }

            // Also kill any other goodbyedpi.exe instances
            foreach (var p in Process.GetProcessesByName("goodbyedpi"))
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(3000);
                }
                catch { }
            }
        }

        // --- System Tray ---

        private void SetupTrayIcon()
        {
            _trayIcon = new WinForms.NotifyIcon();

            var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "discord.ico");
            if (!File.Exists(icoPath))
            {
                var discordIco = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Discord", "app.ico");
                if (File.Exists(discordIco))
                    icoPath = discordIco;
            }

            _trayIcon.Icon = File.Exists(icoPath) ? new Icon(icoPath) : SystemIcons.Application;
            _trayIcon.Text = "Discord Bypass";
            _trayIcon.Visible = true;

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Goster", null, (_, _) => ShowFromTray());
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Cikis", null, (_, _) => ExitApp());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (_, _) => ShowFromTray();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApp()
        {
            // Stop GoodbyeDPI on exit
            StopGoodbyeDpi();

            // Remove NRPT rules on exit
            RunPowerShell(
                $"Get-DnsClientNrptRule | Where-Object {{ $_.Comment -eq '{CommentTag}' }} | " +
                "ForEach-Object { Remove-DnsClientNrptRule -Name $_.Name -Force }");
            RunPowerShell("Clear-DnsClientCache");

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            _trayIcon?.ShowBalloonTip(
                2000,
                "Discord Bypass",
                "Arka planda calismaya devam ediyor. Kapatmak icin sag tiklayin.",
                WinForms.ToolTipIcon.Info);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.OnClosed(e);
        }

        // --- UI Helpers ---

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            if (LogText.Text.Length > 0)
                LogText.Text += "\n";
            LogText.Text += $"[{timestamp}] {message}";
            LogScroller.ScrollToEnd();
        }

        private void SetStatus(bool active, int ruleCount, bool gdpiRunning)
        {
            _isActive = active && gdpiRunning;
            if (_isActive)
            {
                StatusDot.Fill = _green;
                StatusText.Text = "Durum: ETKIN";
                StatusDetail.Text = $"GoodbyeDPI aktif, {ruleCount} NRPT kurali";
                BtnToggle.Content = "Devre Disi Birak";
                BtnToggle.Background = _red;
            }
            else
            {
                StatusDot.Fill = _red;
                StatusText.Text = "Durum: DEVRE DISI";
                StatusDetail.Text = gdpiRunning ? "GoodbyeDPI aktif ama NRPT kurali yok" : "Bypass kapali";
                BtnToggle.Content = "Etkinlestir";
                BtnToggle.Background = _blurple;
            }

            if (_trayIcon != null)
                _trayIcon.Text = _isActive ? "Discord Bypass - ETKIN" : "Discord Bypass - DEVRE DISI";
        }

        private void SetBusy(bool busy)
        {
            BtnToggle.IsEnabled = !busy;
            if (busy)
            {
                StatusDot.Fill = _yellow;
                StatusText.Text = "Durum: ISLENIYOR...";
                StatusDetail.Text = "";
            }
        }

        private void UpdateRulesList(List<NrptRule> rules)
        {
            if (rules.Count > 0)
            {
                RulesList.ItemsSource = rules;
                RulesList.Visibility = Visibility.Visible;
                NoRulesText.Visibility = Visibility.Collapsed;
            }
            else
            {
                RulesList.ItemsSource = null;
                RulesList.Visibility = Visibility.Collapsed;
                NoRulesText.Visibility = Visibility.Visible;
            }
        }

        // --- PowerShell Execution ---

        private static string RunPowerShell(string script)
        {
            var bytes = Encoding.Unicode.GetBytes(script);
            var encoded = Convert.ToBase64String(bytes);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null) return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);
            return output.Trim();
        }

        // --- NRPT Operations ---

        private List<NrptRule> GetCurrentRules()
        {
            var rules = new List<NrptRule>();

            var script =
                $"Get-DnsClientNrptRule | Where-Object {{ $_.Comment -eq '{CommentTag}' }} | " +
                "ForEach-Object { \"$($_.Namespace)|$($_.NameServers -join ',')\" }";

            var output = RunPowerShell(script);

            if (string.IsNullOrWhiteSpace(output)) return rules;

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split('|');
                if (parts.Length >= 2)
                {
                    rules.Add(new NrptRule
                    {
                        Domain = parts[0],
                        Servers = parts[1]
                    });
                }
            }
            return rules;
        }

        private void RefreshStatus()
        {
            SetBusy(true);
            var thread = new Thread(() =>
            {
                var rules = GetCurrentRules();
                var gdpiRunning = IsGoodbyeDpiRunning();
                Dispatcher.Invoke(() =>
                {
                    SetStatus(rules.Count > 0, rules.Count, gdpiRunning);
                    UpdateRulesList(rules);
                    if (gdpiRunning && rules.Count > 0)
                        Log($"Durum kontrol: Bypass etkin ({rules.Count} NRPT + GoodbyeDPI).");
                    else if (gdpiRunning)
                        Log("Durum kontrol: GoodbyeDPI calisiyor ama NRPT kurali yok.");
                    else if (rules.Count > 0)
                        Log($"Durum kontrol: {rules.Count} NRPT kurali var ama GoodbyeDPI kapali.");
                    else
                        Log("Durum kontrol: Bypass devre disi.");
                    SetBusy(false);
                });
            })
            { IsBackground = true };
            thread.Start();
        }

        private void EnableBypass()
        {
            SetBusy(true);
            Log("Etkinlestirme baslatiliyor...");

            var thread = new Thread(() =>
            {
                // 1. Start GoodbyeDPI
                var gdpiOk = StartGoodbyeDpi();

                // 2. Remove existing NRPT rules
                var existing = GetCurrentRules();
                if (existing.Count > 0)
                {
                    Dispatcher.Invoke(() => Log("Mevcut NRPT kurallar kaldiriliyor..."));
                    RunPowerShell(
                        $"Get-DnsClientNrptRule | Where-Object {{ $_.Comment -eq '{CommentTag}' }} | " +
                        "ForEach-Object { Remove-DnsClientNrptRule -Name $_.Name -Force }");
                }

                // 3. Ensure DoH servers
                Dispatcher.Invoke(() => Log("DoH sunuculari kontrol ediliyor..."));
                RunPowerShell(
                    $"$s = Get-DnsClientDohServerAddress -ServerAddress '{DnsPrimary}' -ErrorAction SilentlyContinue; " +
                    $"if (-not $s) {{ Add-DnsClientDohServerAddress -ServerAddress '{DnsPrimary}' " +
                    $"-DohTemplate 'https://cloudflare-dns.com/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true }}");
                RunPowerShell(
                    $"$s = Get-DnsClientDohServerAddress -ServerAddress '{DnsSecondary}' -ErrorAction SilentlyContinue; " +
                    $"if (-not $s) {{ Add-DnsClientDohServerAddress -ServerAddress '{DnsSecondary}' " +
                    $"-DohTemplate 'https://cloudflare-dns.com/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true }}");

                // 4. Add NRPT rules
                foreach (var domain in DiscordDomains)
                {
                    var d = domain;
                    RunPowerShell(
                        $"Add-DnsClientNrptRule -Namespace '{d}' " +
                        $"-NameServers @('{DnsPrimary}','{DnsSecondary}') " +
                        $"-Comment '{CommentTag}'");
                    Dispatcher.Invoke(() => Log($"  NRPT kurali eklendi: {d}"));
                }

                // 5. Clear DNS cache
                RunPowerShell("Clear-DnsClientCache");

                // 6. Refresh UI
                var rules = GetCurrentRules();
                var gdpiRunning = IsGoodbyeDpiRunning();
                Dispatcher.Invoke(() =>
                {
                    Log("DNS onbellegi temizlendi.");
                    if (gdpiRunning && rules.Count > 0)
                        Log($"Bypass ETKIN - GoodbyeDPI + {rules.Count} NRPT kurali aktif.");
                    else if (!gdpiRunning)
                        Log("UYARI: GoodbyeDPI baslatilamadi! DPI bypass calismayabilir.");
                    else
                        Log("UYARI: NRPT kurallari eklenemedi!");
                    SetStatus(rules.Count > 0, rules.Count, gdpiRunning);
                    UpdateRulesList(rules);
                    SetBusy(false);
                });
            })
            { IsBackground = true };
            thread.Start();
        }

        private void DisableBypass()
        {
            SetBusy(true);
            Log("Devre disi birakma baslatiliyor...");

            var thread = new Thread(() =>
            {
                // 1. Stop GoodbyeDPI
                StopGoodbyeDpi();

                // 2. Remove NRPT rules
                var existing = GetCurrentRules();
                if (existing.Count > 0)
                {
                    RunPowerShell(
                        $"Get-DnsClientNrptRule | Where-Object {{ $_.Comment -eq '{CommentTag}' }} | " +
                        "ForEach-Object { Remove-DnsClientNrptRule -Name $_.Name -Force }");
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var rule in existing)
                            Log($"  NRPT kurali kaldirildi: {rule.Domain}");
                    });
                }

                // 3. Clear DNS cache
                RunPowerShell("Clear-DnsClientCache");

                var rules = GetCurrentRules();
                Dispatcher.Invoke(() =>
                {
                    Log("DNS onbellegi temizlendi.");
                    Log("Bypass DEVRE DISI.");
                    SetStatus(false, 0, false);
                    UpdateRulesList(rules);
                    SetBusy(false);
                });
            })
            { IsBackground = true };
            thread.Start();
        }

        // --- Event Handlers ---

        private void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isActive)
                DisableBypass();
            else
                EnableBypass();
        }
    }

    public class NrptRule
    {
        public string Domain { get; set; } = string.Empty;
        public string Servers { get; set; } = string.Empty;
    }
}
