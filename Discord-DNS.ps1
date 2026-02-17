<#
.SYNOPSIS
    Discord DNS Bypass Araci - Windows 11 NRPT tabanli
.DESCRIPTION
    Discord alan adlari icin DNS sorgularini Cloudflare DoH uzerinden yonlendirir.
    Sadece Discord trafigi etkilenir, diger tum DNS sorguari normal devam eder.
.PARAMETER Islem
    ac     - Bypass'i etkinlestir
    kapat  - Bypass'i devre disi birak
    durum  - Mevcut durumu goster (varsayilan)
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("ac", "kapat", "durum")]
    [string]$Islem = "durum"
)

# --- Sabitler ---
$COMMENT_TAG   = "DiscordDNS-Bypass"
$DNS_PRIMARY   = "1.1.1.1"
$DNS_SECONDARY = "1.0.0.1"
$DISCORD_DOMAINS = @(
    ".discord.com"
    ".discord.gg"
    ".discordapp.com"
    ".discord.media"
    ".discordapp.net"
    ".discord.dev"
)

# --- Yardimci Fonksiyonlar ---

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-Baslik {
    param([string]$Metin)
    Write-Host ""
    Write-Host "  $Metin" -ForegroundColor Cyan
    Write-Host "  $('-' * $Metin.Length)" -ForegroundColor Cyan
}

function Write-Basari {
    param([string]$Metin)
    Write-Host "  [+] $Metin" -ForegroundColor Green
}

function Write-Hata {
    param([string]$Metin)
    Write-Host "  [!] $Metin" -ForegroundColor Red
}

function Write-Bilgi {
    param([string]$Metin)
    Write-Host "  [*] $Metin" -ForegroundColor Yellow
}

# --- Admin Kontrolu ---
if (-not (Test-Admin)) {
    Write-Bilgi "Admin yetkisi gerekiyor, UAC yukseltme isteniyor..."
    $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" $Islem"
    try {
        Start-Process powershell.exe -Verb RunAs -ArgumentList $argList
    }
    catch {
        Write-Hata "Admin yetkisi alinamadi. Islem iptal edildi."
        Read-Host "`n  Cikmak icin Enter'a basin"
    }
    exit
}

# --- DoH Sunucu Kaydi ---
function Ensure-DohServers {
    $dohServers = @(
        @{ Address = $DNS_PRIMARY;   Template = "https://cloudflare-dns.com/dns-query" }
        @{ Address = $DNS_SECONDARY; Template = "https://cloudflare-dns.com/dns-query" }
    )

    foreach ($server in $dohServers) {
        $existing = Get-DnsClientDohServerAddress -ServerAddress $server.Address -ErrorAction SilentlyContinue
        if (-not $existing) {
            Write-Bilgi "DoH sunucusu kaydediliyor: $($server.Address)"
            Add-DnsClientDohServerAddress -ServerAddress $server.Address `
                -DohTemplate $server.Template `
                -AllowFallbackToUdp $false `
                -AutoUpgrade $true
        }
    }
}

# --- Mevcut Kurallari Getir ---
function Get-DiscordNrptRules {
    return Get-DnsClientNrptRule | Where-Object { $_.Comment -eq $COMMENT_TAG }
}

# --- Etkinlestir ---
function Enable-DiscordBypass {
    Write-Baslik "Discord DNS Bypass - Etkinlestirme"

    # Mevcut kurallar var mi kontrol et
    $existing = Get-DiscordNrptRules
    if ($existing) {
        Write-Bilgi "Discord DNS bypass zaten etkin. Once kapatiliyor..."
        Disable-DiscordBypass -Quiet
    }

    # DoH sunucularinin kayitli oldugunu dogrula
    try {
        Ensure-DohServers
    }
    catch {
        Write-Hata "DoH sunuculari kaydedilemedi: $_"
        return
    }

    # NRPT kurallari olustur
    $created = 0
    foreach ($domain in $DISCORD_DOMAINS) {
        try {
            Add-DnsClientNrptRule `
                -Namespace $domain `
                -NameServers @($DNS_PRIMARY, $DNS_SECONDARY) `
                -Comment $COMMENT_TAG | Out-Null
            Write-Basari "Kural eklendi: $domain"
            $created++
        }
        catch {
            Write-Hata "Kural eklenemedi ($domain): $_"
        }
    }

    # DNS onbellegini temizle
    Clear-DnsClientCache
    Write-Bilgi "DNS onbellegi temizlendi."

    Write-Host ""
    if ($created -eq $DISCORD_DOMAINS.Count) {
        Write-Basari "Tum kurallar basariyla olusturuldu ($created/$($DISCORD_DOMAINS.Count))."
        Write-Basari "Discord DNS bypass ETKIN."
    }
    else {
        Write-Bilgi "$created/$($DISCORD_DOMAINS.Count) kural olusturuldu."
    }
}

# --- Devre Disi Birak ---
function Disable-DiscordBypass {
    param([switch]$Quiet)

    if (-not $Quiet) {
        Write-Baslik "Discord DNS Bypass - Devre Disi Birakma"
    }

    $rules = Get-DiscordNrptRules
    if (-not $rules) {
        if (-not $Quiet) {
            Write-Bilgi "Kaldirilacak kural bulunamadi. Bypass zaten devre disi."
        }
        return
    }

    $removed = 0
    foreach ($rule in $rules) {
        try {
            Remove-DnsClientNrptRule -Name $rule.Name -Force
            if (-not $Quiet) {
                Write-Basari "Kural kaldirildi: $($rule.Namespace)"
            }
            $removed++
        }
        catch {
            Write-Hata "Kural kaldirilamadi ($($rule.Namespace)): $_"
        }
    }

    # DNS onbellegini temizle
    Clear-DnsClientCache
    if (-not $Quiet) {
        Write-Bilgi "DNS onbellegi temizlendi."
        Write-Host ""
        Write-Basari "$removed kural kaldirildi. Discord DNS bypass DEVRE DISI."
    }
}

# --- Durum ---
function Show-Status {
    Write-Baslik "Discord DNS Bypass - Durum"

    $rules = Get-DiscordNrptRules

    if ($rules) {
        Write-Basari "Bypass ETKIN"
        Write-Host ""
        Write-Host "  Aktif NRPT Kurallari:" -ForegroundColor Cyan
        Write-Host ""
        $rules | ForEach-Object {
            $ns = ($_.NameServers -join ", ")
            Write-Host "    $($_.Namespace)" -ForegroundColor White -NoNewline
            Write-Host "  ->  $ns" -ForegroundColor Gray
        }
    }
    else {
        Write-Bilgi "Bypass DEVRE DISI - Aktif NRPT kurali yok."
    }

    # Ag adaptoru DNS bilgileri
    Write-Host ""
    Write-Host "  Aktif Ag Adaptoru DNS Bilgileri:" -ForegroundColor Cyan
    Write-Host ""
    Get-DnsClientServerAddress -AddressFamily IPv4 |
        Where-Object { $_.ServerAddresses.Count -gt 0 } |
        ForEach-Object {
            $addrs = $_.ServerAddresses -join ", "
            Write-Host "    $($_.InterfaceAlias)" -ForegroundColor White -NoNewline
            Write-Host "  ->  $addrs" -ForegroundColor Gray
        }
}

# --- Ana Akis ---
try {
    switch ($Islem) {
        "ac"    { Enable-DiscordBypass }
        "kapat" { Disable-DiscordBypass }
        "durum" { Show-Status }
    }
}
catch {
    Write-Hata "Beklenmeyen hata: $_"
}

Write-Host ""
Read-Host "  Cikmak icin Enter'a basin"
