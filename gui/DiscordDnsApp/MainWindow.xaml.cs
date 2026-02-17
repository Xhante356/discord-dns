using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        private WinForms.NotifyIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();
            Log("Uygulama baslatildi.");
            RefreshStatus();
        }

        // --- System Tray ---

        private void SetupTrayIcon()
        {
            _trayIcon = new WinForms.NotifyIcon();

            // Try to use Discord icon, fallback to system icon
            var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "discord.ico");
            if (!File.Exists(icoPath))
            {
                var discordIco = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Discord", "app.ico");
                if (File.Exists(discordIco))
                    icoPath = discordIco;
            }

            if (File.Exists(icoPath))
                _trayIcon.Icon = new Icon(icoPath);
            else
                _trayIcon.Icon = SystemIcons.Application;

            _trayIcon.Text = "Discord DNS Bypass";
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
            // Minimize to tray instead of closing
            e.Cancel = true;
            Hide();
            _trayIcon?.ShowBalloonTip(
                2000,
                "Discord DNS Bypass",
                "Uygulama arka planda calismaya devam ediyor.",
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

        private void SetStatus(bool active, int ruleCount)
        {
            _isActive = active;
            if (active)
            {
                StatusDot.Fill = _green;
                StatusText.Text = "Durum: ETKIN";
                StatusDetail.Text = $"{ruleCount} NRPT kurali aktif";
                BtnToggle.Content = "Devre Disi Birak";
                BtnToggle.Background = _red;
            }
            else
            {
                StatusDot.Fill = _red;
                StatusText.Text = "Durum: DEVRE DISI";
                StatusDetail.Text = "Aktif NRPT kurali yok";
                BtnToggle.Content = "Etkinlestir";
                BtnToggle.Background = _blurple;
            }

            // Update tray tooltip
            if (_trayIcon != null)
                _trayIcon.Text = active ? "Discord DNS Bypass - ETKIN" : "Discord DNS Bypass - DEVRE DISI";
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

        // --- PowerShell Execution (EncodedCommand to avoid escaping issues) ---

        private static string RunPowerShell(string script)
        {
            // Encode as Base64 UTF-16LE for -EncodedCommand
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
            var error = process.StandardError.ReadToEnd();
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
                Dispatcher.Invoke(() =>
                {
                    SetStatus(rules.Count > 0, rules.Count);
                    UpdateRulesList(rules);
                    Log(rules.Count > 0
                        ? $"Durum kontrol: {rules.Count} kural aktif."
                        : "Durum kontrol: Bypass devre disi.");
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
                // Remove existing rules first
                var existing = GetCurrentRules();
                if (existing.Count > 0)
                {
                    Dispatcher.Invoke(() => Log("Mevcut kurallar kaldiriliyor..."));
                    RunPowerShell(
                        $"Get-DnsClientNrptRule | Where-Object {{ $_.Comment -eq '{CommentTag}' }} | " +
                        "ForEach-Object { Remove-DnsClientNrptRule -Name $_.Name -Force }");
                }

                // Ensure DoH servers are registered
                Dispatcher.Invoke(() => Log("DoH sunuculari kontrol ediliyor..."));
                RunPowerShell(
                    $"$s = Get-DnsClientDohServerAddress -ServerAddress '{DnsPrimary}' -ErrorAction SilentlyContinue; " +
                    $"if (-not $s) {{ Add-DnsClientDohServerAddress -ServerAddress '{DnsPrimary}' " +
                    $"-DohTemplate 'https://cloudflare-dns.com/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true }}");
                RunPowerShell(
                    $"$s = Get-DnsClientDohServerAddress -ServerAddress '{DnsSecondary}' -ErrorAction SilentlyContinue; " +
                    $"if (-not $s) {{ Add-DnsClientDohServerAddress -ServerAddress '{DnsSecondary}' " +
                    $"-DohTemplate 'https://cloudflare-dns.com/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true }}");

                // Add NRPT rules
                int created = 0;
                foreach (var domain in DiscordDomains)
                {
                    var d = domain;
                    RunPowerShell(
                        $"Add-DnsClientNrptRule -Namespace '{d}' " +
                        $"-NameServers @('{DnsPrimary}','{DnsSecondary}') " +
                        $"-Comment '{CommentTag}'");
                    created++;
                    Dispatcher.Invoke(() => Log($"  Kural eklendi: {d}"));
                }

                // Clear DNS cache
                RunPowerShell("Clear-DnsClientCache");

                // Refresh UI
                var rules = GetCurrentRules();
                Dispatcher.Invoke(() =>
                {
                    Log("DNS onbellegi temizlendi.");
                    Log(rules.Count > 0
                        ? $"Bypass ETKIN - {rules.Count} kural aktif."
                        : "Bypass etkinlestirilemedi! PowerShell'i admin olarak calistirdiginizdan emin olun.");
                    SetStatus(rules.Count > 0, rules.Count);
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
                var existing = GetCurrentRules();
                if (existing.Count == 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        Log("Kaldirilacak kural yok. Bypass zaten devre disi.");
                        SetStatus(false, 0);
                        SetBusy(false);
                    });
                    return;
                }

                // Remove all rules in one command
                RunPowerShell(
                    $"Get-DnsClientNrptRule | Where-Object {{ $_.Comment -eq '{CommentTag}' }} | " +
                    "ForEach-Object { Remove-DnsClientNrptRule -Name $_.Name -Force }");

                Dispatcher.Invoke(() =>
                {
                    foreach (var rule in existing)
                        Log($"  Kural kaldirildi: {rule.Domain}");
                });

                RunPowerShell("Clear-DnsClientCache");

                var rules = GetCurrentRules();
                Dispatcher.Invoke(() =>
                {
                    Log("DNS onbellegi temizlendi.");
                    Log($"{existing.Count} kural kaldirildi. Bypass DEVRE DISI.");
                    SetStatus(rules.Count > 0, rules.Count);
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
