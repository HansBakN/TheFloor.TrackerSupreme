import { readFile, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import { newDataRoot, startServer } from './isolated-server'

test('published game uses local assets and recovers an unchanged snapshot after restart', async ({ browser }) => {
  const root = await newDataRoot()
  let server: Awaited<ReturnType<typeof startServer>> | undefined
  try {
    server = await startServer(root.path)
    const origin = server.origin
    const port = Number(new URL(origin).port)
    const host = await browser.newContext({ baseURL: origin })
    const audience = await browser.newContext({ baseURL: origin })
    const outbound: string[] = []
    try {
      for (const context of [host, audience]) {
        await context.route('**/*', route => {
          if (new URL(route.request().url()).origin === origin) return route.continue()
          outbound.push(route.request().url())
          return route.abort()
        })
      }
      const request = host.request
      expect((await request.post('/api/local-host')).status()).toBe(204)
      const categoryId = crypto.randomUUID()
      const setId = crypto.randomUUID()
      expect((await request.put('/api/library/categories/' + categoryId, { data: { definition: {
        schemaVersion: 1, id: categoryId, name: 'Restart fixture',
        questions: [{
          id: crypto.randomUUID(), type: 'text', prompt: 'Original snapshot question?', answer: 'Host-only',
        }],
      } } })).ok()).toBeTruthy()
      expect((await request.put('/api/library/sets/' + setId, { data: { definition: {
        schemaVersion: 1, id: setId, name: 'Offline event', categoryIds: [categoryId],
      } } })).ok()).toBeTruthy()
      expect((await request.post('/api/library/sets/' + setId + '/activate')).ok()).toBeTruthy()

      const display = await audience.newPage()
      const dashboard = await host.newPage()
      await Promise.all([display.goto('/display'), dashboard.goto('/host')])
      await dashboard.getByLabel('Player A', { exact: true }).fill('Martin')
      await dashboard.getByLabel('Player B', { exact: true }).fill('Thomas')
      await dashboard.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
      await dashboard.getByRole('combobox', { name: 'Starting player', exact: true }).selectOption('a')
      await dashboard.getByRole('button', { name: 'Prepare duel', exact: true }).click()
      await dashboard.getByRole('button', { name: 'Start', exact: true }).click()
      await expect(display.getByText('Original snapshot question?')).toBeVisible()

      const file = join(root.path, 'library', 'categories', categoryId, 'category.yaml')
      const original = await readFile(file, 'utf8')
      await writeFile(file, original.replace('Original snapshot question?', 'Changed in the library?'))
      await expect.poll(async () => (await (await request.get('/api/library')).json()).categories[0].definition.questions[0].prompt)
        .toBe('Changed in the library?')
      await expect(display.getByText('Original snapshot question?')).toBeVisible()

      const before = await (await audience.request.get('/api/display/state')).json()
      await server.stop()
      server = undefined
      // The downtime is deliberately longer than the clock precision.
      await new Promise(resolve => setTimeout(resolve, 900))
      server = await startServer(root.path, port)
      await expect(display.getByText('Paused', { exact: true })).toBeVisible()
      await expect(dashboard.getByRole('button', { name: /Resume/ })).toBeEnabled()
      const after = await (await audience.request.get('/api/display/state')).json()
      expect(after.id).toBe(before.id)
      expect(after.question.id).toBe(before.question.id)
      expect(after.question.prompt).toBe('Original snapshot question?')
      expect(after.bMilliseconds).toBe(45_000)
      expect(Math.abs(after.aMilliseconds - before.aMilliseconds)).toBeLessThan(800)
      expect((await (await request.get('/api/library')).json()).activeSetId).toBe(setId)
      expect(outbound).toEqual([])
    } finally {
      await host.close()
      await audience.close()
    }
  } finally {
    await server?.stop()
    await root.remove()
  }
})
