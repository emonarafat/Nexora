using FluentAssertions;
using Nexora.SearchAPI.Pipeline;
using Xunit;

namespace Nexora.SearchAPI.Tests.Pipeline;

public class QueryNormalizerTests
{
    private readonly QueryNormalizer _sut = new();

    [Theory]
    [InlineData("Running Shoes", "running shoes")]
    [InlineData("NIKE AIR MAX", "nike air max")]
    [InlineData("  trim me  ", "trim me")]
    public void Normalize_VariousInputs_ReturnsExpected(string input, string expected)
        => _sut.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_ExcessiveSpaces_CollapseToSingle()
        => _sut.Normalize("running   shoes").Should().Be("running shoes");

    [Fact]
    public void Normalize_Empty_ReturnsEmpty()
        => _sut.Normalize("").Should().BeEmpty();
}
