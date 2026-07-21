using System.Collections.Immutable;
using System.Globalization;
using EunSlip.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace EunSlip.Infrastructure.Logging;

public static class EunSlipLogging
{
    public static ILoggerFactory CreateLoggerFactory(AppPaths paths)
    {
        Logger logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.With(new RedactionEnricher())
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "eunslip-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileTimeLimit: TimeSpan.FromDays(30),
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return new SerilogLoggerFactory(logger, dispose: true);
    }

    private sealed class RedactionEnricher : ILogEventEnricher
    {
        private const string Redacted = "[redacted]";

        private static readonly ImmutableHashSet<string> SensitiveNames =
            ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
                "Nik", "Email", "Token", "AccessToken", "RefreshToken",
                "AuthorizationCode", "Password", "Salary", "Gross", "Nett", "Total",
                "TakeHomePay", "Gaji", "BasicSalary", "PdfContent");

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
            {
                LogEventPropertyValue redactedValue = RedactValue(property.Key, property.Value);
                if (!ReferenceEquals(redactedValue, property.Value))
                {
                    logEvent.AddOrUpdateProperty(
                        new LogEventProperty(property.Key, redactedValue));
                }
            }
        }

        private static LogEventPropertyValue RedactValue(string name, LogEventPropertyValue value)
        {
            return SensitiveNames.Contains(name)
                ? new ScalarValue(Redacted)
                : value switch
                {
                    StructureValue structure => new StructureValue(
                        structure.Properties.Select(p =>
                            new LogEventProperty(p.Name, RedactValue(p.Name, p.Value))),
                        structure.TypeTag),
                    SequenceValue sequence => new SequenceValue(
                        sequence.Elements.Select(e => RedactValue(string.Empty, e))),
                    DictionaryValue dictionary => new DictionaryValue(
                        dictionary.Elements.Select(kv =>
                            KeyValuePair.Create(kv.Key, RedactValue(string.Empty, kv.Value)))),
                    _ => value,
                };
        }
    }
}
