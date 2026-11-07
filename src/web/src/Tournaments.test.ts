import { computed, ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import TournamentFloor from './TournamentFloor.vue'
import Tournaments from './Tournaments.vue'
import type { TournamentPool, TournamentState, TournamentView } from './tournament'
import { useTournament } from './tournament'

vi.mock('./Host.vue', () => ({ default: { template: '<section data-testid="existing-duel">Existing duel controls</section>' } }))
vi.mock('./tournament', async importOriginal => {
  const actual = await importOriginal<typeof import('./tournament')>()
  return { ...actual, useTournament: vi.fn() }
})
const categories = Array.from({ length: 19 }, (_, i) => ({ id: `cat${i}`, name: `Category ${i}`, status: 'available' as const, ownerId: null }))
const fixture = (): TournamentState => ({
  id: 'event', setId: 'set', setName: 'Event pool', width: 3, height: 2, version: 4, phase: 'initialSelection',
  players: Array.from({ length: 6 }, (_, i) => ({ id: `p${i}`, name: `Player ${i}`, color: i, categoryId: null, eliminationOrder: null })),
  floor: Array.from({ length: 6 }, (_, i) => ({ x: i % 3, y: Math.floor(i / 3), ownerId: `p${i}` })),
  selectionOrder: ['p0', 'p1', 'p2', 'p3', 'p4', 'p5'], categories, selectionIndex: 0, offer: ['cat0', 'cat1', 'cat2', 'cat3', 'cat4'],
  selectingPlayerId: 'p0', challengerId: null, challengerSource: 'random', matchups: [], activeDuelId: null, championId: null,
  seconds: 45, passSeconds: 3, order: 'shuffled', created: '2026-11-07T12:00:00Z', completed: null,
})
let view = ref<TournamentView>()
let pool = ref<TournamentPool>()
const command = vi.fn()
const send = vi.fn()
let error = ref('')
beforeEach(() => {
  vi.clearAllMocks()
  view = ref({ state: fixture(), eligibleOpponents: [], standings: [], canUndo: true })
  pool = ref({ setId: 'set', setName: 'Event pool', categories, errors: [] })
  error = ref('')
  vi.mocked(useTournament).mockReturnValue({
    view, state: computed(() => view.value?.state), pool, list: ref([]), pending: ref(false), error, loading: ref(false),
    command, send, refresh: vi.fn(),
  })
})
afterEach(() => { vi.restoreAllMocks() })
async function render(path = '/tournaments/event') {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/tournaments/:id?', component: Tournaments }] })
  await router.push(path)
  const wrapper = mount(Tournaments, { global: { plugins: [router] } })
  await flushPromises()
  return wrapper
}
const button = (wrapper: Awaited<ReturnType<typeof render>>, text: string) => wrapper.findAll('button').find(b => b.text() === text)!
function matchup() {
  view.value!.state.matchups = [{ id: 'matchup', challengerId: 'p0', defenderId: 'p2', defenderCategoryId: 'cat0', challengerCategoryId: 'cat1',
    duels: [], deciderOffer: ['cat2', 'cat3', 'cat4', 'cat5', 'cat6'], bans: [], deciderCategoryId: null,
    winnerId: null, loserId: null, transferred: [], completed: null }]
}
it('highlights and selects every server-provided neighbor, including the opposite edge; no diagonal', async () => {
  const state = fixture()
  state.phase = 'opponentSelection'
  state.challengerId = 'p0'
  const wrapper = mount(TournamentFloor, { props: { state, eligible: ['p1', 'p2', 'p3'] } })
  expect(wrapper.findAll('[data-eligible="true"]')).toHaveLength(3)
  expect(wrapper.get('[data-x="2"][data-y="0"]').attributes('disabled')).toBeUndefined()
  await wrapper.get('[data-x="2"][data-y="0"]').trigger('click')
  expect(wrapper.emitted('challenge')).toEqual([['p2']])
  expect(wrapper.get('[data-x="2"][data-y="1"]').attributes('disabled')).toBeDefined()
  expect(wrapper.text()).toContain('Left and right edges connect')
  wrapper.unmount()
})
it('shows stable five-category initial offer and sends one explicit pick', async () => {
  const wrapper = await render()
  expect(wrapper.findAll('.category-offer button')).toHaveLength(5)
  expect(wrapper.text()).toContain('Pick 1 of 6')
  await button(wrapper, 'Select Category 2').trigger('click')
  expect(command).toHaveBeenCalledWith('pick', { categoryIds: ['cat2'] })
  wrapper.unmount()
})
it('enforces two challenger bans in UI and preserves visibly banned categories', async () => {
  matchup()
  view.value!.state.phase = 'challengerBans'
  view.value!.state.matchups[0]!.bans = ['cat2']
  const wrapper = await render()
  expect(wrapper.text()).toContain('Challenger Player 0 bans two')
  expect(button(wrapper, 'Banned · Category 2').attributes('disabled')).toBeDefined()
  expect(button(wrapper, 'Confirm two bans (0/2)').attributes('disabled')).toBeDefined()
  await button(wrapper, 'Ban · Category 3').trigger('click')
  await button(wrapper, 'Ban · Category 4').trigger('click')
  await button(wrapper, 'Confirm two bans (2/2)').trigger('click')
  expect(command).toHaveBeenCalledWith('ban', { categoryIds: ['cat3', 'cat4'] })
  wrapper.unmount()
})
it('shows fixed duel categories and starter and embeds existing duel controls while playing', async () => {
  matchup()
  view.value!.state.phase = 'duelReady'
  const wrapper = await render()
  expect(wrapper.text()).toContain('Duel 1: Category 0 · Player 2 first')
  expect(wrapper.text()).toContain('Player 2 answers first')
  await button(wrapper, 'Prepare tournament duel').trigger('click')
  expect(command).toHaveBeenCalledWith('launch')
  view.value!.state.phase = 'playing'
  await flushPromises()
  expect(wrapper.get('[data-testid="existing-duel"]').text()).toBe('Existing duel controls')
  wrapper.unmount()
})
it('requires confirmation for correction, restart and deletion and shows host errors', async () => {
  error.value = 'The tournament changed. Review its current state.'
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
  const wrapper = await render()
  expect(wrapper.get('[role="alert"]').text()).toContain('The tournament changed')
  await button(wrapper, 'Undo latest duel result').trigger('click')
  expect(command).not.toHaveBeenCalled()
  confirm.mockReturnValue(true)
  for (const [label, action] of [['Undo latest duel result', 'undoResult'], ['Restart tournament', 'restart'], ['Delete tournament', 'delete']]) {
    await button(wrapper, label!).trigger('click')
    expect(command).toHaveBeenCalledWith(action, { confirm: true })
  }
  wrapper.unmount()
})
it('disables creation below 19 categories and shows pool and settings for review', async () => {
  view.value = undefined
  pool.value!.categories = categories.slice(0, 18)
  const wrapper = await render('/tournaments')
  expect(wrapper.text()).toContain('At least 19 distinct playable categories')
  expect(button(wrapper, 'Create tournament').attributes('disabled')).toBeDefined()
  expect(wrapper.findAll('input[maxlength="80"]')).toHaveLength(6)
  pool.value!.categories = categories
  await flushPromises()
  expect(button(wrapper, 'Create tournament').attributes('disabled')).toBeUndefined()
  expect(wrapper.text()).toContain('Review eligible category pool (19)')
  wrapper.unmount()
})
it('displays champion, final floor, standings and accessible full results', async () => {
  matchup()
  const state = view.value!.state
  state.phase = 'completed'; state.championId = 'p0'
  state.floor = state.floor.map(c => ({ ...c, ownerId: 'p0' }))
  view.value!.standings = [{ ...state.players[0]!, active: true, territories: 6, matchupsPlayed: 2, matchupsWon: 2, matchupsLost: 0, duelsWon: 4, duelsLost: 1 }]
  const wrapper = await render()
  expect(wrapper.get('[data-testid="phase-title"]').text()).toContain('Player 0 is the Mini Floor champion')
  expect(wrapper.get('.standings').text()).toContain('4–1')
  expect(wrapper.findAll('[data-owner="p0"]')).toHaveLength(6)
  expect(wrapper.text()).not.toContain('Continue or return to the floor?')
  expect(wrapper.text()).toContain('Matchup history (1)')
  wrapper.unmount()
})

it('configures a nonrectangular participant count with dynamic names, preview and pool capacity', async () => {
  view.value = undefined
  const wrapper = await render('/tournaments')
  await wrapper.get('input[min="2"]').setValue(7)
  const dimensions = wrapper.findAll('input[max="64"]')
  await dimensions[0]!.setValue(3)
  await dimensions[1]!.setValue(3)
  expect(wrapper.findAll('input[maxlength="80"]')).toHaveLength(7)
  expect(wrapper.findAll('.preview-cell')).toHaveLength(9)
  expect(wrapper.text()).toContain('9 cells · 7 players · 2 spare cells')
  expect(wrapper.text()).toContain('At least 22 distinct playable categories')
  expect(button(wrapper, 'Create tournament').attributes('disabled')).toBeDefined()
  pool.value!.categories = Array.from({ length: 22 }, (_, i) => ({ id: `c${i}`, name: `Category ${i}`, status: 'available', ownerId: null }))
  await flushPromises()
  expect(button(wrapper, 'Create tournament').attributes('disabled')).toBeUndefined()
  await dimensions[1]!.setValue(2)
  expect(button(wrapper, 'Create tournament').attributes('disabled')).toBeDefined()
  wrapper.unmount()
})
