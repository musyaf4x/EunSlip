using System.Globalization;
using System.Windows.Data;
using EunSlip.Core.Persistence;
using EunSlip.Desktop.Localization;
using EunSlip.Desktop.ViewModels;

namespace EunSlip.Desktop.Converters;

public sealed class StatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? key = value switch
        {
            BatchStatus status => $"BatchStatus_{status}",
            RecipientStatus status => $"RecipientStatus_{status}",
            AttemptStatus status => $"AttemptStatus_{status}",
            AttemptType status => $"AttemptType_{status}",
            PayrollRunMode status => $"PayrollRunMode_{status}",
            null => null,
            _ => throw new NotSupportedException($"Unsupported status type: {value.GetType().FullName}"),
        };

        return key is null ? "—" : Strings.GetForCulture(key, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
