import { expect, test } from '@playwright/test'

test('the published server serves the frontend and API', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'TheFloor.TrackerSupreme' })).toBeVisible()
  await expect(page.getByRole('status')).toHaveText('Local server ready')
  const response = await page.request.get('/api/unknown')
  expect(response.status()).toBe(404)
})
