import { readFile, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'

test('host edits are persisted and external edits are detected', async ({ request, playwright, baseURL }) => {
  const audience = await playwright.request.newContext({ baseURL })
  try {
    expect((await audience.get('/api/library')).status()).toBe(401)
    expect((await request.post('/api/local-host')).status()).toBe(204)
    const id = crypto.randomUUID()
    const path = `/api/library/categories/${id}`
    const definition = { schemaVersion: 1, id, name: 'Before external edit', questions: [] }
    const created = await request.put(path, { data: { definition, revision: null } })
    expect(created.ok()).toBeTruthy()
    const { revision } = await created.json()
    const library = await (await request.get('/api/library')).json()
    const file = join(library.dataDirectory, 'library', 'categories', id, 'category.yaml')
    const initial = await readFile(file, 'utf8')
    expect(initial).toContain('Before external edit')

    await writeFile(file, initial.replace('Before external edit', 'After external edit'))
    await expect.poll(async () => {
      const state = await (await request.get('/api/library')).json()
      return state.categories.find((item: { definition: { id: string } }) => item.definition.id === id)?.definition.name
    }).toBe('After external edit')

    const stale = await request.put(path, { data: { definition: { ...definition, name: 'Stale edit' }, revision } })
    expect(stale.status()).toBe(409)
    expect(await readFile(file, 'utf8')).toContain('After external edit')
    expect((await audience.get('/api/library')).status()).toBe(401)
  } finally {
    await audience.dispose()
  }
})
