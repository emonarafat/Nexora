using FluentAssertions;
using Nexora.SearchAPI.Pipeline;
using Xunit;

namespace Nexora.SearchAPI.Tests.Pipeline;

public class QuerySanitizerTests
{
    private readonly QuerySanitizer _sut = new();

    [Fact]
    public void Sanitize_ValidQuery_TrimmedAndReturned()
        => _sut.Sanitize("  running shoes  ").Should().Be("running shoes");

    [Theory]
    [InlineData("SELECT * FROM products")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("1; DROP TABLE products--")]
    [InlineData("/* comment */")]
    public void Sanitize_InjectionPatterns_ReturnsEmpty(string malicious)
        => _sut.Sanitize(malicious).Should().BeEmpty();

    [Fact]
    public void Sanitize_LongQuery_TruncatesToMaxLength()
        => _sut.Sanitize(new string('a', 300)).Length.Should().BeLessThanOrEqualTo(200);

    [Fact]
    public void Sanitize_EmptyOrWhitespace_ReturnsEmpty()
    {
        _sut.Sanitize("").Should().BeEmpty();
        _sut.Sanitize("   ").Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_HtmlInNonInjectionQuery_StripsHtml()
    {
        var result = _sut.Sanitize("running <b>shoes</b> for men");
        result.Should().NotContain("<b>");
        result.Should().Contain("running");
        result.Should().Contain("shoes");
    }
}
