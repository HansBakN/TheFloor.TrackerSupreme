import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import TournamentAudience from './TournamentAudience.vue'
import type { AudienceTournament } from './tournament'
import { playerColor } from './tournament'

export const audienceFixture = (): AudienceTournament => ({
  id: 'tour', version: 5, width: 3, height: 2, phase: 'initialSelection',
  players: Array.from({ length: 5 }, (_, i) => ({ id: `p${i}`, name: `Player ${i}`, color: i, categoryId: `c${i}`, eliminationOrder: null,
    territories: i === 0 ? 2 : 1, matchupsWon: 0, matchupsLost: 0, duelsWon: 0, duelsLost: 0 })),
  floor: Array.from({ length: 6 }, (_, i) => ({ x: i % 3, y: Math.floor(i / 3), ownerId: `p${i % 5}` })),
  categories: Array.from({ length: 16 }, (_, i) => ({ id: `c${i}`, name: `Category ${i}` })),
  eligibleOpponents: ['p1', 'p2'], challengerId: 'p0', championId: null, challengerSource: 'random', selectingPlayerId: 'p0',
  selectionIndex: 2, selectionOrder: ['p0', 'p1', 'p2', 'p3', 'p4'], offer: ['c5', 'c6', 'c7', 'c8', 'c9'],
  matchup: { challengerId: 'p0', defenderId: 'p2', challengerScore: 1, defenderScore: 1, defenderCategoryId: 'c2', challengerCategoryId: 'c0',
    deciderCategoryId: null, deciderOffer: ['c5', 'c6', 'c7', 'c8', 'c9'], bans: ['c5'], duels: [], winnerId: null, loserId: null,
    territoriesGained: 0, duelNumber: 3, startingPlayerId: 'p0', categoryId: null },
  completedMatchups: 0, totalMatchups: 4, activeDuelId: null,
})

it('shows five category options and dynamic pick counts without audience mutation controls', () => {
  const wrapper = mount(TournamentAudience, { props: { tournament: audienceFixture() } })
  expect(wrapper.text()).toContain('Category pick 3 of 5')
  expect(wrapper.findAll('[aria-label="Category options"] li')).toHaveLength(5)
  expect(wrapper.findAll('button,input,select,form')).toHaveLength(0)
  expect(wrapper.findAll('.territory')).toHaveLength(6)
  wrapper.unmount()
})
it('shows legal opponent options and highlights every corresponding territory', () => {
  const tournament = audienceFixture(); tournament.phase = 'opponentSelection'
  const wrapper = mount(TournamentAudience, { props: { tournament } })
  expect(wrapper.findAll('[aria-label="Opponent options"] li')).toHaveLength(2)
  expect(wrapper.findAll('.territory.eligible')).toHaveLength(2)
  expect(wrapper.text()).toContain('Opposite board edges connect')
  wrapper.unmount()
})
it('shows ordered bans, chosen category, results, next decisions and champion', async () => {
  const tournament = audienceFixture(); tournament.phase = 'challengerBans'
  const wrapper = mount(TournamentAudience, { props: { tournament } })
  expect(wrapper.get('[data-testid="audience-phase-title"]').text()).toContain('Player 0 bans two')
  expect(wrapper.findAll('.audience-options .banned')).toHaveLength(1)
  await wrapper.setProps({ tournament: { ...tournament, phase: 'defenderPick', matchup: { ...tournament.matchup!, bans: ['c5', 'c6', 'c7'] } } })
  expect(wrapper.text()).toContain('Defender chooses from 2')
  expect(wrapper.findAll('.audience-options .banned')).toHaveLength(3)
  await wrapper.setProps({ tournament: { ...tournament, phase: 'duelReady', matchup: { ...tournament.matchup!, deciderCategoryId: 'c8', categoryId: 'c8' } } })
  expect(wrapper.get('.chosen').text()).toContain('Category 8')
  expect(wrapper.text()).toContain('Player 0 answers first')
  await wrapper.setProps({ tournament: { ...tournament, phase: 'matchupResult', matchup: { ...tournament.matchup!, winnerId: 'p0', loserId: 'p2', territoriesGained: 3 } } })
  expect(wrapper.text()).toContain('Player 0 captures 3 territories')
  await wrapper.setProps({ tournament: { ...tournament, phase: 'winnerDecision' } })
  expect(wrapper.text()).toContain('Continue challenging')
  expect(wrapper.text()).toContain('Return to the floor')
  await wrapper.setProps({ tournament: { ...tournament, phase: 'completed', championId: 'p0' } })
  expect(wrapper.text()).toContain('Player 0 is the Mini Floor champion')
  expect(wrapper.text()).toContain('4 completed matchups')
  wrapper.unmount()
})
it('provides stable player colors beyond the original six', () => {
  expect(playerColor(0)).toBe('#63ede3')
  expect(playerColor(6)).toMatch(/^hsl/)
  expect(playerColor(15)).toBe(playerColor(15))
  expect(playerColor(6)).not.toBe(playerColor(7))
})
