# Discord Bypass

Windows 11 Discord engel asma araci. GoodbyeDPI (DPI bypass) + NRPT (DNS bypass) + C# WPF GUI.

## Dosyalar
- Discord-DNS.ps1 — Ana PowerShell scripti (NRPT kural yonetimi, CLI)
- Discord DNS Ac.bat — Masaustu kisayolu (etkinlestir, CLI)
- Discord DNS Kapat.bat — Masaustu kisayolu (devre disi birak, CLI)
- gui/DiscordDnsApp/ — C# WPF GUI uygulamasi (.NET 6)
- goodbyedpi/ — GoodbyeDPI Turkey edition (x86_64, .gitignore'da)
- ~/Desktop/Discord DNS.bat — GUI launcher (masaustu)

## GUI
- C# WPF, .NET 6, Discord temali karanlik arayuz
- GoodbyeDPI process yonetimi (baslatma/durdurma)
- PowerShell subprocess ile NRPT islemleri
- Tek toggle buton: Etkinlestir / Devre Disi Birak
- System tray: pencere kapatilinca arka planda calisir
- Admin yetkisi app.manifest ile istenir (UAC)
- Build: `dotnet publish -c Release` (gui/DiscordDnsApp/ icinde)

## Nasil Calisir
1. GoodbyeDPI: TLS paketlerini fragmente ederek ISP DPI/SNI engellemesini astirir
2. NRPT: Discord alan adlari icin DNS sorgularini Cloudflare'e yonlendirir

## CLI Komutlar
- `.\Discord-DNS.ps1 ac` — Bypass etkinlestir (sadece DNS)
- `.\Discord-DNS.ps1 kapat` — Bypass devre disi birak
- `.\Discord-DNS.ps1 durum` — Durum kontrol

## Teknik Detaylar
- GoodbyeDPI args: `-5 --set-ttl 5 --dns-addr 77.88.8.8 --dns-port 1253`
- NRPT kurallari Comment alani "DiscordDNS-Bypass" ile isaretlenir
- DNS: Cloudflare 1.1.1.1 / 1.0.0.1 (DoH otomatik)
- Alan adlari: .discord.com, .discord.gg, .discordapp.com, .discord.media, .discordapp.net, .discord.dev
- Admin yetkisi gereklidir (GoodbyeDPI + NRPT icin)
