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
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {ExceptionSanitized}{NewLine}")
            .CreateLogger();

        return new SerilogLoggerFactory(logger, dispose: true);
    }

    private sealed class RedactionEnricher : ILogEventEnricher
    {
        private const string Redacted = "[redacted]";

        private static readonly ImmutableArray<string> SensitiveFragments =
            ImmutableArray.Create(
                "nik", "email", "token", "secret", "password", "salary",
                "gross", "nett", "total", "takehome", "gaji", "authorizationcode",
                "accesstoken", "refreshtoken", "pdfcontent", "clientsecret");

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

            if (logEvent.Exception is not null)
            {
                logEvent.AddPropertyIfAbsent(new LogEventProperty(
                    "ExceptionSanitized", new ScalarValue(SanitizeException(logEvent.Exception))));
            }
        }

        private static string SanitizeException(System.Exception ex)
        {
            string message = ex.Message ?? string.Empty;
            message = System.Text.RegularExpressions.Regex.Replace(
                message, @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", "[email]");
            return message.Length > 200 ? message[..200] : message;
        }

        private static bool IsSensitiveName(string name)
        {
            string lower = name.ToLowerInvariant();
            foreach (string fragment in SensitiveFragments)
            {
                if (lower.Contains(fragment, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static LogEventPropertyValue RedactValue(string name, LogEventPropertyValue value)
        {
            if (IsSensitiveName(name))
            {
                return new ScalarValue(Redacted);
            }

            return value switch
            {
                ScalarValue scalar when scalar.Value is string text && ContainsSensitivePattern(text)
                    => new ScalarValue(Redacted),
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

        private static bool ContainsSensitivePattern(string text)
        {
            if (text.Contains('@') && text.Contains('.', StringComparison.Ordinal))
            {
                ReadOnlySpan<char> trimmed = text.AsSpan().Trim();
                int at = trimmed.IndexOf('@');
                if (at > 0 && at < trimmed.Length - 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
