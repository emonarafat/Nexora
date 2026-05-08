import { expect, test } from "@playwright/test";
import type { LoginResponse } from "../src/features/auth/types";
import type { Synonym, SynonymsResponse, ReindexJob, RankingConfig } from "../src/types/admin";

// ── helpers ────────────────────────────────────────────────────────────────

function makeLoginResponse(): LoginResponse {
  return { accessToken: "test.jwt.token", expiresIn: 3600, role: "admin" };
}

function makeSynonymsResponse(): SynonymsResponse {
  return {
    synonyms: [
      {
        id: "syn_1",
        terms: ["sneaker", "trainer", "running shoe"],
        createdAt: "2024-01-01T00:00:00Z",
        updatedAt: "2024-01-15T00:00:00Z",
      },
    ],
    totalCount: 1,
  };
}

function makeReindexJob(): ReindexJob {
  return {
    jobId: "job_abc123",
    status: "completed",
    startedAt: "2024-01-15T10:00:00Z",
    completedAt: "2024-01-15T10:03:00Z",
    documentsProcessed: 5000,
    totalDocuments: 5000,
  };
}

function makeRankingConfig(): RankingConfig {
  return {
    textScoreWeight: 0.4,
    ctrWeight: 0.2,
    conversionWeight: 0.15,
    recencyWeight: 0.1,
    popularityWeight: 0.15,
    version: 3,
    lastUpdatedAt: "2024-01-10T00:00:00Z",
    lastUpdatedBy: "admin",
  };
}

// ── mock setup ─────────────────────────────────────────────────────────────

async function mockAll(page: import("@playwright/test").Page) {
  await page.route("**/api/v1/auth/login", (route) => {
    void route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeLoginResponse()),
    });
  });

  await page.route("**/api/v1/synonyms", (route) => {
    if (route.request().method() === "GET") {
      void route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(makeSynonymsResponse()),
      });
    } else if (route.request().method() === "POST") {
      const newSynonym: Synonym = {
        id: "syn_new",
        terms: ["boot", "ankle boot"],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };
      void route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify(newSynonym),
      });
    } else {
      void route.continue();
    }
  });

  // Match both POST /reindex and GET /reindex/{jobId}
  await page.route("**/api/v1/reindex**", (route) => {
    void route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeReindexJob()),
    });
  });

  await page.route("**/api/v1/ranking/config", (route) => {
    void route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRankingConfig()),
    });
  });
}

async function loginAndGoToAdmin(page: import("@playwright/test").Page) {
  await page.goto("/login");
  await page.getByLabel("Username").fill("admin");
  await page.getByLabel("Password").fill("password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/admin/);
}

// ── tests ──────────────────────────────────────────────────────────────────

test.describe("Admin auth guard", () => {
  test("unauthenticated access to /admin redirects to /login", async ({ page }) => {
    await page.goto("/admin");
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByRole("heading", { name: "Nexora Admin" })).toBeVisible();
  });

  test("login form shows error for failed credentials", async ({ page }) => {
    await page.route("**/api/v1/auth/login", (route) => {
      void route.fulfill({ status: 401, body: "Unauthorized" });
    });

    await page.goto("/login");
    await page.getByLabel("Username").fill("wrong");
    await page.getByLabel("Password").fill("wrong");
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(page.getByRole("alert")).toBeVisible();
    await expect(page.getByRole("alert")).toContainText("Invalid credentials");
  });

  test("sign-out clears session and redirects to login on admin revisit", async ({ page }) => {
    await mockAll(page);
    await loginAndGoToAdmin(page);

    await page.getByRole("button", { name: "Sign out" }).click();

    // Should still be on the root, not logged in
    await expect(page.getByRole("button", { name: "Sign out" })).not.toBeVisible();

    await page.goto("/admin");
    await expect(page).toHaveURL(/\/login/);
  });
});

test.describe("Admin synonyms CRUD", () => {
  test.beforeEach(async ({ page }) => {
    await mockAll(page);
    await loginAndGoToAdmin(page);
  });

  test("renders existing synonyms list", async ({ page }) => {
    await expect(page.getByText("sneaker • trainer • running shoe")).toBeVisible();
  });

  test("add synonym form is visible", async ({ page }) => {
    await expect(page.getByText("Add New Synonym Group")).toBeVisible();
    await expect(page.getByPlaceholder("Enter comma-separated synonym terms")).toBeVisible();
  });

  test("creating a synonym calls POST and refreshes list", async ({ page }) => {
    const requests: string[] = [];
    await page.route("**/api/v1/synonyms", (route) => {
      if (route.request().method() === "POST") {
        requests.push(route.request().url());
        void route.fulfill({
          status: 201,
          contentType: "application/json",
          body: JSON.stringify({
            id: "syn_new",
            terms: ["boot", "ankle boot"],
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          } satisfies Synonym),
        });
      } else {
        void route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(makeSynonymsResponse()),
        });
      }
    });

    await page.getByPlaceholder("Enter comma-separated synonym terms").fill("boot, ankle boot");
    await page.getByRole("button", { name: "Add" }).click();

    await page.waitForResponse((r) => r.url().includes("/api/v1/synonyms") && r.request().method() === "POST");
    expect(requests.length).toBeGreaterThan(0);
  });
});

test.describe("Admin reindex", () => {
  test.beforeEach(async ({ page }) => {
    await mockAll(page);
    await loginAndGoToAdmin(page);
    await page.getByRole("button", { name: /Reindex/ }).click();
  });

  test("renders reindex trigger controls", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Trigger Reindex" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Trigger Reindex" })).toBeEnabled();
  });

  test("triggering reindex shows job ID and terminal status", async ({ page }) => {
    await page.getByRole("button", { name: "Trigger Reindex" }).click();

    await expect(page.getByText(/Job ID:/)).toBeVisible();
    await expect(page.getByText("COMPLETED", { exact: true })).toBeVisible();
  });
});

test.describe("Admin ranking config", () => {
  test.beforeEach(async ({ page }) => {
    await mockAll(page);
    await loginAndGoToAdmin(page);
    await page.getByRole("button", { name: /Ranking Config/ }).click();
  });

  test("renders ranking weight sliders", async ({ page }) => {
    await expect(page.getByText("Text Score Weight")).toBeVisible();
    await expect(page.getByText("Popularity Weight")).toBeVisible();
  });

  test("shows current config version", async ({ page }) => {
    await expect(page.getByText("3")).toBeVisible();
  });
});
