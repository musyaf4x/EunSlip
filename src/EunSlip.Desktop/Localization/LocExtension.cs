using System.Windows.Markup;

namespace EunSlip.Desktop.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}
