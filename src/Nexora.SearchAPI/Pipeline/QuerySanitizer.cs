using System.Text.RegularExpressions;

namespace Nexora.SearchAPI.Pipeline;

public sealed partial class QuerySanitizer
{
    [GeneratedRegex(@"(SELECT\s|INSERT\s|UPDATE\s|DELETE\s|DROP\s|UNION\s|EXEC\s|<script|javascript:|onerror\s*=|onload\s*=|--\s|/\*)", RegexOptions.IgnoreCase)]
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
