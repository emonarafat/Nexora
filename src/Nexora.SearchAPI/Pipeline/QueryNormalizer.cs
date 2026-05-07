using System.Text;
using System.Text.RegularExpressions;

namespace Nexora.SearchAPI.Pipeline;

public sealed partial class QueryNormalizer
{
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"[^\w\s\-+#/]")]
    private static partial Regex ExcessivePunctuation();

    public string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var result = input.ToLowerInvariant().Normalize(NormalizationForm.FormC);
        result = ExcessivePunctuation().Replace(result, " ");
        result = MultiSpace().Replace(result, " ").Trim();
        return result;
    }
}
