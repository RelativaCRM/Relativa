using System.Globalization;
using System.Text.Json;
using Relativa.Authentication.Application.Interfaces;

namespace Relativa.Authentication.Infrastructure.Services;

public sealed class JsonEmailLocalizer : IEmailLocalizer
{
    private const string DefaultLocale = "en";

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _catalogs;

    public JsonEmailLocalizer()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Emails", "locales");
        var catalogs = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            {
                var locale = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (entries is not null)
                {
                    catalogs[locale] = entries;
                }
            }
        }

        _catalogs = catalogs;
    }

    public string Get(string? locale, string key, params object[] args)
    {
        var template = Resolve(locale, key) ?? key;
        return args.Length > 0
            ? string.Format(CultureInfo.InvariantCulture, template, args)
            : template;
    }

    private string? Resolve(string? locale, string key)
    {
        foreach (var candidate in CandidateLocales(locale))
        {
            if (_catalogs.TryGetValue(candidate, out var catalog) && catalog.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateLocales(string? locale)
    {
        if (!string.IsNullOrWhiteSpace(locale))
        {
            yield return locale;
            var dash = locale.IndexOf('-');
            if (dash > 0)
            {
                yield return locale[..dash];
            }
        }

        yield return DefaultLocale;
    }
}
