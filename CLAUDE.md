# Discord DNS Bypass

Windows 11 NRPT tabanli Discord DNS bypass araci. PowerShell + C# WPF GUI.

## Dosyalar
- Discord-DNS.ps1 — Ana PowerShell scripti (NRPT kural yonetimi, CLI)
- Discord DNS Ac.bat — Masaustu kisayolu (etkinlestir, CLI)
- Discord DNS Kapat.bat — Masaustu kisayolu (devre disi birak, CLI)
- gui/DiscordDnsApp/ — C# WPF GUI uygulamasi (.NET 6)
- ~/Desktop/Discord DNS.bat — GUI launcher (masaustu)

## GUI
- C# WPF, .NET 6, Discord temali karanlik arayuz
- PowerShell subprocess ile NRPT islemleri
- Admin yetkisi app.manifest ile istenir (UAC)
- Build: `dotnet publish -c Release` (gui/DiscordDnsApp/ icinde)

## CLI Komutlar
- `.\Discord-DNS.ps1 ac` — Bypass etkinlestir
- `.\Discord-DNS.ps1 kapat` — Bypass devre disi birak
- `.\Discord-DNS.ps1 durum` — Durum kontrol

## Teknik Detaylar
- NRPT kurallari Comment alani "DiscordDNS-Bypass" ile isaretlenir
- DNS: Cloudflare 1.1.1.1 / 1.0.0.1 (DoH otomatik)
- Alan adlari: .discord.com, .discord.gg, .discordapp.com, .discord.media, .discordapp.net, .discord.dev
- Admin yetkisi gereklidir (otomatik UAC yukseltme mevcut)
