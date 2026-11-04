import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'

test('a category can persist and reload a 20-question image library', async ({ page }) => {
  const image = fileURLToPath(new URL(
    '../../../examples/demo/categories/5e5506b3-06fa-56a7-b977-c6b173fc9c4d/images/blue.png',
    import.meta.url,
  ))
  await page.goto('/categories')
  await page.getByLabel('New category name', { exact: true }).fill('Image collection')
  await page.getByRole('button', { name: 'Create category', exact: true }).click()
  await expect(page.getByLabel('Category name', { exact: true })).toHaveValue('Image collection')

  for (let number = 1; number <= 20; number++) {
    await page.getByRole('button', { name: 'Add question', exact: true }).click()
    await page.getByRole('combobox', { name: 'Question type' }).selectOption('image')
    await page.getByLabel('Prompt', { exact: true }).fill('Image question ' + number + '?')
    await page.getByLabel('Correct answer', { exact: true }).fill('Blue')
    await page.getByLabel('Question media', { exact: true }).setInputFiles(image)
    await expect(page.getByText(/media\/.*\.png/)).toBeVisible()
  }
  const saved = page.waitForResponse(response =>
    response.request().method() === 'PUT' && response.url().includes('/api/library/categories/'))
  await page.getByRole('button', { name: 'Save category', exact: true }).click()
  expect((await saved).ok()).toBeTruthy()
  await page.reload()
  await expect(page.getByRole('heading', { name: /Questions · 20/ })).toBeVisible()
  await expect(page.getByRole('button', { name: '20. Image question 20?', exact: true })).toBeVisible()
})
