import { expect, test } from "@playwright/test";
import type { LoginResponse } from "../src/features/auth/types";

function makeLoginResponse(): LoginResponse {
  return {
    accessToken: "test.jwt.token",
    expiresIn: 3600,
    role: "admin",
  };
}

async function mockAuthApi(page: import("@playwright/test").Page) {
  await page.route("**/api/v1/auth/login", (route) => {
    void route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeLoginResponse()),
    });
  });
}

test("search shell and admin route render", async ({ page }) => {
  await mockAuthApi(page);

  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Nexora UI" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Search" })).toBeVisible();

  // Navigate to admin — should redirect to login
  await page.getByRole("link", { name: "Admin" }).click();
  await expect(page).toHaveURL(/\/login/);

  // Log in via the login form
  await page.getByLabel("Username").fill("admin");
  await page.getByLabel("Password").fill("password");
  await page.getByRole("button", { name: "Sign in" }).click();

  // Should now be on the admin page
  await expect(page).toHaveURL(/\/admin$/);
  await expect(page.getByRole("heading", { name: "Admin Dashboard" })).toBeVisible();
});
