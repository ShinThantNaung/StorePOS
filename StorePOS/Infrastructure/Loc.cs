using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace StorePOS.Infrastructure;

/// <summary>All screen text comes from Resources/Strings.resx (English) and Strings.my.resx (Burmese).</summary>
public static class Loc
{
    private static readonly ResourceManager Resources =
        new("StorePOS.Resources.Strings", typeof(Loc).Assembly);

    public static CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo("my-MM");

    public static bool IsBurmese => Culture.TwoLetterISOLanguageName == "my";

    public static string Get(string key) => Resources.GetString(key, Culture) ?? $"[{key}]";

    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), args);

    /// <summary>Formats a date with a pattern that itself comes from the resource file.</summary>
    public static string Date(DateTime value, string patternKey) => value.ToString(Get(patternKey), Culture);
}

/// <summary>XAML: Text="{l:Tr Nav_Sales}"</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) { Key = key; }

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
