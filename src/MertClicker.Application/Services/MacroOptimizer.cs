using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;
using MertClicker.Domain.Macros;

namespace MertClicker.Application.Services;

// WH_MOUSE_LL, imleç her piksel hareket ettiğinde bir olay üretebildiği için ham kayıtlar çok sayıda
// MouseMoveAction içerir. Bu servis, art arda gelen hareketleri zaman/mesafe eşiğine göre seyreltir;
// ancak bir tıklama veya tekerlek olayından hemen önceki hareketi (imlecin gerçek son konumu) ve
// kaydın en son hareketini her zaman korur.
public sealed class MacroOptimizer
{
    private readonly IHighResolutionClock _clock;
    private readonly IApplicationSettingsProvider _settingsProvider;

    public MacroOptimizer(IHighResolutionClock clock, IApplicationSettingsProvider settingsProvider)
    {
        _clock = clock;
        _settingsProvider = settingsProvider;
    }

    public Macro Optimize(Macro macro)
    {
        // Her çağrıda güncel ayarlar okunur; kullanıcı Ayarlar ekranından örnekleme eşiklerini
        // değiştirirse uygulamayı yeniden başlatmaya gerek kalmadan bir sonraki kayıtta etkili olur.
        var samplingSettings = _settingsProvider.Current.MouseMovementSampling;
        var minimumIntervalTicks = (long)(samplingSettings.MinimumIntervalMilliseconds / 1000.0 * _clock.Frequency);
        var minimumDistancePixels = (double)samplingSettings.MinimumDistancePixels;

        var result = new List<MacroAction>(macro.Actions.Count);
        MouseMoveAction? pendingMove = null;
        MouseMoveAction? lastKeptMove = null;

        foreach (var action in macro.Actions)
        {
            if (action is MouseMoveAction move)
            {
                pendingMove = move;

                var shouldKeep = lastKeptMove is null
                    || move.OffsetTicks - lastKeptMove.OffsetTicks >= minimumIntervalTicks
                    || Distance(move, lastKeptMove) >= minimumDistancePixels;

                if (shouldKeep)
                {
                    result.Add(move);
                    lastKeptMove = move;
                    pendingMove = null;
                }

                continue;
            }

            // Tıklama/tekerlek gibi bir olaydan hemen önce süzülüp atlanmış bir hareket varsa,
            // imlecin gerçek konumunu korumak için şimdi eklenir.
            if (pendingMove is not null)
            {
                result.Add(pendingMove);
                lastKeptMove = pendingMove;
                pendingMove = null;
            }

            result.Add(action);
        }

        // Kayıt bir hareketle bitiyor ve o hareket henüz eklenmediyse, son imleç konumunu korumak
        // için yine de eklenir.
        if (pendingMove is not null)
        {
            result.Add(pendingMove);
        }

        return macro with { Actions = result };
    }

    private static double Distance(MouseMoveAction a, MouseMoveAction b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * (double)dx) + (dy * (double)dy));
    }
}
