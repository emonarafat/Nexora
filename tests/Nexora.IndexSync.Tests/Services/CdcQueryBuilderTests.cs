using FluentAssertions;
using Nexora.IndexSync.Services;

namespace Nexora.IndexSync.Tests.Services;

public class CdcQueryBuilderTests
{
    private readonly CdcQueryBuilder _builder = new();

    [Fact]
    public void BuildChangesQuery_IncludesProductsStockAndPricingCdcSources()
    {
        var query = _builder.BuildChangesQuery();

        query.Should().Contain("fn_cdc_get_all_changes_dbo_products");
        query.Should().Contain("fn_cdc_get_all_changes_dbo_stock");
        query.Should().Contain("fn_cdc_get_all_changes_dbo_pricing");
        query.Should().Contain("vw_search_product_flat");
        query.Should().Contain("change_source");
        query.Should().Contain("change_timestamp");
    }

    [Fact]
    public void BuildFullReindexQuery_UsesPagedViewQuery()
    {
        var query = _builder.BuildFullReindexQuery();

        query.Should().Contain("vw_search_product_flat");
        query.Should().Contain("OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY");
        query.Should().Contain("'product' AS change_source");
    }
}
