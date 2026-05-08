namespace Nexora.IndexSync.Services;

public sealed class CdcQueryBuilder
{
    public string BuildChangesQuery() => """
        WITH ProductChanges AS (
            SELECT
                p.__$operation AS operation_code,
                p.product_id,
                sys.fn_cdc_map_lsn_to_time(p.__$start_lsn) AS change_time,
                CAST(1 AS bit) AS has_product_change,
                CAST(0 AS bit) AS has_inventory_change
            FROM cdc.fn_cdc_get_all_changes_dbo_products(
                sys.fn_cdc_map_time_to_lsn('smallest greater than or equal', @from),
                sys.fn_cdc_map_time_to_lsn('largest less than or equal', @to),
                'all') p
            WHERE p.__$operation IN (1, 2, 4)
        ),
        StockChanges AS (
            SELECT
                4 AS operation_code,
                s.product_id,
                sys.fn_cdc_map_lsn_to_time(s.__$start_lsn) AS change_time,
                CAST(0 AS bit) AS has_product_change,
                CAST(1 AS bit) AS has_inventory_change
            FROM cdc.fn_cdc_get_all_changes_dbo_stock(
                sys.fn_cdc_map_time_to_lsn('smallest greater than or equal', @from),
                sys.fn_cdc_map_time_to_lsn('largest less than or equal', @to),
                'all') s
            WHERE s.__$operation IN (2, 4)
        ),
        PricingChanges AS (
            SELECT
                4 AS operation_code,
                p.product_id,
                sys.fn_cdc_map_lsn_to_time(p.__$start_lsn) AS change_time,
                CAST(0 AS bit) AS has_product_change,
                CAST(1 AS bit) AS has_inventory_change
            FROM cdc.fn_cdc_get_all_changes_dbo_pricing(
                sys.fn_cdc_map_time_to_lsn('smallest greater than or equal', @from),
                sys.fn_cdc_map_time_to_lsn('largest less than or equal', @to),
                'all') p
            WHERE p.__$operation IN (2, 4)
        ),
        AggregatedChanges AS (
            SELECT
                c.product_id,
                MAX(CASE WHEN c.operation_code = 1 THEN 1 ELSE 0 END) AS is_delete,
                MAX(CAST(c.has_product_change AS int)) AS has_product_change,
                MAX(c.change_time) AS change_time
            FROM (
                SELECT * FROM ProductChanges
                UNION ALL
                SELECT * FROM StockChanges
                UNION ALL
                SELECT * FROM PricingChanges
            ) c
            GROUP BY c.product_id
        )
        SELECT
            CASE WHEN c.is_delete = 1 THEN 1 ELSE 4 END AS __$operation,
            c.product_id,
            v.product_name,
            v.brand_name,
            v.product_sku,
            v.product_description,
            v.category_name,
            v.category_hierarchy,
            v.unit_price,
            v.currency_code,
            v.color_variants,
            v.size_variants,
            v.avg_rating,
            v.rating_count,
            v.is_featured_flag,
            v.is_active_flag,
            v.merchant_id,
            v.created_date,
            v.modified_date,
            COALESCE(v.stock_status, 'OUT_OF_STOCK') AS stock_status,
            COALESCE(v.stock_quantity, 0) AS stock_quantity,
            CASE WHEN c.has_product_change = 1 THEN 'product' ELSE 'stock_price' END AS change_source,
            CAST(COALESCE(c.change_time, SYSUTCDATETIME()) AS datetimeoffset) AS change_timestamp
        FROM AggregatedChanges c
        LEFT JOIN vw_search_product_flat v ON v.product_id = c.product_id
        ORDER BY c.change_time, c.product_id;
        """;

    public string BuildFullReindexQuery() => """
        SELECT
            4 AS __$operation,
            product_id,
            product_name,
            brand_name,
            product_sku,
            product_description,
            category_name,
            category_hierarchy,
            unit_price,
            currency_code,
            color_variants,
            size_variants,
            avg_rating,
            rating_count,
            is_featured_flag,
            is_active_flag,
            merchant_id,
            created_date,
            modified_date,
            stock_status,
            stock_quantity,
            'product' AS change_source,
            CAST(SYSUTCDATETIME() AS datetimeoffset) AS change_timestamp
        FROM vw_search_product_flat
        ORDER BY product_id
        OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY;
        """;
}
