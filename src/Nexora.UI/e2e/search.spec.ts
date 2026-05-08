import { expect, test } from "@playwright/test";
import type { SearchResponse, SuggestionItem } from "../src/types/search";

// ── helpers ────────────────────────────────────────────────────────────────

function makeSearchResponse(overrides: Partial<SearchResponse> = {}): SearchResponse {
  return {
    results: [
      {
        id: "prod_1",
        title: "Nike Air Zoom Running Shoes",
        brand: "Nike",
        category: "Footwear > Running",
        price: 89.99,
        currency: "USD",
        rating: 4.7,
        ratingCount: 1200,
        stockStatus: "in_stock",
        isFeatured: false,
        finalScore: 0.893,
      },
      {
        id: "prod_2",
        title: "Adidas UltraBoost Trail",
        brand: "Adidas",
        category: "Footwear > Trail",
        price: 120.0,
        currency: "USD",
        rating: 4.5,
        ratingCount: 870,
        stockStatus: "low_stock",
        isFeatured: true,
        finalScore: 0.85,
      },
    ],
    totalCount: 2,
    page: 1,
    perPage: 12,
    totalPages: 1,
    facets: {
      brand: [
        { value: "Nike", count: 1 },
        { value: "Adidas", count: 1 },
      ],
      category: [
        { value: "Footwear > Running", count: 1 },
        { value: "Footwear > Trail", count: 1 },
      ],
    },
    correctedQuery: undefined,
    latencyMs: 12.5,
    cacheHit: false,
    ...overrides,
  };
}

function makeSuggestResponse(): SuggestionItem[] {
  return [
    { text: "running shoes", category: "Footwear", popularityScore: 0.95 },
    { text: "running socks", category: "Accessories", popularityScore: 0.7 },
  ];
}

// ── route mocking setup ────────────────────────────────────────────────────

async function mockSearchApi(
  page: import("@playwright/test").Page,
  response: SearchResponse,
) {
  await page.route("**/api/v1/search**", (route) => {
    void route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(response),
    });
  });
}

async function mockSuggestApi(
  page: import("@playwright/test").Page,
  response: SuggestionItem[],
) {
  await page.route("**/api/v1/suggest**", (route) => {
    void route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(response),
    });
  });
}

// ── tests ──────────────────────────────────────────────────────────────────

test.describe("Search page", () => {
  test("renders search results from API payload", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse());
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    // Results should render with correct titles, brand, and price
    await expect(page.getByText("Nike Air Zoom Running Shoes")).toBeVisible();
    await expect(page.getByText("Adidas UltraBoost Trail")).toBeVisible();
    await expect(page.getByText("USD 89.99")).toBeVisible();
    await expect(page.getByText("USD 120.00")).toBeVisible();
  });

  test("renders result count and latency metadata", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse());
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    await expect(page.getByText(/2 results in 12\.5ms/)).toBeVisible();
  });

  test("renders cache-hit indicator when cacheHit is true", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse({ cacheHit: true }));
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    await expect(page.getByText(/cache hit/)).toBeVisible();
  });

  test("renders suggest dropdown from API payload", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse());
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    const suggestionsList = page.getByRole("list", { name: "Search suggestions" });
    await expect(suggestionsList).toBeVisible();
    await expect(suggestionsList.getByRole("button").filter({ hasText: "running shoes" })).toBeVisible();
    await expect(suggestionsList.getByRole("button").filter({ hasText: "running socks" })).toBeVisible();
  });

  test("clicking a suggestion updates the search input", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse());
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    // Click the first suggestion
    await page.getByRole("list", { name: "Search suggestions" }).getByRole("button").filter({ hasText: "running shoes" }).click();

    await expect(page.getByRole("textbox", { name: "Search products" })).toHaveValue("running shoes");
  });

  test("renders brand and category facets", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse());
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    await expect(page.getByText("Nike (1)")).toBeVisible();
    await expect(page.getByText("Adidas (1)")).toBeVisible();
    await expect(page.getByText("Footwear > Running (1)")).toBeVisible();
  });

  test("checking a brand facet fires a filtered search request", async ({ page }) => {
    const requests: string[] = [];

    await mockSuggestApi(page, makeSuggestResponse());
    await page.route("**/api/v1/search**", (route) => {
      requests.push(route.request().url());
      void route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(makeSearchResponse()),
      });
    });

    await page.goto("/");

    // Wait for initial results then check the Nike brand filter
    await expect(page.getByText("Nike (1)")).toBeVisible();
    const filteredSearchPromise = page.waitForResponse(
      (resp) => resp.url().includes("/api/v1/search") && resp.status() === 200,
    );
    await page.getByLabel("Nike (1)").check();

    // A new search request with filter_by containing "Nike" should be sent
    await filteredSearchPromise;

    const filteredRequest = requests.find((url) => url.includes("filter_by") && url.includes("Nike"));
    expect(filteredRequest).toBeTruthy();
  });

  test("pagination controls are hidden when totalPages is 1", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse({ totalPages: 1 }));
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    await expect(page.getByRole("button", { name: "Previous" })).not.toBeVisible();
    await expect(page.getByRole("button", { name: "Next" })).not.toBeVisible();
  });

  test("pagination controls render and navigate for multi-page results", async ({ page }) => {
    const multiPageResponse = makeSearchResponse({ totalPages: 3, page: 1 });
    const page2Response = makeSearchResponse({ totalPages: 3, page: 2 });

    let currentPage = 1;
    await mockSuggestApi(page, makeSuggestResponse());
    await page.route("**/api/v1/search**", (route) => {
      void route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(currentPage === 1 ? multiPageResponse : page2Response),
      });
    });

    await page.goto("/");

    await expect(page.getByText("Page 1 of 3")).toBeVisible();
    await expect(page.getByRole("button", { name: "Previous" })).toBeDisabled();
    await expect(page.getByRole("button", { name: "Next" })).toBeEnabled();

    currentPage = 2;
    await page.getByRole("button", { name: "Next" }).click();
    await expect(page.getByText("Page 2 of 3")).toBeVisible();
    await expect(page.getByRole("button", { name: "Previous" })).toBeEnabled();
  });

  test("zero-results state displays alternatives from suggest", async ({ page }) => {
    const zeroResults = makeSearchResponse({ results: [], totalCount: 0 });
    await mockSearchApi(page, zeroResults);
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    await expect(page.getByText("No results found")).toBeVisible();
    await expect(page.getByText("Try removing filters or using one of the suggestions.")).toBeVisible();

    // Suggestions rendered as chips
    await expect(page.getByRole("button", { name: "running shoes", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "running socks", exact: true })).toBeVisible();
  });

  test("clicking a zero-results suggestion updates the query", async ({ page }) => {
    const zeroResults = makeSearchResponse({ results: [], totalCount: 0 });
    await mockSearchApi(page, zeroResults);
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    await expect(page.getByText("No results found")).toBeVisible();
    await page.getByRole("button", { name: "running shoes", exact: true }).click();
    await expect(page.getByRole("textbox", { name: "Search products" })).toHaveValue("running shoes");
  });

  test("emits search_result_click analytics event on result click", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse());
    await mockSuggestApi(page, makeSuggestResponse());

    const analyticsRequests: { body: unknown }[] = [];
    await page.route("**/api/v1/analytics/events", async (route) => {
      const body: unknown = route.request().postDataJSON();
      analyticsRequests.push({ body });
      await route.fulfill({ status: 204 });
    });

    await page.goto("/");

    await page.getByRole("button", { name: "Nike Air Zoom Running Shoes", exact: true }).click();

    await page.waitForTimeout(200);

    const clickEvent = analyticsRequests.find(
      (r) => (r.body as Record<string, unknown>)?.eventType === "search_result_click",
    );
    expect(clickEvent).toBeTruthy();

    const payload = clickEvent!.body as Record<string, unknown>;
    // Non-PII: no raw query text, only metadata
    expect(payload).not.toHaveProperty("query");
    expect(payload).toHaveProperty("queryLength");
    expect(payload).toHaveProperty("queryTokenCount");
    expect(payload).toHaveProperty("productId", "prod_1");
    expect(payload).toHaveProperty("position", 1);
  });

  test("emits search_add_to_cart analytics event on add-to-cart click", async ({ page }) => {
    await mockSearchApi(page, makeSearchResponse());
    await mockSuggestApi(page, makeSuggestResponse());

    const analyticsRequests: { body: unknown }[] = [];
    await page.route("**/api/v1/analytics/events", async (route) => {
      const body: unknown = route.request().postDataJSON();
      analyticsRequests.push({ body });
      await route.fulfill({ status: 204 });
    });

    await page.goto("/");

    await page.getByRole("button", { name: "Add Nike Air Zoom Running Shoes to cart" }).click();

    await page.waitForTimeout(200);

    const cartEvent = analyticsRequests.find(
      (r) => (r.body as Record<string, unknown>)?.eventType === "search_add_to_cart",
    );
    expect(cartEvent).toBeTruthy();

    const payload = cartEvent!.body as Record<string, unknown>;
    expect(payload).not.toHaveProperty("query");
    expect(payload).toHaveProperty("queryLength");
    expect(payload).toHaveProperty("productId", "prod_1");
    expect(payload).toHaveProperty("sentAtUtc");
  });

  test("shows error state when search API fails", async ({ page }) => {
    await page.route("**/api/v1/search**", (route) => {
      void route.fulfill({ status: 500, body: "Internal Server Error" });
    });
    await mockSuggestApi(page, makeSuggestResponse());

    await page.goto("/");

    await expect(page.getByRole("alert")).toContainText("Search API unavailable");
  });

  test("price range filter is sent in search request", async ({ page }) => {
    const requests: string[] = [];
    await mockSuggestApi(page, makeSuggestResponse());
    await page.route("**/api/v1/search**", (route) => {
      requests.push(route.request().url());
      void route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(makeSearchResponse()),
      });
    });

    await page.goto("/");

    await page.getByLabel("Price range").selectOption("0-50");

    await page.waitForResponse((resp) => resp.url().includes("/api/v1/search") && resp.status() === 200);

    const filteredRequest = requests.find((url) => url.includes("filter_by") && url.includes("price"));
    expect(filteredRequest).toBeTruthy();
  });
});
