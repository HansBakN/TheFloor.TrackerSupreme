import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { newDataRoot, startServer } from './isolated-server'

const clip = (name: string) => fileURLToPath(new URL(
  `../../../examples/demo/categories/8c1d2f4a-6b3e-5a79-9d10-4e2f7a6b1c35/video/${name}.webm`, import.meta.url))

/** Rewrites the EBML Duration element so a real clip claims to be `seconds` long. */
function withDuration(webm: Buffer, seconds: number) {
  const copy = Buffer.from(webm)
  const at = copy.indexOf(Buffer.from([0x44, 0x89]))
  expect(at).toBeGreaterThan(0)
  const size = copy[at + 2] & 0x7f
  if (size === 8) copy.writeDoubleBE(seconds * 1000, at + 3)
  else copy.writeFloatBE(seconds * 1000, at + 3)
  return copy
}

const label = 'Quiz question video clip'
const state = (page: Page) => page.getByLabel(label).evaluate((video: HTMLVideoElement) => ({
  loop: video.loop, muted: video.muted, controls: video.controls, paused: video.paused,
  ready: video.readyState, time: video.currentTime, duration: video.duration,
}))

test('the editor accepts short clips, previews them as muted loops and rejects bad video', async ({ page }) => {
  await page.goto('/categories')
  await page.getByLabel('New category name', { exact: true }).fill('Video clips')
  await page.getByRole('button', { name: 'Create category', exact: true }).click()
  await page.getByRole('button', { name: 'Add question', exact: true }).click()
  await page.getByRole('combobox', { name: 'Question type' }).selectOption('video')
  await expect(page.getByText('at most 5 seconds and 8 MiB')).toBeVisible()
  await page.getByLabel('Prompt', { exact: true }).fill('Name the pulsing color.')
  await page.getByLabel('Correct answer', { exact: true }).fill('Red')

  const media = page.getByLabel('Question media', { exact: true })
  await expect(media).toHaveAttribute('accept', /video\/webm/)
  await media.setInputFiles({ name: 'broken.webm', mimeType: 'video/webm', buffer: Buffer.from('not a video at all') })
  await expect(page.getByRole('alert')).toContainText(/signature|MIME|duration/i)
  await media.setInputFiles({
    name: 'long.webm', mimeType: 'video/webm', buffer: withDuration(await readFile(clip('red-pulse')), 30),
  })
  await expect(page.getByRole('alert')).toContainText('at most 5 seconds')
  await media.setInputFiles({ name: 'red.png', mimeType: 'image/png', buffer: await readFile(clip('red-pulse')) })
  await expect(page.getByRole('alert')).toBeVisible()
  await expect(page.getByText(/media\/.*\.webm/)).toHaveCount(0)

  await media.setInputFiles(clip('red-pulse'))
  await expect(page.getByText(/media\/.*\.webm/)).toBeVisible()
  await page.getByRole('button', { name: 'Preview question', exact: true }).click()
  await expect.poll(async () => (await state(page)).ready).toBeGreaterThanOrEqual(2)
  const preview = await state(page)
  expect(preview).toMatchObject({ loop: true, muted: true, controls: false })
  expect(preview.duration).toBeGreaterThan(1.5)
  expect(preview.duration).toBeLessThanOrEqual(5.1)

  const saved = page.waitForResponse(response =>
    response.request().method() === 'PUT' && response.url().includes('/api/library/categories/'))
  await page.getByRole('button', { name: 'Save category', exact: true }).click()
  expect((await saved).ok()).toBeTruthy()
  await page.reload()
  await expect(page.getByRole('button', { name: /1\. Name the pulsing color\./ })).toBeVisible()
  await expect(page.getByText(/video · Enabled/)).toBeVisible()
})

test('a duel keeps video hidden until Start, then loops it on the audience display', async ({ browser }, testInfo) => {
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
    const category = { schemaVersion: 1, id: categoryId, name: 'Video duel', questions: [] as object[] }
    const created = await request.put(url, { data: { definition: category } })
    expect(created.ok()).toBeTruthy()
    const upload = await request.post(url + '/media', {
      multipart: { file: { name: 'red.webm', mimeType: 'video/webm', buffer: await readFile(clip('red-pulse')) } },
    })
    expect(upload.ok()).toBeTruthy()
    const { path: mediaPath } = await upload.json()
    expect(mediaPath).toMatch(/\.webm$/)
    const bad = await request.post(url + '/media', {
      multipart: { file: { name: 'long.webm', mimeType: 'video/webm', buffer: withDuration(await readFile(clip('red-pulse')), 60) } },
    })
    expect(bad.status()).toBeGreaterThanOrEqual(400)
    const questions = [1, 2].map(number => ({
      id: crypto.randomUUID(), type: 'video', prompt: 'Video question ' + number,
      media: mediaPath, answer: 'Red', alternatives: [], enabled: true,
    }))
    expect((await request.put(url, { data: { definition: { ...category, questions }, revision: (await created.json()).revision } })).ok()).toBeTruthy()
    expect((await request.put(origin + '/api/library/sets/' + setId, { data: { definition: {
      schemaVersion: 1, id: setId, name: 'Video event', categoryIds: [categoryId],
    } } })).ok()).toBeTruthy()
    expect((await request.post(origin + '/api/library/sets/' + setId + '/activate')).ok()).toBeTruthy()

    const dashboard = await host.newPage()
    const display = await audience.newPage()
    await Promise.all([dashboard.goto(origin + '/host'), display.goto(origin + '/display')])
    await dashboard.getByLabel('Player A', { exact: true }).fill('Martin')
    await dashboard.getByLabel('Player B', { exact: true }).fill('Thomas')
    await dashboard.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
    await dashboard.getByLabel('Question order').selectOption('ordered')
    await dashboard.getByRole('button', { name: 'Prepare duel', exact: true }).click()
    await expect(dashboard.getByLabel(label)).toBeVisible()
    await expect(display.getByText('Ready to start', { exact: true })).toBeVisible()
    await expect(display.getByLabel(label)).toHaveCount(0)
    const ready = (await (await request.get(origin + '/api/host/state')).json()).session.duel
    const publicMedia = origin + '/api/display/' + ready.id + '/media/' + questions[0].id
    expect((await audience.request.get(publicMedia)).status()).toBe(400)

    await dashboard.getByRole('button', { name: 'Start', exact: true }).click()
    const video = display.getByLabel(label)
    await expect(video).toBeVisible()
    await expect(video).toHaveJSProperty('loop', true)
    await expect(video).toHaveJSProperty('muted', true)
    const ranged = await audience.request.get(publicMedia, { headers: { Range: 'bytes=0-99' } })
    expect(ranged.status()).toBe(206)
    expect(ranged.headers()['content-type']).toBe('video/webm')

    // The clip must actually play and then wrap around: currentTime advances and later drops back.
    await expect.poll(async () => (await state(display)).time, { timeout: 10_000 }).toBeGreaterThan(0.3)
    let previous = 0
    let wrapped = false
    for (let sample = 0; sample < 60 && !wrapped; sample++) {
      const { time, paused } = await state(display)
      expect(paused).toBe(false)
      if (time < previous) wrapped = true
      previous = time
      await display.waitForTimeout(150)
    }
    expect(wrapped).toBe(true)
    await display.screenshot({ path: testInfo.outputPath('video-question-1920x1080.png') })

    await dashboard.getByRole('button', { name: /^Correct / }).click()
    await expect(display.getByText('Video question 2')).toBeVisible()
    await expect(display.getByLabel(label)).toBeVisible()
  } finally {
    await host.close()
    await audience.close()
    await server?.stop()
    await root.remove()
  }
})
