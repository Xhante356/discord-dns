using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;

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

        private readonly SolidColorBrush _green = new(Color.FromRgb(0x57, 0xF2, 0x87));
        private readonly SolidColorBrush _red = new(Color.FromRgb(0xED, 0x42, 0x45));
        private readonly SolidColorBrush _yellow = new(Color.FromRgb(0xFE, 0xE7, 0x5C));

        public MainWindow()
        {
            InitializeComponent();
            Log("Uygulama baslatildi.");
            RefreshStatus();
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
            if (active)
            {
                StatusDot.Fill = _green;
                StatusText.Text = "Durum: ETKIN";
                StatusDetail.Text = $"{ruleCount} NRPT kurali aktif";
            }
            else
            {
                StatusDot.Fill = _red;
                StatusText.Text = "Durum: DEVRE DISI";
                StatusDetail.Text = "Aktif NRPT kurali yok";
            }
        }

        private void SetBusy(bool busy)
        {
            BtnEnable.IsEnabled = !busy;
            BtnDisable.IsEnabled = !busy;
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

        private static string RunPowerShell(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
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
            var output = RunPowerShell(
                $"Get-DnsClientNrptRule | Where-Object {{ $_.Comment -eq '{CommentTag}' }} | " +
                "ForEach-Object { \\\"$($_.Namespace)|$($_.NameServers -join ',')\\\" }");

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
                var errors = new List<string>();

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
                    "-DohTemplate 'https://cloudflare-dns.com/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true }");
                RunPowerShell(
                    $"$s = Get-DnsClientDohServerAddress -ServerAddress '{DnsSecondary}' -ErrorAction SilentlyContinue; " +
                    $"if (-not $s) {{ Add-DnsClientDohServerAddress -ServerAddress '{DnsSecondary}' " +
                    "-DohTemplate 'https://cloudflare-dns.com/dns-query' -AllowFallbackToUdp $false -AutoUpgrade $true }");

                // Add NRPT rules
                int created = 0;
                foreach (var domain in DiscordDomains)
                {
                    try
                    {
                        RunPowerShell(
                            $"Add-DnsClientNrptRule -Namespace '{domain}' " +
                            $"-NameServers @('{DnsPrimary}','{DnsSecondary}') " +
                            $"-Comment '{CommentTag}'");
                        created++;
                        var d = domain;
                        Dispatcher.Invoke(() => Log($"  Kural eklendi: {d}"));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{domain}: {ex.Message}");
                    }
                }

                // Clear DNS cache
                RunPowerShell("Clear-DnsClientCache");

                // Refresh UI
                var rules = GetCurrentRules();
                Dispatcher.Invoke(() =>
                {
                    Log("DNS onbellegi temizlendi.");
                    if (errors.Count > 0)
                    {
                        foreach (var err in errors)
                            Log($"  HATA: {err}");
                    }
                    Log(rules.Count > 0
                        ? $"Bypass ETKIN - {rules.Count} kural aktif."
                        : "Bypass etkinlestirilemedi!");
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

                int removed = 0;
                foreach (var rule in existing)
                {
                    var domain = rule.Domain;
                    RunPowerShell(
                        $"Get-DnsClientNrptRule | Where-Object {{ $_.Comment -eq '{CommentTag}' -and $_.Namespace -eq '{domain}' }} | " +
                        "ForEach-Object { Remove-DnsClientNrptRule -Name $_.Name -Force }");
                    removed++;
                    Dispatcher.Invoke(() => Log($"  Kural kaldirildi: {domain}"));
                }

                RunPowerShell("Clear-DnsClientCache");

                var rules = GetCurrentRules();
                Dispatcher.Invoke(() =>
                {
                    Log("DNS onbellegi temizlendi.");
                    Log($"{removed} kural kaldirildi. Bypass DEVRE DISI.");
                    SetStatus(rules.Count > 0, rules.Count);
                    UpdateRulesList(rules);
                    SetBusy(false);
                });
            })
            { IsBackground = true };
            thread.Start();
        }

        // --- Event Handlers ---

        private void BtnEnable_Click(object sender, RoutedEventArgs e) => EnableBypass();
        private void BtnDisable_Click(object sender, RoutedEventArgs e) => DisableBypass();
    }

    public class NrptRule
    {
        public string Domain { get; set; } = string.Empty;
        public string Servers { get; set; } = string.Empty;
    }
}
