# Değişiklik Günlüğü

Bu projedeki tüm önemli değişiklikler bu dosyada belgelenir. Sürüm numaralandırması [Semantic Versioning](https://semver.org/lang/tr/) biçimini takip eder.

## [2.0.0] - 2026-07-29

Uygulamanın **MertClicker**'dan **m4 Auto Clicker**'a yeniden markalandığı ve arayüzün baştan sona gözden geçirildiği sürüm.

### Değiştirildi
- Uygulama adı `MertClicker` → **`m4 Auto Clicker`** olarak değiştirildi; tüm proje/derleme adları (`MertClicker.*` → `m4AutoClicker.*`) buna göre güncellendi.
- Tüm ekranlarda (Ana Sayfa, Auto Clicker, Makro Kaydedici, Makrolarım, Ayarlar, Kayıtlar, Hakkında) ortak bir tasarım diline geçildi: sayfa başlığı/gövde tipografisi, bölüm başlıkları, kart (`Card`) bileşenleri ve tutarlı boşluk/hizalama kuralları tek bir stil kaynağına (`Themes/Styles.xaml`, `Themes/Dimensions.xaml`) taşındı.
- Renk paleti (`Themes/Colors.xaml`) genişletildi ve tutarlı yüzey/kenarlık/metin tonlarına oturtuldu.
- Ayarlar ekranında kısayol atama biçimi, açılır listeden tuş seçmekten **tuşlara doğrudan basarak yakalamaya** dönüştürüldü (`HotkeySettingViewModel.ApplyCaptured`, `SettingsView.xaml.cs`).
- Kısayol değişikliği artık ayrı bir "Kaydet" adımı gerektirmeden, yakalama anında diske yazılıyor ve eski kısayol o anda bırakılıyor.
- Hakkında ekranı; özellik listesi, geliştirici ve depo bilgisi gibi ek verilerle zenginleştirildi (`AboutViewModel`), ekrandaki sürüm bilgisi sabit metin yerine derleme (assembly) sürümünden dinamik okunmaya devam ediyor.
- Uygulama artık kendi ikonuyla paketleniyor (`Assets/app.ico`, `Assets/logo.png`); v1.0.0'da özel bir uygulama ikonu yoktu.

### Eklendi
- **Kısayol çakışması koruması:** Ayarlar ekranından yeni bir kombinasyon işletim sistemi seviyesinde kaydedilemezse (ör. başka bir uygulama tarafından kullanılıyorsa), ilgili eylem için bir önceki çalışan kısayol otomatik olarak geri yükleniyor; kullanıcı hatalı bir değişiklik yüzünden bir eylemi kısayolsuz bırakmıyor (`HotkeyCoordinatorService.RegisterHotkeysAndPersist`).
- Sürüm bilgisi artık tek bir merkezi kaynaktan (`Directory.Build.props`) yönetiliyor; tüm projeler ve paketler aynı sürüm numarasını kullanıyor.

### Düzeltildi
- **Açılışta nadiren yaşanan kilitlenme (deadlock) giderildi:** Uygulama başlangıcında ve kapanışında ayarların/otomasyonların eşzamansız (async) olarak yüklenip durdurulması, WPF arayüz iş parçacığını engelleyen bir senkron bekleme (`GetAwaiter().GetResult()`) üzerinden yapılıyordu. Arayüz mesaj döngüsü henüz başlamadan bu bekleme bazı durumlarda kilitlenmeye yol açıyordu; ilgili çağrılar artık arka plan iş parçacığına (`Task.Run`) devrediliyor (`App.xaml.cs`).

## [1.0.0] - 2026-07-27

İlk kararlı sürüm, **MertClicker** adıyla yayınlandı.

### Eklendi
- Auto Clicker: sabit sayıda veya durdurulana kadar tıklama; sol/sağ/orta tuş, tekli/çiftli tıklama, sabit nokta veya güncel imleç konumu hedefi.
- Makro Kaydedici/Oynatıcı: fare hareketi, tıklama, tekerlek olayları ve klavye tuş basışlarının gerçek zamanlamasıyla kaydedilip oynatılması.
- Zaman telafili duraklat/devam et desteği (Auto Clicker ve makro oynatma için).
- Acil durdurma kısayolu: tüm aktif otomasyonları anında durdurma.
- Makro kütüphanesi: kaydedilen makroları listeleme, oynatma, açıklama/etiket ekleme, silme, `.json` olarak dışa/içe aktarma.
- Farklı ekran çözünürlüğünde kaydedilmiş bir makro içe aktarıldığında oynatmadan önce uyarı gösterme.
- Özelleştirilebilir global kısayollar (varsayılan F6–F9), Ayarlar ekranından açılır listeyle yeniden atanabilir.
- Log ekranı: uygulama içinden son işlemleri izleme; log dosyalarının 14 günde bir otomatik temizlenmesi.
- Sistem tepsisi desteği: arka planda çalışma, tepsi simgesinden Göster / Acil Durdur / Çıkış.
- 158 birim/entegrasyon testi ile katmanlı mimarinin (Domain/Application/Infrastructure/Platform.Windows) doğrulanması.

### Bilinen Sorunlar
- Ayarların diskten okunması sırasında oluşabilen bir zamanlama koşulu nedeniyle, uygulama bazen açılış penceresini göstermeden takılı kalabiliyordu. Bu sorun v2.0.0'da giderildi.
