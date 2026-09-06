
# 🚀 Swift Launcher

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)

Swift Launcher, C# ile geliştirilmiş, hafif, komut satırı arayüzüne (CLI) sahip bir Minecraft başlatıcıdır. Mojang'ın resmi sunucularından oyun dosyalarını indirir, oyunun gereksinim duyduğu Java sürümünü otomatik olarak tespit edip indirir ve oyunu doğrudan konsol üzerinden başlatıp logları ekrana yazar.

## ✨ Özellikler

- **Tam Otomatik Kurulum:** Sadece sürüm numarasını girin; istemci (client), kütüphaneler (libraries) ve varlıklar (assets) otomatik olarak indirilsin.
- **Akıllı Java Yönetimi:** Seçtiğiniz Minecraft sürümüne göre (örneğin 1.16 ve altı için Java 8, 1.17 için Java 16, 1.20.5+ için Java 21) gerekli Java Runtime Environment (JRE) sürümünü [Adoptium](https://adoptium.net/) üzerinden indirir ve kurar.
- **Sürüm Yönetimi:** Mojang'ın manifestosundan tüm piyasaya sürülmüş sürümleri listeleyebilir, indirebilir ve bilgisayarınızdaki indirilmiş sürümler arasında anlık olarak geçiş yapabilirsiniz.
- **İlerleme Çubuğu (Progress Bar):** Dosya indirmeleri sırasında konsol ekranında yüzdelik ve görsel ilerleme çubuğu gösterir.
- **Konsol Logları:** Oyun başlatıldığında tüm oyun logları (ve hatalar) doğrudan launcher konsoluna kırmızı/beyaz renklerle akıtılır.
- **Kalıcı Ayarlar:** Kullanıcı adı, RAM limiti ve seçilen sürüm `.minecraft/settings.json` dosyasına kaydedilir, her açılışta otomatik yüklenir.

## 🛠️ Gereksinimler

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) veya daha yeni bir sürüm
- Windows, macOS veya Linux işletim sistemi
- İnternet bağlantısı

## 🚀 Kurulum ve Çalıştırma

1. Bu projeyi bilgisayarınıza klonlayın:
   ```bash
   git clone https://github.com/kullaniciadi/SwiftLauncher.git
   cd SwiftLauncher
   ```
2. Projeyi derleyin:
   ```bash
   dotnet build -c Release
   ```
3. Uygulamayı çalıştırın:
   ```bash
   dotnet run
   ```
   *Alternatif olarak, derleme sonrasında `bin/Release/net8.0/` (veya kullandığınız .NET sürümüne göre) klasöründeki `SwiftLauncher.exe` dosyasına çift tıklayarak çalıştırabilirsiniz.*

## 🎮 Kullanım

İlk açılışta launcher sizden bir Kullanıcı Adı, RAM miktarı (MB cinsinden) ve indirmek istediğiniz Minecraft sürümünü isteyecektir. 

**Ana Menü:**
```text
Swift Launcher | Version 1.0.0

1. Start Game
2. Change Settings
3. Quit Launcher
```

- **Start Game:** Seçili sürümü (eğer indirilmemişse indirerek) başlatır. Oyunun logları ekranda görünür. Oyun kapanana kadar bekler.
- **Change Settings:** Kullanıcı adı, RAM, sürüm değiştirme, yeni sürüm indirme ve zorla yeniden indirme (force redownload) seçeneklerinin bulunduğu alt menüyü açar.
- **Quit Launcher:** Uygulamayı kapatır.

## 📂 Proje Yapısı

Proje, sorumlulukların ayrılması prensibine (Separation of Concerns) göre tasarlanmıştır:

- `Program.cs`: Uygulamanın giriş noktası. Menü yönetimi, ayarların kaydedilmesi/yüklenmesi ve ilerleme çubuğu çizimini içerir.
- `MinecraftVersion.cs`: Mojang'ın API'sine istek atarak tüm mevcut Minecraft sürümlerini çeker.
- `DownloadMinecraft.cs`: Seçilen sürüme ait `version.json`, `client.jar`, `assets` ve `libraries` dosyalarını indirir.
- `DownloadJava.cs`: Minecraft sürümüne göre gerekli Java sürümünü hesaplar ve Adoptium API'sinden ZIP olarak indirip `.minecraft/runtime` içine çıkarır.
- `StartMinecraft.cs`: İndirilen kütüphaneleri ve `natives` (yerel kütüphaneler) dosyalarını birleştirip classpath oluşturur, gerekli JVM argümanlarını ayarlar ve oyunu `Process` olarak başlatır.

## ⚠️ Sorumluluk Reddi (Disclaimer)

Bu proje resmi Mojang veya Microsoft ürünü değildir ve onlarla hiçbir bağlantısı yoktur. Eğitim amaçlı açık kaynaklı bir yazılımdır. Oyunu oynamak için yine de resmi bir Minecraft hesabınızın olması beklenir (bu launcher "Offline" modda çalışır ve kimlik doğrulaması yapmaz). Telif hakları Mojang Studios'a aittir.

## 📄 Lisans

Bu proje MIT Lisansı ile lisanslanmıştır. Daha fazla bilgi için `LICENSE` dosyasına bakın.
```
