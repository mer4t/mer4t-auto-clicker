using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Services;

public sealed class MacroValidator
{
    public MacroValidationResult Validate(Macro macro)
    {
        ArgumentNullException.ThrowIfNull(macro);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(macro.Name))
        {
            errors.Add("Makro adı boş olamaz.");
        }

        if (macro.SchemaVersion <= 0)
        {
            errors.Add("Makro şema sürümü geçersiz.");
        }

        if (macro.Actions.Count == 0)
        {
            errors.Add("Makro en az bir eylem içermelidir.");
        }

        for (var i = 0; i < macro.Actions.Count; i++)
        {
            var action = macro.Actions[i];

            if (action.OffsetTicks < 0)
            {
                errors.Add($"{i}. eylemin zaman ofseti negatif olamaz.");
            }

            if (action is DelayAction delay && delay.DurationTicks < 0)
            {
                errors.Add($"{i}. eylemin bekleme süresi negatif olamaz.");
            }
        }

        return errors.Count == 0 ? MacroValidationResult.Valid() : MacroValidationResult.Invalid([.. errors]);
    }
}
