import { readFile, unlink, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import { wave } from './media-fixtures'

test('rehearses on a separate display, selects one audio output, and saves reusable setup', async ({ page, request, browser }, testInfo) => {
  expect((await request.post('/api/local-host')).status()).toBe(204)
  const categoryId = crypto.randomUUID()
  const setId = crypto.randomUUID()
  const questionId = crypto.randomUUID()
  const category = { schemaVersion: 1, id: categoryId, name: 'Sound check', questions: [] }
  const initial = await request.put('/api/library/categories/' + categoryId, { data: { definition: category } })
  expect(initial.ok()).toBeTruthy()
  const uploaded = await request.post('/api/library/categories/' + categoryId + '/media', {
    multipart: { file: { name: 'silence.wav', mimeType: 'audio/wav', buffer: wave() } },
  })
  expect(uploaded.ok()).toBeTruthy()
  const relativeMedia = (await uploaded.json()).path
  expect((await request.put('/api/library/categories/' + categoryId, { data: {
    revision: (await initial.json()).revision,
    definition: { ...category, questions: [
      { id: questionId, type: 'audio', prompt: 'Listen to the test clip.', media: relativeMedia, answer: 'Private rehearsal answer' },
      { id: crypto.randomUUID(), type: 'text', prompt: 'Disabled question', answer: 'Test', enabled: false },
    ] },
  } })).ok()).toBeTruthy()
  expect((await request.put('/api/library/sets/' + setId, { data: { definition: {
    schemaVersion: 1, id: setId, name: 'Rehearsal set', categoryIds: [categoryId],
  } } })).ok()).toBeTruthy()
  expect((await request.post('/api/library/sets/' + setId + '/activate')).ok()).toBeTruthy()

  await page.goto('/host')
  await page.getByLabel('Starting time per player (seconds)').fill('55')
  await page.getByLabel('Pass penalty (seconds)').fill('4')
  await page.getByRole('combobox', { name: 'Starting player' }).selectOption('b')
  await page.getByRole('combobox', { name: 'Question order' }).selectOption('ordered')
  await page.getByText('Duel presets', { exact: true }).click()
  await page.getByLabel('Preset name').fill('Long round')
  await page.getByRole('button', { name: 'Save current settings' }).click()
  await expect(page.getByText('Preset saved.')).toBeVisible()
  await page.reload()
  await page.getByText('Duel presets', { exact: true }).click()
  await page.getByRole('combobox', { name: 'Saved preset' }).selectOption('Long round')
  await page.getByRole('button', { name: 'Apply preset' }).click()
  await expect(page.getByLabel('Starting time per player (seconds)')).toHaveValue('55')
  await expect(page.getByLabel('Pass penalty (seconds)')).toHaveValue('4')
  await expect(page.getByRole('combobox', { name: 'Starting player' })).toHaveValue('b')
  await expect(page.getByRole('combobox', { name: 'Question order' })).toHaveValue('ordered')
  await page.getByRole('button', { name: 'Delete preset' }).click()
  await expect(page.getByRole('option', { name: 'Long round' })).toHaveCount(0)

  await page.getByLabel('Player A', { exact: true }).fill('Alex')
  await page.getByLabel('Player B', { exact: true }).fill('Sam')
  await page.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
  await page.getByRole('button', { name: 'Prepare duel' }).click()
  await expect(page.getByRole('button', { name: 'Start', exact: true })).toBeEnabled()
  const before = (await (await request.get('/api/host/state')).json()).session.duel

  const first = await browser.newContext()
  const second = await browser.newContext()
  try {
    const firstDisplay = await first.newPage()
    const secondDisplay = await second.newPage()
    await firstDisplay.goto('/display')
    await expect(firstDisplay.locator('audio')).toHaveCount(1)
    await secondDisplay.goto('/display')
    await expect(page.getByText('2 displays connected', { exact: true })).toBeVisible()
    await expect(firstDisplay.locator('audio')).toHaveCount(1)
    await expect(secondDisplay.locator('audio')).toHaveCount(0)

    await page.getByText('Pair displays and choose audio', { exact: true }).click()
    const qr = page.getByRole('img', { name: 'QR code for the display link' })
    await expect(qr).toBeVisible()
    await expect.poll(() => qr.evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0)
    await expect(page.getByLabel('Display link', { exact: true })).toHaveValue(/\/display$/)
    const secondName = (await secondDisplay.getByText(/Quiz duel · DISPLAY/).textContent())!.split(' · ')[1]
    await page.getByRole('combobox', { name: 'Audio output display' }).selectOption({ label: secondName })
    await expect(secondDisplay.locator('audio')).toHaveCount(1)
    await expect(firstDisplay.locator('audio')).toHaveCount(0)
    expect((await first.request.put('/api/host/audio-display', { data: { displayId: null } })).status()).toBe(401)

    await page.getByRole('link', { name: 'Rehearsal', exact: true }).click()
    await expect(page.getByText('1 enabled · 1 disabled')).toBeVisible()
    await expect(page.getByText('Below the minimum question count.')).toBeVisible()
    const library = await (await request.get('/api/library')).json()
    const originalMedia = join(library.dataDirectory, 'library', 'categories', categoryId, relativeMedia)
    const originalBytes = await readFile(originalMedia)
    await unlink(originalMedia)
    try {
      await page.getByRole('button', { name: 'Recheck content files' }).click()
      await expect(page.getByText(/Missing media/)).toBeVisible()
    } finally {
      await writeFile(originalMedia, originalBytes)
    }
    await page.getByRole('button', { name: 'Recheck content files' }).click()
    await expect(page.getByText(/Missing media/)).toHaveCount(0)
    await page.getByRole('combobox', { name: 'Rehearsal category' }).selectOption(categoryId)
    await page.getByRole('combobox', { name: 'Rehearsal question' }).selectOption(questionId)
    await page.getByRole('button', { name: 'Show on rehearsal display' }).click()
    const link = await page.getByRole('link', { name: 'Open display link' }).getAttribute('href')
    const rehearsal = await second.newPage()
    await rehearsal.goto(link!)
    await expect(rehearsal.getByText('Rehearsal · clocks off')).toBeVisible()
    await expect(rehearsal.getByText('Listen to the test clip.')).toBeVisible()
    await expect(rehearsal.getByText('Private rehearsal answer')).toHaveCount(0)
    await rehearsal.getByRole('slider', { name: 'Rehearsal volume' }).press('End')
    await expect.poll(() => rehearsal.locator('audio').evaluate((audio: HTMLAudioElement) => audio.volume)).toBe(1)
    await expect.poll(() => rehearsal.locator('audio').evaluate((audio: HTMLAudioElement) => audio.duration)).toBe(1)
    const fullscreen = rehearsal.getByRole('button', { name: 'Toggle fullscreen' })
    await expect(fullscreen).toBeEnabled()
    if (await rehearsal.evaluate(() => document.fullscreenEnabled)) {
      await fullscreen.click()
      await expect.poll(() => rehearsal.evaluate(() => !!document.fullscreenElement)).toBe(true)
      await fullscreen.click()
    }
    await page.screenshot({ path: testInfo.outputPath('event-rehearsal.png'), animations: 'disabled' })
    await page.getByRole('button', { name: 'End rehearsal' }).click()
    await expect(rehearsal.getByText('Rehearsal is waiting for a preview')).toBeVisible()
    await expect(secondDisplay.getByText('Listen to the test clip.')).toBeVisible()
    const after = (await (await request.get('/api/host/state')).json()).session.duel
    expect([after.id, after.version, after.status, after.question.id]).toEqual(
      [before.id, before.version, 'ready', questionId])
  } finally {
    await first.close()
    await second.close()
    const state = (await (await request.get('/api/host/state')).json()).session?.duel
    if (state?.id === before.id && state.status !== 'finished') {
      const ended = await request.post('/api/host/command', { data: {
        sessionId: state.id, commandId: crypto.randomUUID(), version: state.version, action: 'end',
      } })
      if (ended.ok()) {
        const finished = (await ended.json()).session.duel
        await request.post('/api/host/command', { data: {
          sessionId: finished.id, commandId: crypto.randomUUID(), version: finished.version, action: 'return',
        } })
      }
    }
    await request.put('/api/host/audio-display', { data: { displayId: null } })
  }
})
