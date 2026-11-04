import { expect, test } from '@playwright/test'

test('audience receives a restricted projection and observes server-side expiration', async ({ request, playwright, baseURL }) => {
  const audience = await playwright.request.newContext({ baseURL })
  try {
    expect((await request.post('/api/local-host')).status()).toBe(204)
    const categoryId = crypto.randomUUID()
    const setId = crypto.randomUUID()
    const category = {
      schemaVersion: 1, id: categoryId, name: 'Test geography',
      questions: [{
        id: crypto.randomUUID(), type: 'text', prompt: 'Public prompt?',
        answer: 'Secret answer', alternatives: ['Secret alternative'], notes: 'Secret notes', enabled: true,
      }],
    }
    const set = { schemaVersion: 1, id: setId, name: 'Test set', categoryIds: [categoryId] }
    expect((await request.put(`/api/library/categories/${categoryId}`, { data: { definition: category } })).ok()).toBeTruthy()
    expect((await request.put(`/api/library/sets/${setId}`, { data: { definition: set } })).ok()).toBeTruthy()
    expect((await request.post(`/api/library/sets/${setId}/activate`)).ok()).toBeTruthy()
    const readyResponse = await request.post('/api/host/duel', {
      data: { playerA: 'Martin', playerB: 'Thomas', categoryId, seconds: 1, starter: 'a', order: 'ordered' },
    })
    expect(readyResponse.ok()).toBeTruthy()
    const { session } = await readyResponse.json()
    expect(session.answer).toBe('Secret answer')
    expect((await audience.get('/api/host/state')).status()).toBe(401)
    const command = { sessionId: session.duel.id, version: session.duel.version, commandId: crypto.randomUUID(), action: 'start' }
    expect((await audience.post('/api/host/command', { data: command })).status()).toBe(401)
    const publicText = await (await audience.get('/api/display/state')).text()
    expect(publicText).toContain('Public prompt?')
    expect(publicText).not.toContain('Secret')
    expect((await request.post('/api/host/command', { data: command })).ok()).toBeTruthy()
    await expect.poll(async () => (await (await audience.get('/api/display/state')).json()).status).toBe('finished')
    const finished = await (await audience.get('/api/display/state')).json()
    expect(finished.winner).toBe('b')
    expect(finished.aMilliseconds).toBe(0)
    expect(finished.bMilliseconds).toBe(1000)
    expect((await request.post('/api/host/command', { data: {
      ...command, version: finished.version, commandId: crypto.randomUUID(), action: 'return',
    } })).ok()).toBeTruthy()
    expect(await (await audience.get('/api/display/state')).json()).toBeNull()
  } finally {
    await audience.dispose()
  }
})
