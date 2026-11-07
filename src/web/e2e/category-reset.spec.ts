import { readFile, readdir } from 'node:fs/promises'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import type { Library } from '../src/contracts'
import { newDataRoot, startServer } from './isolated-server'

test('reset requires confirmation, backs up every category, preserves sets and the duel, and persists after restart', async ({ browser }, testInfo) => {
  const root = await newDataRoot()
  let server: Awaited<ReturnType<typeof startServer>> | undefined
  const host = await browser.newContext({ viewport: { width: 1280, height: 720 } })
  const audience = await browser.newContext()
  try {
    server = await startServer(root.path)
    const origin = server.origin
    const api = host.request
    expect((await api.post(origin + '/api/local-host')).status()).toBe(204)
    const categoryIds = [crypto.randomUUID(), crypto.randomUUID()]
    const setIds = [crypto.randomUUID(), crypto.randomUUID()]
    for (const [index, id] of categoryIds.entries()) {
      expect((await api.put(origin + '/api/library/categories/' + id, { data: { definition: {
        schemaVersion: 1, id, name: 'Reset category ' + index,
        questions: [{ id: crypto.randomUUID(), type: 'text', prompt: 'Snapshot question ' + index,
          answer: 'Secret', alternatives: [], enabled: true }],
      } } })).ok()).toBeTruthy()
    }
    const uploaded = await api.post(origin + '/api/library/categories/' + categoryIds[0] + '/media', {
      multipart: { file: { name: 'test.gif', mimeType: 'image/gif', buffer: Buffer.from('GIF89atest') } },
    })
    expect(uploaded.ok()).toBeTruthy()
    const media = (await uploaded.json()).path
    for (const [index, id] of setIds.entries()) {
      expect((await api.put(origin + '/api/library/sets/' + id, { data: { definition: {
        schemaVersion: 1, id, name: 'Reset event ' + index, categoryIds,
      } } })).ok()).toBeTruthy()
    }
    expect((await api.post(origin + '/api/library/sets/' + setIds[0] + '/activate')).ok()).toBeTruthy()
    const prepared = await api.post(origin + '/api/host/duel', { data: {
      playerA: 'A', playerB: 'B', categoryId: categoryIds[0], seconds: 600,
      passSeconds: 3, starter: 'a', order: 'ordered',
    } })
    expect(prepared.ok()).toBeTruthy()
    const session = (await prepared.json()).session.duel
    const display = await audience.newPage()
    await display.goto(origin + '/display')
    const page = await host.newPage()
    await page.goto(origin + '/categories/' + categoryIds[0])
    await expect(page.getByLabel('Category name', { exact: true })).toHaveValue('Reset category 0')
    await page.getByLabel('Category name', { exact: true }).fill('Unsaved edits')
    await page.getByLabel('Search categories', { exact: true }).fill('Reset category 0')
    await expect(page.getByRole('link', { name: 'Reset category 1', exact: true })).toHaveCount(0)
    const reset = page.getByRole('button', { name: 'Reset all categories', exact: true })
    page.once('dialog', async dialog => {
      expect(dialog.message()).toContain('Reset all 2 categories')
      expect(dialog.message()).toContain('Reset category 1')
      expect(dialog.message()).toContain('Reset event 0')
      expect(dialog.message()).toContain('Reset event 1')
      await dialog.dismiss()
    })
    await reset.click()
    await expect(page.getByLabel('Category name', { exact: true })).toHaveValue('Unsaved edits')
    const before: Library = await (await api.get(origin + '/api/library')).json()
    const confirmation = {
      categories: Object.fromEntries(before.categories.map(c => [c.definition.id, c.revision])),
      sets: Object.fromEntries(before.sets.map(s => [s.definition.id, s.revision])),
    }
    expect((await audience.request.post(origin + '/api/library/categories/reset', { data: confirmation })).status()).toBe(401)
    const edited = before.categories[1]
    expect((await api.put(origin + '/api/library/categories/' + edited.definition.id, { data: {
      definition: { ...edited.definition, description: 'External change' }, revision: edited.revision,
    } })).ok()).toBeTruthy()
    expect((await api.post(origin + '/api/library/categories/reset', { data: confirmation })).status()).toBe(409)
    expect((await readdir(join(root.path, 'library/categories'))).length).toBe(2)
    const reloaded = page.waitForResponse(response => response.url().endsWith('/api/library/reload'))
    await page.getByRole('button', { name: 'Reload files', exact: true }).click()
    expect((await reloaded).ok()).toBeTruthy()
    const resetResponse = page.waitForResponse(response => response.url().endsWith('/categories/reset'))
    page.once('dialog', dialog => dialog.accept())
    await reset.click()
    const response = await resetResponse
    expect(response.ok()).toBeTruthy()
    const { backupDirectory } = await response.json()
    await expect(page).toHaveURL(origin + '/categories')
    await expect(page.getByRole('status')).toContainText('All categories reset.')
    await expect(reset).toBeDisabled()
    await expect(page.getByText('No matching categories.')).toBeVisible()
    await page.screenshot({ path: testInfo.outputPath('categories-reset-1280x720.png') })
    const after: Library = await (await api.get(origin + '/api/library')).json()
    expect(after.categories).toEqual([])
    expect(after.errors).toEqual([])
    expect(after.sets).toHaveLength(2)
    expect(after.sets.every(s => !s.definition.categoryIds.length)).toBe(true)
    expect(after.activeSetId).toBe(setIds[0])
    expect(await readdir(join(root.path, 'library/categories'))).toEqual([])
    expect(await readFile(join(root.path, backupDirectory, 'categories', categoryIds[0], media), 'utf8')).toBe('GIF89atest')
    expect(await readFile(join(root.path, backupDirectory, 'sets', setIds[0], 'set.yaml'), 'utf8')).toContain(categoryIds[0])
    const untouched = (await (await api.get(origin + '/api/host/state')).json()).session.duel
    expect(untouched.id).toBe(session.id)
    expect(untouched.version).toBe(session.version)
    expect(untouched.question.prompt).toBe('Snapshot question 0')
    expect((await api.post(origin + '/api/host/command', { data: {
      sessionId: session.id, commandId: crypto.randomUUID(), version: untouched.version, action: 'start',
    } })).ok()).toBeTruthy()
    await expect(display.getByText('Snapshot question 0', { exact: true })).toBeVisible()
    await server.stop()
    server = await startServer(root.path)
    expect((await api.post(server.origin + '/api/local-host')).status()).toBe(204)
    expect((await (await api.get(server.origin + '/api/library')).json()).categories).toEqual([])
    const recovered = (await (await api.get(server.origin + '/api/host/state')).json()).session.duel
    expect(recovered.id).toBe(session.id)
    expect(recovered.status).toBe('paused')
    expect(recovered.question.prompt).toBe('Snapshot question 0')
    await page.goto(server.origin + '/categories')
    await expect(page.getByRole('button', { name: 'Reset all categories', exact: true })).toBeDisabled()
  } finally {
    await host.close()
    await audience.close()
    await server?.stop()
    await root.remove()
  }
})
