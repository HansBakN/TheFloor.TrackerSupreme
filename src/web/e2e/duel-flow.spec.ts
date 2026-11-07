import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'
import type { AudienceDuel } from '../src/contracts'
import { newDataRoot, startServer } from './isolated-server'

test('prepared images stay private, pass waits the full interval, and an unfinished duel returns to setup', async ({ browser }, testInfo) => {
  const root = await newDataRoot()
  let server: Awaited<ReturnType<typeof startServer>> | undefined
  const host = await browser.newContext({ viewport: { width: 1280, height: 720 } })
  const audience = await browser.newContext({ viewport: { width: 1920, height: 1080 } })
  try {
    server = await startServer(root.path)
    const origin = server.origin
    const request = host.request
    expect((await request.post(origin + '/api/local-host')).status()).toBe(204)
    const categoryId = crypto.randomUUID()
    const setId = crypto.randomUUID()
    const url = origin + '/api/library/categories/' + categoryId
    const category = { schemaVersion: 1, id: categoryId, name: 'Image duel regression', questions: [] as object[] }
    const saved = await request.put(url, { data: { definition: category } })
    expect(saved.ok()).toBeTruthy()
    const image = fileURLToPath(new URL('../../../examples/demo/categories/5e5506b3-06fa-56a7-b977-c6b173fc9c4d/images/blue.png', import.meta.url))
    const upload = await request.post(url + '/media', {
      multipart: { file: { name: 'blue.png', mimeType: 'image/png', buffer: await readFile(image) } },
    })
    expect(upload.ok()).toBeTruthy()
    const { path: media } = await upload.json()
    const questions = [1, 2, 3].map(number => ({
      id: crypto.randomUUID(), type: 'image', prompt: 'Private image question ' + number,
      media, answer: 'Host secret', alternatives: [], enabled: true,
    }))
    expect((await request.put(url, { data: { definition: { ...category, questions }, revision: (await saved.json()).revision } })).ok()).toBeTruthy()
    expect((await request.put(origin + '/api/library/sets/' + setId, { data: { definition: {
      schemaVersion: 1, id: setId, name: 'Image event', categoryIds: [categoryId],
    } } })).ok()).toBeTruthy()
    expect((await request.post(origin + '/api/library/sets/' + setId + '/activate')).ok()).toBeTruthy()

    const dashboard = await host.newPage()
    const display = await audience.newPage()
    const frames: AudienceDuel[] = []
    const mediaRequests: string[] = []
    display.on('request', request => { if (request.url().includes('/media/')) mediaRequests.push(request.url()) })
    display.on('websocket', socket => socket.on('framereceived', ({ payload }) => {
      for (const text of payload.toString().split('\u001e').filter(Boolean)) {
        const frame = JSON.parse(text)
        if (frame.target === 'duel' && frame.arguments[0]) frames.push(frame.arguments[0])
      }
    }))
    await Promise.all([dashboard.goto(origin + '/host'), display.goto(origin + '/display')])
    await dashboard.getByLabel('Player A', { exact: true }).fill('Martin')
    await dashboard.getByLabel('Player B', { exact: true }).fill('Thomas')
    await dashboard.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
    await dashboard.getByLabel('Starting player').selectOption('a')
    await dashboard.getByLabel('Question order').selectOption('ordered')
    await dashboard.getByLabel('Pass penalty (seconds)').fill('3')
    await dashboard.getByRole('button', { name: 'Prepare duel', exact: true }).click()
    await expect(dashboard.getByAltText('Quiz question image')).toBeVisible()
    await expect(display.getByText('Ready to start', { exact: true })).toBeVisible()
    await display.reload()
    await expect(display.getByText('Ready to start', { exact: true })).toBeVisible()
    await expect(display.getByAltText('Quiz question image')).toHaveCount(0)
    await expect(display.getByText(questions[0].prompt)).toHaveCount(0)
    const ready = (await (await request.get(origin + '/api/host/state')).json()).session.duel
    const publicMedia = (id: string) => origin + '/api/display/' + ready.id + '/media/' + id
    expect((await audience.request.get(publicMedia(questions[0].id))).status()).toBe(400)
    expect((await audience.request.get(origin + ready.question.mediaUrl)).status()).toBe(401)
    expect((await (await audience.request.get(origin + '/api/display/state')).json()).question).toBeNull()
    expect(frames.filter(frame => frame.status === 'ready').every(frame => frame.question === null)).toBe(true)
    expect(mediaRequests).toEqual([])

    await dashboard.getByRole('button', { name: 'Start', exact: true }).click()
    await expect(display.getByAltText('Quiz question image')).toBeVisible()
    await expect(display.getByText(questions[0].prompt)).toBeVisible()
    expect((await audience.request.get(publicMedia(questions[0].id))).status()).toBe(200)
    const passResponse = dashboard.waitForResponse(response => response.url().endsWith('/api/host/command') &&
      response.request().postDataJSON().action === 'pass')
    const passStarted = performance.now()
    await dashboard.getByRole('button', { name: /^Pass / }).click()
    const passed: AudienceDuel = (await (await passResponse).json()).session.duel
    // The response is sampled after persistence and broadcasting, which can exceed 100 ms on CI.
    // Account for the measured round trip while still requiring the full configured lockout.
    const passRoundTrip = performance.now() - passStarted
    expect(passed.passMilliseconds).toBeGreaterThan(0)
    expect(passed.passMilliseconds).toBeLessThanOrEqual(3000)
    expect(passed.passMilliseconds + passRoundTrip).toBeGreaterThanOrEqual(2998)
    expect(passed.question).toBeNull()
    await expect(display.getByRole('heading', { name: 'Pass lockout', exact: true })).toBeVisible()
    await expect(display.getByAltText('Quiz question image')).toHaveCount(0)
    await expect(dashboard.getByRole('button', { name: /^Correct / })).toBeDisabled()
    await expect(dashboard.getByRole('button', { name: /^Pass / })).toBeDisabled()
    expect((await audience.request.get(publicMedia(questions[1].id))).status()).toBe(400)
    await display.screenshot({ path: testInfo.outputPath('pass-lockout-1920x1080.png') })
    await expect(display.getByText(questions[1].prompt)).toBeVisible()
    await expect(dashboard.getByRole('button', { name: /^Correct / })).toBeEnabled()
    const revealed = frames.find(frame => frame.question?.id === questions[1].id)!
    expect(revealed).toBeTruthy()
    expect(revealed.sampledAt - passed.sampledAt).toBeGreaterThanOrEqual(passed.passMilliseconds - 2)
    expect(passed.aMilliseconds - revealed.aMilliseconds).toBeGreaterThanOrEqual(passed.passMilliseconds - 2)
    expect(passed.aMilliseconds - revealed.aMilliseconds).toBeLessThan(4000)
    expect(revealed.bMilliseconds).toBe(45_000)
    expect(frames.filter(frame => frame.passMilliseconds > 0).every(frame => frame.question === null)).toBe(true)
    expect(JSON.stringify(frames)).not.toContain('Host secret')

    const end = dashboard.getByRole('button', { name: 'End duel and return to setup', exact: true })
    dashboard.once('dialog', dialog => dialog.dismiss())
    await end.click()
    await expect(end).toBeVisible()
    dashboard.once('dialog', dialog => dialog.accept())
    await end.click()
    await expect(dashboard.getByRole('button', { name: 'Prepare duel', exact: true })).toBeEnabled()
    await expect(display.getByRole('heading', { name: 'Waiting for the host' })).toBeVisible()
    expect(await (await audience.request.get(origin + '/api/display/state')).json()).toBeNull()
    await dashboard.reload()
    await expect(dashboard.getByRole('button', { name: 'Prepare duel', exact: true })).toBeVisible()
    await dashboard.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
    await expect(dashboard.getByRole('button', { name: 'Prepare duel', exact: true })).toBeEnabled()
  } finally {
    await host.close()
    await audience.close()
    await server?.stop()
    await root.remove()
  }
})
