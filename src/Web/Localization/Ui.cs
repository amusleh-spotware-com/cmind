namespace Web;

/// <summary>
/// Marker type for the app's shared UI resource set. Inject <c>IStringLocalizer&lt;Ui&gt;</c> into a
/// component/endpoint and read <c>L["key"]</c>; the strings live in <c>Resources/Ui.resx</c> (English,
/// the invariant fallback) plus one <c>Ui.&lt;culture&gt;.resx</c> per language in
/// <see cref="Core.Constants.SupportedCultures"/>. Lives in the assembly root namespace so the resource
/// base name resolves to <c>Web.Resources.Ui</c> with <c>ResourcesPath = "Resources"</c>.
/// </summary>
public sealed class Ui;
