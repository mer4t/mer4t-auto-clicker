using System.Text.Json.Serialization;

namespace m4AutoClicker.Domain.Macros;

// JSON'a yazılırken/okunurken alt türü ayırt etmek için "type" alanı kullanılır (bkz. Aşama 7,
// JsonMacroRepository). Yeni bir eylem türü eklendiğinde buraya da bir JsonDerivedType eklenmelidir.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MouseMoveAction), "move")]
[JsonDerivedType(typeof(MouseButtonDownAction), "buttonDown")]
[JsonDerivedType(typeof(MouseButtonUpAction), "buttonUp")]
[JsonDerivedType(typeof(MouseWheelAction), "wheel")]
[JsonDerivedType(typeof(DelayAction), "delay")]
[JsonDerivedType(typeof(KeyDownAction), "keyDown")]
[JsonDerivedType(typeof(KeyUpAction), "keyUp")]
public abstract record MacroAction
{
    public required long OffsetTicks { get; init; }
}
