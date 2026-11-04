import { expect, test } from '@playwright/test'
import { newDataRoot, startServer } from './isolated-server'

test('a set ZIP imports into a clean installation and conflicts require a copy', async ({ page, request, browser }) => {
  expect((await request.post('/api/local-host')).status()).toBe(204)
  const categoryId = crypto.randomUUID()
  const setId = crypto.randomUUID()
  const definition = {
    schemaVersion: 1, id: categoryId, name: 'Portable category', questions: [{
      id: crypto.randomUUID(), type: 'text', prompt: 'Which city?', answer: 'Copenhagen',
    }],
  }
  expect((await request.put('/api/library/categories/' + categoryId, { data: { definition } })).ok()).toBeTruthy()
  expect((await request.put('/api/library/sets/' + setId, { data: { definition: {
    schemaVersion: 1, id: setId, name: 'Portable event', categoryIds: [categoryId],
  } } })).ok()).toBeTruthy()
  await page.goto('/sets')
  await page.getByRole('button', { name: 'Portable event', exact: true }).click()
  const downloaded = page.waitForEvent('download')
  await page.getByRole('link', { name: 'Export set ZIP', exact: true }).click()
  const archive = (await (await downloaded).path())!
  const root = await newDataRoot()
  const server = await startServer(root.path)
  try {
    const context = await browser.newContext({ baseURL: server.origin })
    try {
      const target = await context.newPage()
      await target.goto('/sets')
      const upload = target.getByLabel('Import set ZIP')
      await upload.setInputFiles(archive)
      await expect(target.getByRole('link', { name: 'Portable category', exact: true })).toBeVisible()
      const view = await (await context.request.get('/api/library')).json()
      expect(view.sets[0].definition.id).toBe(setId)
      expect(view.categories[0].definition.id).toBe(categoryId)

      expect((await context.request.put('/api/library/categories/' + categoryId, { data: {
        definition: { ...definition, name: 'Existing edited category' }, revision: view.categories[0].revision,
      } })).ok()).toBeTruthy()
      await upload.setInputFiles(archive)
      await expect(target.getByRole('alert').filter({ hasText: 'conflicts with the existing UUID' })).toBeVisible()
      await target.getByRole('button', { name: 'Import independent copies', exact: true }).click()
      await expect(target.getByText(/1 copied/)).toBeVisible()
      const after = await (await context.request.get('/api/library')).json()
      const original = after.categories.find((entry: { definition: { id: string } }) => entry.definition.id === categoryId)
      expect(original.definition.name).toBe('Existing edited category')
      const copiedSet = after.sets.find((entry: { definition: { id: string } }) => entry.definition.id !== setId)
      expect(copiedSet.definition.categoryIds[0]).not.toBe(categoryId)
      await expect(target.getByRole('link', { name: 'Portable category', exact: true })).toBeVisible()
    } finally {
      await context.close()
    }
  } finally {
    await server.stop()
    await root.remove()
  }
})
