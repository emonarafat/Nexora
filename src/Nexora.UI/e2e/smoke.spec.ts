import { expect, test } from "@playwright/test";

test("search shell and admin route render", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Nexora UI" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Search" })).toBeVisible();

  await page.getByRole("link", { name: "Admin" }).click();
  await expect(page).toHaveURL(/\/admin$/);
  await expect(page.getByRole("heading", { name: "Admin Dashboard" })).toBeVisible();
});
