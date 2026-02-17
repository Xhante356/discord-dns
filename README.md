# Discord Bypass

Windows 11 icin Discord engel asma araci. GoodbyeDPI (DPI bypass) + NRPT (DNS bypass).

## Nasil Calisir?

Iki katmanli engel asma:

1. **GoodbyeDPI**: ISP'nin DPI/SNI engellemesini TLS paket fragmentasyonu ile astirir
2. **NRPT**: Discord alan adlari icin DNS sorgularini Cloudflare'e yonlendirir (DoH)

- Sadece Discord etkilenir, diger trafik degismez
- Sistem genelinde calisir (tarayici + Discord uygulamasi)
- Pencereyi kapatinca arka planda calismaya devam eder (system tray)

## Kurulum

Repoyu indirin:

```
git clone https://github.com/Xhante356/discord-dns.git
```

## Kullanim

### GUI Uygulamas (Onerilen)

Masaustundeki **Discord DNS.bat** dosyasini cift tiklayarak GUI uygulamasini acin. Uygulama otomatik olarak admin yetkisi isteyecektir.

GUI ozellikleri:
- Discord temali karanlik arayuz (C# WPF)
- Tek tikla etkinlestir/devre disi birak (GoodbyeDPI + NRPT birlikte)
- Canli NRPT kural listesi ve GoodbyeDPI durumu
- System tray: pencereyi kapatinca arka planda calismaya devam eder
- Islem kaydi

### Masaustu Kisayollari (CLI)

- **Discord DNS Ac.bat** — Cift tiklayarak bypass'i etkinlestirir
- **Discord DNS Kapat.bat** — Cift tiklayarak bypass'i devre disi birakir

### PowerShell

```powershell
.\Discord-DNS.ps1 ac      # Bypass etkinlestir
.\Discord-DNS.ps1 kapat   # Bypass devre disi birak
.\Discord-DNS.ps1 durum   # Mevcut durumu goster
```

> Admin yetkisi gereklidir. Script otomatik olarak UAC yukseltme penceresi acar.

## Kapsanan Alan Adlari

| Alan Adi | Aciklama |
|----------|----------|
| `.discord.com` | Ana Discord alan adi |
| `.discord.gg` | Davet linkleri |
| `.discordapp.com` | Eski Discord alan adi |
| `.discord.media` | Medya icerikleri |
| `.discordapp.net` | CDN ve medya |
| `.discord.dev` | Gelistirici portali |

## Teknik Detaylar

- NRPT kurallari registry'de saklanir ve Comment alani `DiscordDNS-Bypass` ile isaretlenir
- DNS sunuculari: Cloudflare `1.1.1.1` / `1.0.0.1` (DoH otomatik)
- `ac` komutu mevcut kurallari kontrol eder, varsa once kaldirir ve yeniden olusturur
- `kapat` komutu sadece `DiscordDNS-Bypass` isaretli kurallari kaldirir
- Her islemden sonra DNS onbellegi otomatik temizlenir
