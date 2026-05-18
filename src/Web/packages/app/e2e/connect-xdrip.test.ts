import { expect, test } from "@playwright/test";

test.describe("/connect/xdrip trampoline", () => {
	test("renders without authentication", async ({ page }) => {
		const response = await page.goto("/connect/xdrip");
		expect(response?.status()).toBe(200);
		await expect(page).toHaveURL(/\/connect\/xdrip$/);
	});

	test("shows redirecting state then falls back to manual UI", async ({ page }) => {
		await page.goto("/connect/xdrip");
		// Initial "redirecting" state visible
		await expect(page.getByText("Opening xDrip+")).toBeVisible();
		// After 2 seconds, fallback UI appears
		await expect(page.getByText("xDrip+ didn't open automatically")).toBeVisible({
			timeout: 5_000,
		});
	});

	test("fallback UI shows the instance URL", async ({ page }) => {
		await page.goto("/connect/xdrip");
		// Wait for fallback
		await expect(page.getByText("xDrip+ didn't open automatically")).toBeVisible({
			timeout: 5_000,
		});
		// The code block should contain the origin URL
		const codeBlock = page.locator("code").first();
		await expect(codeBlock).toContainText("http");
	});
});
