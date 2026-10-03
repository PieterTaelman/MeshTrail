import { expect, test } from '@playwright/test';

test('create a sample and see it in the list', async ({ page }) => {
  const name = `e2e-${Date.now()}`;

  await page.goto('/samples/new');
  await page.getByLabel('Name').fill(name);
  await page.getByRole('button', { name: 'Save' }).click();
  await expect(page.getByRole('heading', { name: 'Edit sample' })).toBeVisible();

  await page.goto('/samples');
  await page.getByLabel('Search samples').fill(name);
  await expect(page.getByRole('link', { name })).toBeVisible();
});
