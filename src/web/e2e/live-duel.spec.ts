import { expect, test } from '@playwright/test'
import type { Page, WebSocketRoute } from '@playwright/test'

function capture(page: Page, target: string) {
  const messages: string[] = []
  page.on('websocket', socket => {
    if (!socket.url().includes('/live?')) return
    socket.on('framereceived', ({ payload }) => {
      for (const frame of payload.toString().split('\u001e')) {
        if (frame.includes('"target":"' + target + '"')) messages.push(frame)
      }
    })
  })
  return messages
}

test('the host and independent display receive live role-specific updates', async ({ page, browser, request }, testInfo) => {
  await page.setViewportSize({ width: 1280, height: 720 })
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } })
  const display = await context.newPage()
  let socket: WebSocketRoute | undefined
  let displayConnections = 0
  const publicFrames: string[] = []
  await display.routeWebSocket(/\/live\?role=audience/, route => {
    const server = route.connectToServer()
    socket = route
    displayConnections++
    server.onMessage(message => {
      publicFrames.push(message.toString())
      route.send(message)
    })
  })
  const hostFrames = capture(page, 'hostDuel')
  try {
    expect((await request.post('/api/local-host')).status()).toBe(204)
    const categoryId = crypto.randomUUID()
    const setId = crypto.randomUUID()
    const category = {
      schemaVersion: 1, id: categoryId, name: 'Live geography',
      questions: ['First question?', 'Second question?', 'Third question?', 'Fourth question?'].map(prompt => ({
        id: crypto.randomUUID(), type: 'text', prompt, enabled: true,
        answer: 'Private answer', alternatives: ['Private alternative'], notes: 'Private notes',
      })),
    }
    const set = { schemaVersion: 1, id: setId, name: 'Live set', categoryIds: [categoryId] }
    expect((await request.put('/api/library/categories/' + categoryId, { data: { definition: category } })).ok()).toBeTruthy()
    expect((await request.put('/api/library/sets/' + setId, { data: { definition: set } })).ok()).toBeTruthy()
    expect((await request.post('/api/library/sets/' + setId + '/activate')).ok()).toBeTruthy()

    await Promise.all([page.goto('/host'), display.goto('/display')])
    const previous = page.getByRole('button', { name: 'Return to setup' })
    if (await previous.isVisible()) await previous.click()
    await expect(page.getByText('1 display connected', { exact: true })).toBeVisible()
    const secondDisplay = await context.newPage()
    await secondDisplay.goto('/display')
    await expect(page.getByText('2 displays connected', { exact: true })).toBeVisible()
    await secondDisplay.close()
    await expect(page.getByText('1 display connected', { exact: true })).toBeVisible()
    await page.getByLabel('Player A', { exact: true }).fill('Martin')
    await page.getByLabel('Player B', { exact: true }).fill('Thomas')
    await page.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
    await page.getByLabel('Starting player').selectOption('a')
    await page.getByLabel('Question order').selectOption('ordered')
    await page.getByRole('button', { name: 'Prepare duel' }).click()
    await expect(display.getByText('First question?')).toBeVisible()
    await expect(page.getByText('Private answer', { exact: true })).toBeVisible()
    await expect.poll(() => hostFrames.some(frame => frame.includes('Private answer'))).toBe(true)
    await expect.poll(() => publicFrames.some(frame => frame.includes('First question?'))).toBe(true)
    expect(publicFrames.join('')).not.toContain('Private')
    await expect(display.getByText('Private answer')).toHaveCount(0)

    const controls = page.locator('.controls')
    expect(await controls.evaluate(node => node.getBoundingClientRect().bottom <= window.innerHeight)).toBe(true)
    await page.screenshot({ path: testInfo.outputPath('host-1280x720.png'), animations: 'disabled' })
    await page.getByRole('button', { name: 'Start', exact: true }).click()
    await expect.poll(async () => Number(await display.locator('[data-clock=a]').textContent())).toBeLessThan(45)
    await expect(display.locator('[data-clock=b]')).toHaveText('45.0')
    await page.getByRole('heading', { name: 'Host dashboard' }).click()
    await page.keyboard.press('Space')
    await expect(display.getByText('Second question?')).toBeVisible()
    await expect(page.getByRole('button', { name: /Pass/ })).toBeEnabled()
    await page.keyboard.press('p')
    await expect(display.getByText('Third question?')).toBeVisible()
    await expect.poll(async () => Number(await display.locator('[data-clock=b]').textContent())).toBeLessThanOrEqual(42)

    const savedClock = await display.locator('[data-clock=a]').textContent()
    await expect(page.getByRole('button', { name: /Correct/ })).toBeEnabled()
    await page.keyboard.press('Space')
    await expect(display.getByText('Fourth question?')).toBeVisible()
    await expect(display.getByRole('region', { name: 'Contestant A' })).toContainText('Active player')
    await expect.poll(async () => Number(await display.locator('[data-clock=a]').textContent())).toBeGreaterThan(Number(savedClock) - 2)

    await expect(page.getByRole('button', { name: /Undo/ })).toBeEnabled()
    await page.keyboard.press('u')
    await expect(display.getByText('Third question?')).toBeVisible()
    await expect(display.getByText('Paused', { exact: true })).toBeVisible()
    await display.screenshot({ path: testInfo.outputPath('audience-1920x1080.png'), animations: 'disabled' })
    await socket?.close({ code: 1012 })
    await expect.poll(() => displayConnections).toBeGreaterThan(1)
    await expect(display.getByText('Third question?')).toBeVisible()
    await page.reload()
    await expect(page.getByText('Private answer', { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: /Resume/ })).toBeEnabled()
    await page.keyboard.press('r')
    await expect(display.getByText('Paused', { exact: true })).toHaveCount(0)
    await expect(page.getByRole('button', { name: /Pause/ })).toBeEnabled()
    await page.keyboard.press('Escape')
    await expect(display.getByText('Paused', { exact: true })).toBeVisible()

    await page.getByText('Duel options', { exact: true }).click()
    page.once('dialog', dialog => dialog.accept())
    await page.getByRole('button', { name: 'Declare Martin winner' }).click()
    await expect(display.getByRole('heading', { name: 'Martin wins!' })).toBeVisible()
    expect(await display.evaluate(() => document.documentElement.scrollHeight)).toBe(1080)
    await page.getByRole('button', { name: 'Return to setup' }).click()
    await expect(display.getByRole('heading', { name: 'Waiting for the host' })).toBeVisible()

    await page.getByLabel('Player A', { exact: true }).fill('Martin')
    await page.getByLabel('Player B', { exact: true }).fill('Thomas')
    await page.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
    await page.getByLabel('Starting player').selectOption('a')
    await page.getByLabel('Starting time per player (seconds)').fill('1')
    await page.getByRole('button', { name: 'Prepare duel' }).click()
    await page.getByRole('button', { name: 'Start', exact: true }).click()
    await expect(display.getByRole('heading', { name: 'Thomas wins!' })).toBeVisible()
    await expect(display.locator('[data-clock=a]')).toHaveText('0.0')
    await expect(display.locator('[data-clock=b]')).toHaveText('1.0')
    await expect(page.getByRole('button', { name: /Correct/ })).toBeDisabled()
    await expect(page.getByRole('button', { name: /Pass/ })).toBeDisabled()
    await page.getByRole('button', { name: 'Return to setup' }).click()
    await expect(page.getByRole('button', { name: 'Prepare duel' })).toBeEnabled()

  } finally {
    await context.close()
  }
})
