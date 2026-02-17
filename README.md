# Discord DNS Bypass

Windows 11 NRPT (Name Resolution Policy Table) tabanli Discord DNS bypass araci.

Sadece Discord alan adlari icin DNS sorgularini Cloudflare'e (1.1.1.1 / 1.0.0.1) yonlendirir. Diger tum trafik normal DNS uzerinden devam eder.

## Nasil Calisir?

Windows 11'in yerlesik NRPT ozelligini kullanarak Discord alan adlari icin ozel DNS kurallari olusturur. Cloudflare, Windows 11'in bilinen DoH (DNS over HTTPS) sunucu listesinde oldugu icin sorgular otomatik olarak sifrelenir.

- Ek yazilim gerektirmez (Windows 11 yerlesik)
- Sadece Discord etkilenir, diger trafik degismez
- Sistem genelinde calisir (tarayici + Discord uygulamasi + her sey)
- Yeniden baslatma sonrasi da gecerli kalir

## Kurulum

Repoyu indirin:

```
git clone https://github.com/Xhante356/discord-dns.git
```

## Kullanim

### Masaustu Kisayollari

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
