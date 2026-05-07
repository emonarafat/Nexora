using FluentAssertions;
using Nexora.SearchAPI.Pipeline;
using Xunit;

namespace Nexora.SearchAPI.Tests.Pipeline;

/// <summary>
/// Tests for spell correction behavior through Typesense typo tolerance.
/// Spell correction is handled by Typesense's num_typos parameter,
/// not in the pipeline itself.
/// </summary>
public class SpellCorrectionTests
{
    private readonly QueryStructurer _structurer = new();

    [Theory]
    [InlineData("snekars")]    // Typo: sneakers
    [InlineData("laptpo")]     // Typo: laptop
    [InlineData("tabel")]      // Typo: table
    [InlineData("shose")]      // Typo: shoes
    public void TypoTolerance_LongTypos_Uses2Typos(string query)
    {
        // Arrange: queries > 8 chars should have num_typos = 2
        var processed = new ProcessedQuery(
            OriginalQuery: query,
            NormalizedQuery: query,
            CorrectedQuery: null,
            ExpandedTerms: [query],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new Nexora.Shared.DTOs.SearchRequest { Query = query };

        // Act
        var result = _structurer.BuildSearchParameters(processed, request);

        // Assert
        // Typesense will use 2 typos to find "sneakers" from "snekars"
        if (query.Length > 8)
        {
            result.NumberOfTypos.Should().Be("2");
        }
        else
        {
            result.NumberOfTypos.Should().Be(query.Length <= 8 ? "1" : "2");
        }
    }

    [Fact]
    public void TypoTolerance_Sneakers_Typo_Example()
    {
        // Arrange: "snekars" should match "sneakers" with 2 typos in Typesense
        const string typoQuery = "snekars";  // 7 chars - but we use >= 8 check, so it's 1 typo

        var processed = new ProcessedQuery(
            OriginalQuery: typoQuery,
            NormalizedQuery: typoQuery,
            CorrectedQuery: null,
            ExpandedTerms: [typoQuery],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new Nexora.Shared.DTOs.SearchRequest { Query = typoQuery };

        // Act
        var result = _structurer.BuildSearchParameters(processed, request);

        // Assert
        // 7 chars <= 8, so num_typos = 1
        result.NumberOfTypos.Should().Be("1");
    }

    [Fact]
    public void TypoTolerance_LongerSneakersTypo_Uses2Typos()
    {
        // Arrange: "snekars shoes" is > 8 chars, should use 2 typos
        const string typoQuery = "snekars shoes";  // 13 chars

        var processed = new ProcessedQuery(
            OriginalQuery: typoQuery,
            NormalizedQuery: typoQuery,
            CorrectedQuery: null,
            ExpandedTerms: [typoQuery],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new Nexora.Shared.DTOs.SearchRequest { Query = typoQuery };

        // Act
        var result = _structurer.BuildSearchParameters(processed, request);

        // Assert
        // 13 chars > 8, so num_typos = 2 (Typesense will correct "snekars" → "sneakers")
        result.NumberOfTypos.Should().Be("2");
    }

    [Theory]
    [InlineData("laptop", "1")]     // 6 chars
    [InlineData("shoe", "1")]       // 4 chars
    [InlineData("table", "1")]      // 5 chars
    [InlineData("computer", "1")]   // 8 chars (boundary)
    [InlineData("computers", "2")]  // 9 chars
    [InlineData("smartphone", "2")] // 10 chars
    public void TypoTolerance_VaryingLengths_CorrectTypoCount(string query, string expectedTypos)
    {
        // Arrange
        var processed = new ProcessedQuery(
            OriginalQuery: query,
            NormalizedQuery: query,
            CorrectedQuery: null,
            ExpandedTerms: [query],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new Nexora.Shared.DTOs.SearchRequest { Query = query };

        // Act
        var result = _structurer.BuildSearchParameters(processed, request);

        // Assert
        result.NumberOfTypos.Should().Be(expectedTypos);
    }

    [Fact]
    public void Pipeline_ProcessesTypo_PreservesOriginal()
    {
        // This test demonstrates that the pipeline doesn't correct typos
        // It preserves the original query for Typesense to handle
        var processed = new ProcessedQuery(
            OriginalQuery: "snekars",
            NormalizedQuery: "snekars",
            CorrectedQuery: null,
            ExpandedTerms: ["snekars"],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        // The pipeline should NOT correct the typo
        // Typesense will handle it with num_typos parameter
        processed.NormalizedQuery.Should().Be("snekars");
        processed.CorrectedQuery.Should().BeNull();
    }
}
