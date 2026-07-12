namespace Core.WhiteLabel;

/// <summary>
/// The shape of a white-label option's value, driving both validation and which editor the Owner UI
/// renders (switch / text / colour picker / select / chips / password / number).
/// </summary>
public enum WhiteLabelValueKind
{
    Bool,
    String,
    MultilineString,
    Int,
    Number,
    TimeSpan,
    Enum,
    StringList,
    Color,
    Secret
}
