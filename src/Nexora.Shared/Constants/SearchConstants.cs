namespace Nexora.Shared.Constants;

public static class SearchConstants
{
    public const string ProductsCollection = "products";
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxQueryLength = 200;
    public const int MaxSuggestResults = 20;
    public const int DefaultSuggestResults = 8;
    public const int MaxDeepPaginationPage = 50;

    public static class StockStatus
    {
        public const string InStock = "in_stock";
        public const string LowStock = "low_stock";
        public const string OutOfStock = "out_of_stock";
    }

    public static class SortModes
    {
        public const string Relevance = "relevance";
        public const string PriceAsc = "price_asc";
        public const string PriceDesc = "price_desc";
        public const string Rating = "rating";
        public const string Newest = "newest";
    }

    public static class CacheKeys
    {
        public static string Search(string queryHash, string filtersHash, int page)
            => $"search::{queryHash}::{filtersHash}::{page}";
        public static string Suggest(string prefixHash)
            => $"suggest::{prefixHash}";
    }

    public static class CacheTtl
    {
        public static readonly TimeSpan Search = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan Suggest = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan Synonyms = TimeSpan.FromMinutes(5);
    }
}
