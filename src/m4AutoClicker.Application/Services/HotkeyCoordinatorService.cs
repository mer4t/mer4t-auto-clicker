using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Hotkeys;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Services;

// F6-F9 global kısayollarını ilgili uygulama davranışlarına yönlendiren tek koordinatör.
public sealed class HotkeyCoordinatorService : IDisposable
{
    private readonly IHotkeyService _hotkeyService;
    private readonly AutoClickerService _autoClickerService;
    private readonly MacroRecorder _macroRecorder;
    private readonly IMacroPlayer _macroPlayer;
    private readonly IEmergencyStopService _emergencyStopService;
    private readonly IApplicationLogger _logger;
    private readonly Dictionary<string, HotkeyRegistrationResult> _registrationResults = new();
    private readonly Dictionary<string, HotkeyDefinition> _currentDefinitions = new();

    public HotkeyCoordinatorService(
        IHotkeyService hotkeyService,
        AutoClickerService autoClickerService,
        MacroRecorder macroRecorder,
        IMacroPlayer macroPlayer,
        IEmergencyStopService emergencyStopService,
        IApplicationLogger logger)
    {
        _hotkeyService = hotkeyService;
        _autoClickerService = autoClickerService;
        _macroRecorder = macroRecorder;
        _macroPlayer = macroPlayer;
        _emergencyStopService = emergencyStopService;
        _logger = logger;

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
    }

    // Auto Clicker ekranındaki mevcut ayarlardan bir ClickPlan üretir. ViewModel tarafından atanır.
    public Func<ClickPlan>? AutoClickerPlanProvider { get; set; }

    // F8 ile oynatılacak makroyu sağlar (Makrolarım ekranındaki seçili makro). Task döndürür çünkü
    // makro diskten okunabilir; senkron bir Func, çağıran UI thread'ini G/Ç ile bloke ederdi.
    public Func<Task<Macro?>>? MacroPlaybackSourceProvider { get; set; }

    public event EventHandler<AutoClickerHotkeyResultEventArgs>? AutoClickerToggled;

    public event EventHandler<MacroRecorderHotkeyResultEventArgs>? MacroRecorderToggled;

    public event EventHandler<MacroPlaybackHotkeyResultEventArgs>? MacroPlaybackToggled;

    // F9 sonuç mesajı gibi genel bildirimler için hafif bir kanal.
    public event EventHandler<string>? NotificationRaised;

    // Kayıt sonuçları değiştiğinde (ilk kayıt veya kullanıcı Ayarlar'dan kısayolları değiştirdiğinde)
    // tetiklenir; ViewModel'ler HotkeyStatusText gibi görünümleri buna göre tazeleyebilir.
    public event EventHandler? HotkeyRegistrationsChanged;

    public IReadOnlyDictionary<string, HotkeyRegistrationResult> RegisterDefaultHotkeys() =>
        RegisterHotkeys(HotkeyDefaults.All);

    public IReadOnlyDictionary<string, HotkeyRegistrationResult> RegisterHotkeys(IReadOnlyList<HotkeyDefinition> hotkeys)
    {
        foreach (var hotkey in hotkeys)
        {
            RegisterOne(hotkey);
        }

        HotkeyRegistrationsChanged?.Invoke(this, EventArgs.Empty);
        return _registrationResults;
    }

    // Verilen kısayolları önce (varsa) eski atamalarından kaldırıp yeniden kaydeder. Kullanıcı
    // Ayarlar ekranından kısayolları değiştirdiğinde uygulama yeniden başlatılmadan çağrılır.
    // Tüm kısayolların önce serbest bırakılması, iki kısayolun tuşlarını birbirleriyle
    // "değiştirme" gibi senaryolarda geçici bir çakışma yaşanmasını önler.
    public IReadOnlyDictionary<string, HotkeyRegistrationResult> ReassignHotkeys(IReadOnlyList<HotkeyDefinition> hotkeys)
    {
        // Yeni kombinasyon bir nedenle (ör. başka bir uygulamayla çakışma) kaydedilemezse,
        // kullanıcıyı o eylem için tamamen kısayolsuz bırakmamak adına önceki çalışan tanım
        // geri yüklenir. Bu yüzden değiştirmeden önce her kimlik için o anki (çalışan) tanım
        // saklanır.
        var previousDefinitions = hotkeys
            .Select(h => GetCurrentDefinition(h.Id))
            .Where(d => d is not null)
            .Cast<HotkeyDefinition>()
            .ToDictionary(d => d.Id);

        foreach (var hotkey in hotkeys)
        {
            _hotkeyService.Unregister(hotkey.Id);
            _registrationResults.Remove(hotkey.Id);
        }

        RegisterHotkeys(hotkeys);

        // Bu kaydetme isteğinin GERÇEKTE ne sonuç verdiğinin bir anlık görüntüsünü al.
        // _registrationResults, aşağıdaki geri yükleme sırasında güncellenmeye devam edeceği
        // için (aynı sözlük referansı), çağırana bu kopyayı döndürürüz: Ayarlar ekranı kullanıcının
        // isteğinin reddedildiğini ve hangi gerekçeyle reddedildiğini doğru şekilde gösterebilsin
        // (geri yükleme "başarılı" görünümü vererek bu bilgiyi gizlemesin).
        var attemptResults = new Dictionary<string, HotkeyRegistrationResult>(_registrationResults);

        var toRollBack = hotkeys
            .Where(h => !attemptResults[h.Id].Success
                && previousDefinitions.TryGetValue(h.Id, out var previous)
                && previous != h)
            .ToList();

        if (toRollBack.Count == 0)
        {
            return attemptResults;
        }

        foreach (var hotkey in toRollBack)
        {
            _hotkeyService.Unregister(hotkey.Id);
            _registrationResults.Remove(hotkey.Id);
        }

        foreach (var hotkey in toRollBack)
        {
            var previous = previousDefinitions[hotkey.Id];
            RegisterOne(previous);

            if (_registrationResults.TryGetValue(previous.Id, out var restoreResult) && restoreResult.Success)
            {
                _logger.LogWarning(
                    "'{0}' için yeni kısayol ({1}+{2}) kaydedilemedi; önceki çalışan kısayol ({3}+{4}) geri yüklendi.",
                    hotkey.Id, hotkey.Modifiers, hotkey.Key, previous.Modifiers, previous.Key);
            }
        }

        // _registrationResults (kalıcı, uygulama genelindeki "şu an aktif kısayol" durumu) artık
        // geri yüklenmiş tanımı yansıtıyor; diğer sayfalar (ör. Auto Clicker'ın kendi durum
        // metni) GetRegistrationResult üzerinden doğru ("Hazır") durumu görecek.
        HotkeyRegistrationsChanged?.Invoke(this, EventArgs.Empty);
        return attemptResults;
    }

    public HotkeyRegistrationResult? GetRegistrationResult(string hotkeyId) =>
        _registrationResults.GetValueOrDefault(hotkeyId);

    // Kayıt başarılı ya da başarısız olsun, o kimlik için EN SON denenen tanımı döndürür; UI bunu
    // durum metninde ("Ctrl+F1: Hazır" gibi) gerçek, güncel tuş kombinasyonunu göstermek için kullanır.
    public HotkeyDefinition? GetCurrentDefinition(string hotkeyId) =>
        _currentDefinitions.GetValueOrDefault(hotkeyId);

    private void RegisterOne(HotkeyDefinition hotkey)
    {
        _currentDefinitions[hotkey.Id] = hotkey;
        var result = _hotkeyService.Register(hotkey);
        _registrationResults[hotkey.Id] = result;

        if (result.Success)
        {
            _logger.LogInformation("Global kısayol kaydedildi: {0} ({1}+{2}).", hotkey.Id, hotkey.Modifiers, hotkey.Key);
        }
        else
        {
            _logger.LogWarning(
                "Global kısayol kaydedilemedi: {0} ({1}+{2}). Hata türü: {3}, mesaj: {4}",
                hotkey.Id, hotkey.Modifiers, hotkey.Key, result.ErrorType, result.ErrorMessage);
        }
    }

    private async void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        try
        {
            switch (e.HotkeyId)
            {
                case HotkeyIds.AutoClickerToggle:
                    _logger.LogInformation("Auto Clicker kısayolu tetiklendi.");
                    await ToggleAutoClickerAsync();
                    break;

                case HotkeyIds.MacroRecorderToggle:
                    _logger.LogInformation("Makro kaydı kısayolu tetiklendi.");
                    await ToggleMacroRecorderAsync();
                    break;

                case HotkeyIds.MacroPlaybackToggle:
                    _logger.LogInformation("Makro oynatma kısayolu tetiklendi.");
                    await ToggleMacroPlaybackAsync();
                    break;

                case HotkeyIds.EmergencyStop:
                    _logger.LogInformation("Acil durdurma kısayolu tetiklendi.");
                    await EmergencyStopAsync();
                    break;

                default:
                    _logger.LogWarning("Bilinmeyen hotkey kimliği tetiklendi: {0}", e.HotkeyId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotkey işlenirken beklenmeyen hata oluştu: {0}", e.HotkeyId);
        }
    }

    public async Task ToggleAutoClickerAsync()
    {
        if (_autoClickerService.State is PlaybackState.Idle or PlaybackState.Faulted)
        {
            var planProvider = AutoClickerPlanProvider;
            if (planProvider is null)
            {
                _logger.LogWarning("Kısayol tetiklendi ancak Auto Clicker ayarları henüz hazır değil.");
                AutoClickerToggled?.Invoke(
                    this, new AutoClickerHotkeyResultEventArgs { IsRunning = false, StatusMessage = "Auto Clicker ayarları hazır değil." });
                return;
            }

            ClickPlan plan;
            try
            {
                plan = planProvider();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kısayol için tıklama planı oluşturulamadı.");
                AutoClickerToggled?.Invoke(
                    this, new AutoClickerHotkeyResultEventArgs { IsRunning = false, StatusMessage = $"Hata: {ex.Message}" });
                return;
            }

            AutoClickerToggled?.Invoke(this, new AutoClickerHotkeyResultEventArgs { IsRunning = true, StatusMessage = "Çalışıyor..." });

            var result = await _autoClickerService.StartAsync(plan, CancellationToken.None);

            var message = result.Success
                ? $"Tamamlandı. {result.ExecutedActionCount} eylem çalıştırıldı."
                : $"Hata: {result.ErrorMessage}";

            AutoClickerToggled?.Invoke(this, new AutoClickerHotkeyResultEventArgs { IsRunning = false, StatusMessage = message });
        }
        else
        {
            await _autoClickerService.StopAsync();
            _logger.LogInformation("Kısayol ile Auto Clicker durduruldu.");
        }
    }

    // MacroRecorderToggle kısayoluna atanmış ana tuş ve (varsa) değiştirici tuşların ham VK
    // kodları; kayıt bu kısayolla durdurulduğunda MacroRecorder'a "bunları makrodan çıkar" demek
    // için kullanılır (bkz. MacroRecorder.StopAsync).
    private IReadOnlyCollection<ushort> GetMacroRecorderToggleKeyCodes() =>
        GetKeyCodesFor(HotkeyIds.MacroRecorderToggle);

    // Kayıt sırasında sürekli hariç tutulması gereken DİĞER üç kısayolun (MacroRecorderToggle
    // hariç) ham VK kodları; bunlar kaydın herhangi bir anında yanlışlıkla basılırsa makro
    // içeriğine karışmasın diye MacroRecorder.StartAsync'e geçirilir. Hem kısayolla hem de
    // Makro Kaydedici ekranındaki butonla başlatılan kayıtlarda kullanılabilmesi için public.
    public IReadOnlyCollection<ushort> GetOtherHotkeyKeyCodesForRecording()
    {
        var codes = new List<ushort>();
        codes.AddRange(GetKeyCodesFor(HotkeyIds.AutoClickerToggle));
        codes.AddRange(GetKeyCodesFor(HotkeyIds.MacroPlaybackToggle));
        codes.AddRange(GetKeyCodesFor(HotkeyIds.EmergencyStop));
        return codes;
    }

    private IReadOnlyCollection<ushort> GetKeyCodesFor(string hotkeyId)
    {
        var definition = GetCurrentDefinition(hotkeyId);
        if (definition is null)
        {
            return [];
        }

        var codes = new List<ushort> { (ushort)definition.Key };
        codes.AddRange(HotkeyModifierVirtualKeys.For(definition.Modifiers));
        return codes;
    }

    public async Task ToggleMacroRecorderAsync()
    {
        if (_macroRecorder.State is RecordingState.Idle or RecordingState.Faulted)
        {
            try
            {
                await _macroRecorder.StartAsync(CancellationToken.None, GetOtherHotkeyKeyCodesForRecording());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kısayol ile makro kaydı başlatılamadı.");
                MacroRecorderToggled?.Invoke(
                    this, new MacroRecorderHotkeyResultEventArgs { IsRecording = false, StatusMessage = $"Hata: {ex.Message}" });
                return;
            }

            MacroRecorderToggled?.Invoke(
                this, new MacroRecorderHotkeyResultEventArgs { IsRecording = true, StatusMessage = "Kaydediliyor..." });
        }
        else if (_macroRecorder.State == RecordingState.Recording)
        {
            try
            {
                var macro = await _macroRecorder.StopAsync(GetMacroRecorderToggleKeyCodes());
                MacroRecorderToggled?.Invoke(
                    this,
                    new MacroRecorderHotkeyResultEventArgs
                    {
                        IsRecording = false,
                        StatusMessage = $"Kayıt tamamlandı. {macro.Actions.Count} eylem.",
                        RecordedMacro = macro
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kısayol ile makro kaydı durdurulamadı.");
                MacroRecorderToggled?.Invoke(
                    this, new MacroRecorderHotkeyResultEventArgs { IsRecording = false, StatusMessage = $"Hata: {ex.Message}" });
            }
        }
        // Stopping durumunda ikinci bir F7 basışı yok sayılır; zaten devam eden bir durdurma işlemi var.
    }

    public async Task ToggleMacroPlaybackAsync()
    {
        if (_macroPlayer.State is PlaybackState.Idle or PlaybackState.Faulted)
        {
            var sourceProvider = MacroPlaybackSourceProvider;
            var macro = sourceProvider is null ? null : await sourceProvider();
            if (macro is null)
            {
                _logger.LogWarning("Kısayol tetiklendi ancak oynatılacak bir makro yok.");
                MacroPlaybackToggled?.Invoke(
                    this, new MacroPlaybackHotkeyResultEventArgs { IsPlaying = false, StatusMessage = "Oynatılacak bir makro yok. Önce bir kayıt yapın." });
                return;
            }

            MacroPlaybackToggled?.Invoke(this, new MacroPlaybackHotkeyResultEventArgs { IsPlaying = true, StatusMessage = "Oynatılıyor..." });

            var options = new PlaybackOptions { SpeedMultiplier = 1.0, RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };
            var result = await _macroPlayer.PlayAsync(macro, options, CancellationToken.None);

            var message = result.Success
                ? $"Tamamlandı. {result.ExecutedActionCount} eylem çalıştırıldı."
                : $"Hata: {result.ErrorMessage}";

            if (result.DisplayMismatchWarning is not null)
            {
                message = $"Uyarı: Ekran yapılandırması kayıttan farklı ({result.DisplayMismatchWarning}) {message}";
            }

            MacroPlaybackToggled?.Invoke(this, new MacroPlaybackHotkeyResultEventArgs { IsPlaying = false, StatusMessage = message });
        }
        else if (_macroPlayer.State == PlaybackState.Playing)
        {
            await _macroPlayer.StopAsync();
            _logger.LogInformation("Kısayol ile makro oynatma durduruldu.");
        }
    }

    public async Task EmergencyStopAsync()
    {
        await _emergencyStopService.StopAllAsync();
        NotificationRaised?.Invoke(this, "Acil durdurma uygulandı. Aktif otomasyonlar durduruldu.");
    }

    public void Dispose()
    {
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
    }
}
