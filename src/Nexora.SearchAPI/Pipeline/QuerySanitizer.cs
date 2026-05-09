using System.Text.RegularExpressions;

namespace Nexora.SearchAPI.Pipeline;

public sealed partial class QuerySanitizer
{
    [GeneratedRegex(
        // Keep this conservative: prefer matching clearly malicious combinations over single keywords.
        @"(<script|javascript:|onerror\s*=|onload\s*=|--|/\*|\*/|" +
        @"\bselect\b\s+.*\bfrom\b|" +
        @"\bdrop\s+table\b|\bunion\s+select\b|\bexec\s*\(|\bxp_cmdshell\b|" +
        @"'\s*or\s*'?\d+'?\s*=\s*'?\d+'?|" +
        @"(;|&&|\|\|)\s*(cat|curl|wget|nc|rm|sh|bash|powershell)\b|" +
        @"\|\s*(cat|curl|wget|nc|sh|bash|powershell)\b|" +
        @"\$\(|`)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InjectionPattern();

    [GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial Regex ControlChars();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlTags();

    private const int MaxLength = 200;

    public string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var result = input.Trim();
        result = ControlChars().Replace(result, " ");

        if (result.Length > MaxLength)
            result = result[..MaxLength];

        if (InjectionPattern().IsMatch(result))
            return string.Empty;

        result = HtmlTags().Replace(result, " ").Trim();

        return result;
    }
}
