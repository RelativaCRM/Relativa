namespace Relativa.Core.Application.Utilities;

internal static class DisplayNameHelper
{
    // Converts snake_case to Title Case as a fallback when display_name is null.
    internal static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return string.Join(" ", name.Split('_')
            .Where(w => w.Length > 0)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }
}
