using System.Globalization;
using System.Resources;

namespace EunSlip.Desktop.Localization;

public static class Strings
{
    private static readonly ResourceManager Manager =
        new("EunSlip.Desktop.Localization.Strings", typeof(Strings).Assembly);

    public static string Get(string name)
    {
        return Manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
    }

    public static string GetForCulture(string name, CultureInfo culture) =>
        Manager.GetString(name, culture) ?? name;
}
