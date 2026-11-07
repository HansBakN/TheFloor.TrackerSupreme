import { expect, test } from '@playwright/test'
import type { Page, APIRequestContext } from '@playwright/test'
import type { TournamentView } from '../src/tournament'
import { newDataRoot, startServer } from './isolated-server'

async function seed(request: APIRequestContext) {
  expect((await request.post('/api/local-host')).status()).toBe(204)
  const ids: string[] = []
  for (let i = 0; i < 19; i++) {
    const id = crypto.randomUUID()
    ids.push(id)
    expect((await request.put('/api/library/categories/' + id, { data: { definition: {
      schemaVersion: 1, id, name: 'Mini category ' + (i + 1), questions: Array.from({ length: 8 }, (_, j) => ({
        id: crypto.randomUUID(), type: 'text', prompt: `Mini prompt ${i + 1}.${j + 1}`, answer: 'Secret tournament answer', enabled: true,
      })),
    } } })).ok()).toBeTruthy()
  }
  const setId = crypto.randomUUID()
  expect((await request.put('/api/library/sets/' + setId, { data: { definition: {
    schemaVersion: 1, id: setId, name: 'Mini Floor browser event', categoryIds: ids,
  } } })).ok()).toBeTruthy()
  expect((await request.post('/api/library/sets/' + setId + '/activate')).ok()).toBeTruthy()
}
async function state(page: Page): Promise<TournamentView> {
  return (await page.request.get('/api/host/tournaments/' + page.url().split('/').at(-1))).json()
}
async function phase(page: Page, expected: string) {
  await expect.poll(async () => (await state(page)).state.phase).toBe(expected)
  await expect(page.locator('.tournament-workspace')).toHaveAttribute('data-phase', expected)
}
async function initialPicks(page: Page) {
  for (let i = 0; i < 6; i++) {
    await expect(page.getByText(`Pick ${i + 1} of 6.`, { exact: false })).toBeVisible()
    const before = (await state(page)).state
    if (i === 0) {
      await page.reload()
      await expect(page.getByText('Pick 1 of 6.', { exact: false })).toBeVisible()
      expect((await state(page)).state.offer).toEqual(before.offer)
      expect((await state(page)).state.floor).toEqual(before.floor)
      expect((await state(page)).state.selectionOrder).toEqual(before.selectionOrder)
    }
    await page.getByRole('button', { name: /^Select Mini category/ }).first().click()
  }
  await phase(page, 'opponentSelection')
}
async function launch(page: Page, display: Page, winner: 'challenger' | 'defender', interact = false) {
  await phase(page, 'duelReady')
  const before = (await state(page)).state
  const matchup = before.matchups.at(-1)!
  const number = matchup.duels.length + 1
  await page.getByRole('button', { name: 'Prepare tournament duel', exact: true }).click()
  await phase(page, 'playing')
  const start = page.getByRole('button', { name: 'Start', exact: true })
  await expect(start).toBeVisible()
  const prepared = await (await page.request.get('/api/host/state')).json()
  expect(prepared.session.configuration.starter).toBe(number === 1 ? 'b' : 'a')
  expect(prepared.session.configuration.categoryId).toBe(number === 1 ? matchup.defenderCategoryId : number === 2 ? matchup.challengerCategoryId : matchup.deciderCategoryId)
  await start.click()
  await expect(display.locator('.question-prompt')).toBeVisible()
  await expect(display.locator('body')).not.toContainText('Secret tournament answer')
  if (interact) {
    await page.setViewportSize({ width: 1280, height: 720 })
    await page.evaluate(() => window.scrollTo(0, 0))
    await expect(page.getByRole('button', { name: /^Correct/ })).toBeInViewport()
    await expect(page.getByTestId('phase-title')).toBeInViewport()
    await page.screenshot({ path: 'test-results/mini-floor-live-host.png' })
    await display.screenshot({ path: 'test-results/mini-floor-live-audience.png' })
    await page.getByRole('button', { name: /^Correct/ }).click()
    await page.getByRole('button', { name: /^Undo U/ }).click()
    await page.getByRole('button', { name: /^Resume/ }).click()
    await page.getByRole('button', { name: /^Pause/ }).click()
    await page.getByRole('button', { name: /^Resume/ }).click()
  }
  const winnerId = winner === 'challenger' ? matchup.challengerId : matchup.defenderId
  const winnerName = before.players.find(p => p.id === winnerId)!.name
  await page.getByText('Duel options', { exact: true }).click()
  page.once('dialog', dialog => dialog.accept())
  await page.getByRole('button', { name: `Declare ${winnerName} winner`, exact: true }).click()
  await expect.poll(async () => (await state(page)).state.phase).not.toBe('playing')
}

for (const deciding of [false, true]) {
  test(`Mini Floor complete UI tournament ${deciding ? 'with every matchup decided' : 'with five 2–0 matchups'}`, async ({ browser }) => {
    test.setTimeout(180_000)
    const root = await newDataRoot()
    const server = await startServer(root.path)
    const host = await browser.newContext({ baseURL: server.origin, viewport: { width: 1280, height: 900 } })
    const audience = await browser.newContext({ baseURL: server.origin, viewport: { width: 1920, height: 1080 } })
    try {
      expect((await audience.request.get('/api/host/tournaments')).status()).toBe(401)
      expect((await audience.request.post('/api/host/tournaments', { data: {} })).status()).toBe(401)
      await seed(host.request)
      const page = await host.newPage()
      page.setDefaultTimeout(15_000)
      const display = await audience.newPage()
      await display.goto('/display')
      await page.goto('/tournaments')
      await expect(page.getByText('19 eligible categories', { exact: false })).toBeVisible()
      for (let i = 0; i < 6; i++) await page.getByLabel('Participant ' + (i + 1), { exact: true }).fill('Player ' + (i + 1))
      await page.getByRole('button', { name: 'Create tournament', exact: true }).click()
      await expect(page).toHaveURL(/\/tournaments\/[a-f0-9-]+/)
      await initialPicks(page)
      for (let m = 0; m < 5; m++) {
        const current = await state(page)
        await expect.poll(async () => page.locator('.territory[data-eligible="true"]').count())
          .toBe(current.state.floor.filter(c => current.eligibleOpponents.includes(c.ownerId)).length)
        const cells = current.state.floor.filter(c => c.ownerId === current.state.challengerId)
        const edgeTarget = current.state.floor.find(c => current.eligibleOpponents.includes(c.ownerId) && cells.some(a => a.y === c.y && Math.abs(a.x - c.x) === 2))
        const opponent = edgeTarget?.ownerId ?? current.eligibleOpponents[0]
        const opponentName = current.state.players.find(p => p.id === opponent)!.name
        await page.getByRole('button', { name: 'Challenge ' + opponentName, exact: true }).click()
        await launch(page, display, 'challenger', m === 0)
        await launch(page, display, deciding ? 'defender' : 'challenger')
        if (deciding) {
          await phase(page, 'defenderBan')
          const offer = (await state(page)).state.matchups.at(-1)!.deciderOffer
          await page.getByRole('button', { name: /^Ban · Mini category/ }).first().click()
          await phase(page, 'challengerBans')
          if (m === 0) {
            await page.reload()
            await expect(page.getByTestId('phase-title')).toContainText('bans two')
            expect((await state(page)).state.matchups.at(-1)!.deciderOffer).toEqual(offer)
            expect((await state(page)).state.matchups.at(-1)!.bans).toHaveLength(1)
          }
          const available = page.getByRole('button', { name: /^Ban · Mini category/ })
          await available.nth(0).click()
          await available.nth(1).click()
          await page.getByRole('button', { name: 'Confirm two bans (2/2)', exact: true }).click()
          await phase(page, 'defenderPick')
          if (m === 0) await page.screenshot({ path: 'test-results/mini-floor-decider-pick.png', fullPage: true })
          await page.getByRole('button', { name: /^Choose · Mini category/ }).first().click()
          await launch(page, display, 'challenger')
        }
        if (m === 4) break
        await phase(page, 'matchupResult')
        await page.getByRole('button', { name: 'Choose winner’s fresh category', exact: true }).click()
        await phase(page, 'winnerCategory')
        await page.getByRole('button', { name: /^Select Mini category/ }).first().click()
        await phase(page, 'winnerDecision')
        await page.getByRole('button', { name: m % 2 === 0 ? 'Return to floor' : 'Continue challenging', exact: true }).click()
        await phase(page, 'opponentSelection')
      }
      await phase(page, 'completed')
      await expect(page.getByTestId('phase-title')).toContainText('Mini Floor champion!')
      const result = (await state(page)).state
      expect(result.matchups).toHaveLength(5)
      expect(result.players.filter(p => p.eliminationOrder !== null)).toHaveLength(5)
      expect(new Set(result.floor.map(c => c.ownerId))).toEqual(new Set([result.championId]))
      const categories = result.matchups.flatMap(m => m.duels.map(d => d.categoryId))
      expect(new Set(categories).size).toBe(deciding ? 15 : 10)
      await page.screenshot({ path: `test-results/mini-floor-${deciding ? 'deciders' : 'sweeps'}-champion.png`, fullPage: true })
      await page.reload()
      await expect(page.getByTestId('phase-title')).toContainText('Mini Floor champion!')
      // Safe correction must reverse the final elimination and champion declaration.
      await page.getByText('Tournament controls and recovery', { exact: true }).click()
      page.once('dialog', dialog => dialog.dismiss())
      await page.getByRole('button', { name: 'Undo latest duel result', exact: true }).click()
      expect((await state(page)).state.phase).toBe('completed')
      page.once('dialog', dialog => dialog.accept())
      await page.getByRole('button', { name: 'Undo latest duel result', exact: true }).click()
      await phase(page, 'playing')
      expect((await state(page)).state.players.filter(p => p.eliminationOrder === null)).toHaveLength(2)
      await expect(page.getByRole('button', { name: 'Start', exact: true })).toBeVisible()
      await page.getByRole('button', { name: 'Start', exact: true }).click()
      const prior = result.matchups.at(-1)!
      const winnerName = result.players.find(p => p.id === prior.winnerId)!.name
      await page.getByText('Duel options', { exact: true }).click()
      page.once('dialog', dialog => dialog.accept())
      await page.getByRole('button', { name: `Declare ${winnerName} winner`, exact: true }).click()
      await phase(page, 'completed')
      expect((await state(page)).state.championId).toBe(result.championId)
      const tournamentUrl = page.url()
      await page.goto('/host')
      await page.getByRole('button', { name: 'Return to setup', exact: true }).click()
      await expect(page.getByRole('button', { name: 'Prepare duel', exact: true })).toBeVisible()
      await page.getByLabel('Player A', { exact: true }).fill('Standalone A')
      await page.getByLabel('Player B', { exact: true }).fill('Standalone B')
      await page.getByRole('combobox', { name: 'Category', exact: true }).selectOption(result.categories[0]!.id)
      await page.getByRole('button', { name: 'Prepare duel', exact: true }).click()
      await expect(page.getByRole('button', { name: 'Start', exact: true })).toBeVisible()
      const standalone = await (await page.request.get('/api/host/state')).json()
      expect(standalone.session.tournamentId).toBeNull()
      page.once('dialog', dialog => dialog.accept())
      await page.getByRole('button', { name: 'End duel and return to setup', exact: true }).click()
      await page.goto(tournamentUrl)
      await phase(page, 'completed')
      expect((await state(page)).state.championId).toBe(result.championId)
    } finally {
      await Promise.allSettled([host.close(), audience.close()]); await server.stop(); await root.remove()
    }
  })
}

test('Mini Floor restores an in-progress tournament duel paused after actual backend restart', async ({ browser }) => {
  const root = await newDataRoot()
  let server: Awaited<ReturnType<typeof startServer>> | undefined
  try {
    server = await startServer(root.path)
    const port = Number(new URL(server.origin).port)
    const context = await browser.newContext({ baseURL: server.origin })
    try {
      await seed(context.request)
      const page = await context.newPage()
      await page.goto('/tournaments')
      for (let i = 0; i < 6; i++) await page.getByLabel('Participant ' + (i + 1), { exact: true }).fill('Recovery ' + (i + 1))
      await page.getByRole('button', { name: 'Create tournament', exact: true }).click()
      await expect(page).toHaveURL(/\/tournaments\/[a-f0-9-]+/)
      await initialPicks(page)
      await page.getByRole('button', { name: /^Challenge Recovery/ }).first().click()
      await page.getByRole('button', { name: 'Prepare tournament duel', exact: true }).click()
      await page.getByRole('button', { name: 'Start', exact: true }).click()
      const before = await (await context.request.get('/api/host/state')).json()
      const tournamentBefore = (await state(page)).state
      await server.stop()
      server = await startServer(root.path, port)
      await page.reload()
      await expect(page.getByRole('button', { name: /^Resume/ })).toBeVisible()
      const recovered = await (await context.request.get('/api/host/state')).json()
      expect(recovered.session.duel.id).toBe(before.session.duel.id)
      expect(recovered.session.duel.question.id).toBe(before.session.duel.question.id)
      expect(recovered.session.configuration.starter).toBe('b')
      expect(recovered.session.duel.status).toBe('paused')
      expect((await state(page)).state).toEqual(tournamentBefore)
      expect((await state(page)).state.matchups[0].duels).toHaveLength(0)
      await page.getByRole('button', { name: /^Resume/ }).click()
    } finally { await context.close() }
  } finally { await server?.stop(); await root.remove() }
})
