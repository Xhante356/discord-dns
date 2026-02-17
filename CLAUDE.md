# Discord DNS Bypass

Windows 11 NRPT tabanli Discord DNS bypass araci. PowerShell + Batch.

## Dosyalar
- Discord-DNS.ps1 — Ana PowerShell scripti (NRPT kural yonetimi)
- Discord DNS Ac.bat — Masaustu kisayolu (etkinlestir)
- Discord DNS Kapat.bat — Masaustu kisayolu (devre disi birak)

## Komutlar
- `.\Discord-DNS.ps1 ac` — Bypass etkinlestir
- `.\Discord-DNS.ps1 kapat` — Bypass devre disi birak
- `.\Discord-DNS.ps1 durum` — Durum kontrol

## Teknik Detaylar
- NRPT kurallari Comment alani "DiscordDNS-Bypass" ile isaretlenir
- DNS: Cloudflare 1.1.1.1 / 1.0.0.1 (DoH otomatik)
- Alan adlari: .discord.com, .discord.gg, .discordapp.com, .discord.media, .discordapp.net, .discord.dev
- Admin yetkisi gereklidir (otomatik UAC yukseltme mevcut)
