using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Tests.Fixtures;

/// <summary>
/// Test data fixtures for Phase 1.7 integration tests.
/// Provides 1000 product fixtures with realistic data for load testing.
/// </summary>
public static class ProductFixtures
{
    // Use a fixed-seed Random protected by a lock so generation is deterministic and thread-safe.
    private static readonly Random SeededRandom = new(42);
    private static readonly object RandomLock = new();

    private static readonly string[] Brands = [
        "Nike", "Adidas", "Puma", "Reebok", "Under Armour",
        "Samsung", "Apple", "Sony", "LG", "Dell",
        "IKEA", "Ashley", "Wayfair", "Herman Miller",
        "Cuisinart", "KitchenAid", "Ninja", "Instant Pot"
    ];

    private static readonly string[] Categories = [
        "Footwear", "Electronics", "Furniture", "Kitchen & Dining",
        "Sports & Outdoors", "Clothing", "Home Decor", "Books",
        "Toys & Games", "Beauty & Personal Care"
    ];

    private static readonly string[] Colors = [
        "Black", "White", "Red", "Blue", "Green", "Gray",
        "Brown", "Silver", "Gold", "Navy", "Beige"
    ];

    private static readonly string[] StockStatuses = [
        "in_stock", "in_stock", "in_stock", "in_stock", // 80% in stock
        "low_stock", "out_of_stock"
    ];

    private static readonly (string Query, string[] Products)[] QueryExpectations = [
        ("running shoes", ["Nike Air Zoom", "Adidas Ultraboost", "Puma Speed 600"]),
        ("laptop", ["Dell XPS", "Apple MacBook", "Samsung Galaxy Book"]),
        ("couch", ["IKEA Ektorp Sofa", "Ashley L-Shaped Sectional"]),
        ("sneakers", ["Nike Air Force", "Adidas Stan Smith", "Puma Suede"]),
        ("coffee maker", ["Cuisinart Brew Central", "Ninja Hot & Cold"])
    ];

    /// <summary>
    /// Generates 1000 product documents with realistic data distribution.
    /// </summary>
    public static IEnumerable<ProductDocument> Generate1000Products()
    {
        var products = new List<ProductDocument>();

        for (int i = 1; i <= 1000; i++)
        {
            var brand = RandomChoice(Brands);
            var category = RandomChoice(Categories);
            var title = GenerateTitle(brand, category, i);

            products.Add(new ProductDocument
            {
                Id = $"PROD-{i:D5}",
                Sku = $"SKU-{i:D5}",
                Title = title,
                Description = GenerateDescription(title, brand, category),
                Brand = brand,
                Category = category,
                CategoryPath = [category, GenerateSubCategory(category)],
                Price = (float)Math.Round(10 + NextDouble() * 990, 2),
                Currency = "USD",
                StockStatus = RandomChoice(StockStatuses),
                StockQuantity = NextInt(0, 200),
                Rating = (float)Math.Round(2.0 + NextDouble() * 3.0, 1), // 2.0 - 5.0
                RatingCount = NextInt(0, 1000),
                IsFeatured = NextDouble() < 0.05, // 5% featured
                IsActive = NextDouble() < 0.95, // 95% active
                Color = [RandomChoice(Colors)],
                Size = GenerateSizes(category),
                PopularityScore = (float)Math.Round(NextDouble(), 3),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-NextInt(1, 365)).ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-NextInt(0, 30)).ToUnixTimeSeconds()
            });
        }

        return products;
    }

    private const float DefaultQueryProductPrice = 99.99f;

    /// <summary>
    /// Generates a subset of products matching specific query expectations
    /// for acceptance test validation.
    /// </summary>
    public static IEnumerable<ProductDocument> GenerateProductsForQuery(string query)
    {
        var expectation = QueryExpectations.FirstOrDefault(q =>
            q.Query.Equals(query, StringComparison.OrdinalIgnoreCase));

        if (expectation == default) return [];

        var category = InferCategory(query);
        return expectation.Products.Select((title, idx) => new ProductDocument
        {
            Id = $"QUERY-PROD-{idx:D3}",
            Sku = $"QUERY-SKU-{idx:D3}",
            Title = title,
            Description = $"High-quality {title} for {query}",
            Brand = ExtractBrand(title),
            Category = category,
            CategoryPath = [category],
            Price = DefaultQueryProductPrice,
            Currency = "USD",
            StockStatus = "in_stock",
            StockQuantity = 50,
            Rating = 4.5f,
            RatingCount = 100,
            IsFeatured = false,
            IsActive = true,
            Color = [],
            Size = [],
            PopularityScore = 0.9f,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }

    private static string GenerateTitle(string brand, string category, int index)
    {
        var adjectives = new[] { "Premium", "Essential", "Pro", "Classic", "Ultra", "Advanced" };
        var adjective = RandomChoice(adjectives);

        return category switch
        {
            "Footwear" => $"{brand} {adjective} {RandomChoice(new[] { "Running", "Training", "Casual", "Basketball" })} Shoes",
            "Electronics" => $"{brand} {adjective} {RandomChoice(new[] { "Laptop", "Tablet", "Smartphone", "Monitor" })}",
            "Furniture" => $"{brand} {adjective} {RandomChoice(new[] { "Sofa", "Chair", "Desk", "Table" })}",
            "Kitchen & Dining" => $"{brand} {adjective} {RandomChoice(new[] { "Blender", "Coffee Maker", "Toaster", "Mixer" })}",
            _ => $"{brand} {adjective} {category} Item {index}"
        };
    }

    private static string GenerateDescription(string title, string brand, string category)
    {
        return $"Experience quality with the {title}. This {brand} {category.ToLower()} item " +
               $"combines style, comfort, and durability. Perfect for everyday use. " +
               $"Rated highly by customers for performance and value.";
    }

    private static string GenerateSubCategory(string category)
    {
        return category switch
        {
            "Footwear" => RandomChoice(new[] { "Athletic", "Casual", "Formal", "Outdoor" }),
            "Electronics" => RandomChoice(new[] { "Computers", "Mobile", "Audio", "TV & Video" }),
            "Furniture" => RandomChoice(new[] { "Living Room", "Bedroom", "Office", "Outdoor" }),
            "Kitchen & Dining" => RandomChoice(new[] { "Appliances", "Cookware", "Utensils", "Storage" }),
            _ => "General"
        };
    }

    private static string[] GenerateSizes(string category)
    {
        return category switch
        {
            "Footwear" => ["7", "8", "9", "10", "11", "12"],
            "Clothing" => ["XS", "S", "M", "L", "XL", "XXL"],
            _ => []
        };
    }

    private static T RandomChoice<T>(T[] items)
    {
        lock (RandomLock)
        {
            return items[SeededRandom.Next(items.Length)];
        }
    }

    private static double NextDouble()
    {
        lock (RandomLock) { return SeededRandom.NextDouble(); }
    }

    private static int NextInt(int minValue, int maxValue)
    {
        lock (RandomLock) { return SeededRandom.Next(minValue, maxValue); }
    }

    private static string ExtractBrand(string title)
    {
        var parts = title.Split(' ');
        return Brands.FirstOrDefault(b => parts[0].Equals(b, StringComparison.OrdinalIgnoreCase))
               ?? "Generic";
    }

    private static string InferCategory(string query)
    {
        if (query.Contains("shoe") || query.Contains("sneaker")) return "Footwear";
        if (query.Contains("laptop") || query.Contains("phone")) return "Electronics";
        if (query.Contains("couch") || query.Contains("sofa")) return "Furniture";
        if (query.Contains("coffee")) return "Kitchen & Dining";
        return "General";
    }
}
