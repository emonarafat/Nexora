using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Nexora.SearchAPI.Pipeline;

public enum SearchIntent { Transactional, Navigational, CategoryFiltered }

[ExcludeFromCodeCoverage]
public record ClassifiedIntent(SearchIntent Intent, Dictionary<string, string>? Filters = null);

public sealed partial class IntentClassifier
{
    [GeneratedRegex(@"^[A-Z0-9]{2,}-[A-Z0-9]+$", RegexOptions.IgnoreCase)]
    private static partial Regex SkuPattern();

    private static readonly HashSet<string> KnownCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "shoes", "footwear", "clothing", "electronics", "furniture",
        "laptops", "phones", "tablets", "cameras", "headphones",
        "sofas", "chairs", "tables", "beds", "appliances"
    };

    public ClassifiedIntent Classify(string query)
    {
        if (SkuPattern().IsMatch(query.Trim()))
            return new ClassifiedIntent(SearchIntent.Navigational);

        foreach (var word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (KnownCategories.Contains(word))
                return new ClassifiedIntent(SearchIntent.CategoryFiltered, new Dictionary<string, string>());

        return new ClassifiedIntent(SearchIntent.Transactional);
    }
}
