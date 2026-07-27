<div align="center">

# 🖱️ MertClicker

**Windows için modern otomatik tıklama ve makro kayıt/oynatma uygulaması**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)](#)
[![WPF](https://img.shields.io/badge/UI-WPF-blueviolet)](#)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/tests-158%20passing-brightgreen)](#-test)

</div>

---

## Nedir bu?

MertClicker, tekrarlayan fare/klavye işlerini otomatikleştirmek için yazılmış hafif bir Windows masaüstü uygulaması. İki temel yeteneği var:

- **Auto Clicker** — belirlediğin aralıkta, belirlediğin noktada (veya güncel imleç konumunda) otomatik tıklama yapar.
- **Makro Kaydedici/Oynatıcı** — fare hareketlerini, tıklamaları, tekerlek olaylarını ve klavye tuş basışlarını gerçek zamanlamasıyla kaydeder; istediğin zaman aynı hızda veya farklı bir hızda tekrar oynatır.

Her şey global kısayollarla (varsayılan F6–F9, tamamen özelleştirilebilir) tek elden kontrol edilir; uygulama odakta olmasa bile çalışır.

## ✨ Özellikler

| | |
|---|---|
| 🎯 **Auto Clicker** | Sabit sayıda veya durdurana kadar tıklama, sol/sağ/orta tuş, tekli/çiftli tıklama, sabit nokta veya imleç konumu hedefi |
| 🎬 **Makro Kaydı** | Fare hareketi + tıklama + tekerlek **ve klavye tuş basışları** — gerçek zamanlamasıyla kaydedilir |
| ⏯️ **Duraklat / Devam Et** | Hem Auto Clicker hem makro oynatma sırasında, zaman telafili duraklatma (kaldığın yerden değil, doğru zamanlamayla devam eder) |
| ⌨️ **Özelleştirilebilir Kısayollar** | F6–F9 varsayılan; her biri istediğin tuş + Ctrl/Alt/Shift/Win kombinasyonuna yeniden atanabilir |
| 📂 **Makro Kütüphanesi** | Kaydedilen makroları listele, oynat, açıklama/etiket ekle, sil |
| 📤 **Dışa/İçe Aktar** | Makroları `.json` dosyası olarak paylaş, başka bir makineye aktar |
| 🖥️ **Ekran Uyuşmazlığı Uyarısı** | İçe aktarılan bir makro farklı ekran çözünürlüğünde kaydedilmişse oynatmadan önce uyarır |
| 🛑 **Acil Durdurma** | Tek kısayolla (varsayılan F9) tüm aktif otomasyonları anında durdurur |
| 📋 **Log Ekranı** | Uygulama içinden son işlemleri izle; log dosyaları 14 günde bir otomatik temizlenir |
| 🔔 **Sistem Tepsisi** | Arka planda çalışır, tepsi simgesinden Göster / Acil Durdur / Çıkış |

## 🚀 Başlarken

### Gereksinimler

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Derleme ve çalıştırma

```bash
git clone https://github.com/mer4t/mer4t-auto-clicker.git
cd mer4t-auto-clicker
dotnet build
dotnet run --project src/MertClicker.App
```

### Test

```bash
dotnet test
```

Uygulama, katmanların tamamını kapsayan **158 birim/entegrasyon testiyle** doğrulanmıştır.

## ⌨️ Varsayılan Kısayollar

| Kısayol | Eylem |
|---|---|
| `F6` | Auto Clicker'ı başlat/durdur |
| `F7` | Makro kaydını başlat/durdur |
| `F8` | Seçili makroyu oynat/durdur |
| `F9` | Acil durdurma (tüm otomasyonları durdurur) |

Tüm kısayollar **Ayarlar** ekranından yeniden atanabilir; değişiklikler uygulamayı yeniden başlatmadan hemen etkili olur.

## 🏗️ Mimari

Proje, bağımlılıkların tek yönde aktığı katmanlı bir mimariyle yazılmıştır:

```
MertClicker.Domain          → Saf modeller/enum'lar, dış bağımlılık yok
        ↑
MertClicker.Application     → İş mantığı, servisler, arayüzler (IInputInjector, IMacroRepository...)
        ↑
MertClicker.Infrastructure  → JSON tabanlı kalıcılık (makrolar, ayarlar, loglar)
MertClicker.Platform.Windows → Win32 P/Invoke (CsWin32): global kancalar, SendInput, RegisterHotKey
        ↑
MertClicker.App             → WPF arayüzü (MVVM, CommunityToolkit.Mvvm)
```

**Öne çıkan teknik detaylar:**
- `AutomationEngine`: Auto Clicker ve Makro Oynatıcı'nın paylaştığı tek bir oynatma motoru; duraklatma sırasında geçen gerçek süreyi telafi ederek zamanlama tutarlılığını korur.
- `WH_MOUSE_LL` / `WH_KEYBOARD_LL` global kancaları ile sistem genelinde girdi yakalama; kendi kısayol tuşlarının kayda karışmasını önleyen filtreleme mantığı.
- Atomik dosya yazımı (geçici dosya + taşıma) ve eşzamanlılık koruması ile güvenli JSON kalıcılığı.

## 🛠️ Teknoloji

- **.NET 10** / **C# 13**
- **WPF** + **CommunityToolkit.Mvvm** (MVVM)
- **CsWin32** (kaynak üretimli, güvenli Win32 P/Invoke)
- **xUnit** (test)
- **Microsoft.Extensions.Hosting/DependencyInjection**

## 🤝 Katkıda Bulunma

Pull request'ler ve issue'lar memnuniyetle karşılanır. Değişiklik yapmadan önce `dotnet test` ile tüm testlerin geçtiğinden emin olun.

## 📄 Lisans

Bu proje [MIT lisansı](LICENSE) ile lisanslanmıştır.

---

<div align="center">

⚠️ Bu araç yalnızca kendi bilgisayarınızda, kendi otomasyon ihtiyaçlarınız için kullanım amaçlıdır. Üçüncü taraf uygulamaların hizmet şartlarını ihlal edecek şekilde kullanılmasından kullanıcı sorumludur.

</div>
