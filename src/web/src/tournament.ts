import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { json, message, request } from './api'

export type TournamentPhase = 'initialSelection' | 'opponentSelection' | 'duelReady' | 'playing' | 'defenderBan' | 'challengerBans' | 'defenderPick' | 'matchupResult' | 'winnerCategory' | 'winnerDecision' | 'completed'
export interface TournamentPlayer { id: string; name: string; color: number; categoryId: string | null; eliminationOrder: number | null }
export interface Territory { x: number; y: number; ownerId: string }
export interface TournamentCategory { id: string; name: string; status: 'available' | 'selected' | 'played'; ownerId: string | null }
export interface TournamentDuel { id: string; categoryId: string; startingPlayerId: string; winnerId: string; started: string | null; completed: string }
export interface Matchup {
  id: string; challengerId: string; defenderId: string; defenderCategoryId: string; challengerCategoryId: string
  duels: TournamentDuel[]; deciderOffer: string[]; bans: string[]; deciderCategoryId: string | null
  winnerId: string | null; loserId: string | null; transferred: Territory[]; completed: string | null
}
export interface TournamentState {
  id: string; setId: string; setName: string; players: TournamentPlayer[]; floor: Territory[]; selectionOrder: string[]
  categories: TournamentCategory[]; width: number; height: number; version: number; phase: TournamentPhase
  selectionIndex: number; offer: string[]; selectingPlayerId: string | null; challengerId: string | null
  challengerSource: 'random' | 'continued'; matchups: Matchup[]; activeDuelId: string | null; championId: string | null
  seconds: number; passSeconds: number; order: 'ordered' | 'shuffled'; created: string; completed: string | null
}
export interface Standing {
  id: string; name: string; color: number; active: boolean; categoryId: string | null; territories: number
  matchupsPlayed: number; matchupsWon: number; matchupsLost: number; duelsWon: number; duelsLost: number; eliminationOrder: number | null
}
export interface TournamentView { state: TournamentState; eligibleOpponents: string[]; standings: Standing[]; canUndo: boolean }
export interface TournamentSummary { id: string; created: string; phase: TournamentPhase; players: string[]; champion: string | null }
export interface TournamentPool { setId: string | null; setName: string | null; categories: TournamentCategory[]; errors: string[] }
const root = '/api/host/tournaments'
export const playerColors = ['#63ede3', '#ffcb74', '#b7a4ff', '#ff92b6', '#9ce280', '#8ebaff']

// One host-only source of tournament state. The server supplies all legal targets and rules.
export function useTournament(id: () => string | undefined) {
  const view = ref<TournamentView>()
  const list = ref<TournamentSummary[]>([])
  const pool = ref<TournamentPool>()
  const error = ref('')
  const pending = ref(false)
  const loading = ref(true)
  const state = computed(() => view.value?.state)
  let mounted = false
  let timer: number | undefined
  let sequence = 0

  async function refresh() {
    const selected = id()
    const serial = ++sequence
    try {
      if (selected) {
        const value = await request<TournamentView>(`${root}/${selected}`)
        if (serial === sequence) view.value = value
      } else {
        const [summaries, available] = await Promise.all([request<TournamentSummary[]>(root), request<TournamentPool>(root + '/pool')])
        if (serial === sequence) { list.value = summaries; pool.value = available; view.value = undefined }
      }
    } catch (cause) {
      if (serial === sequence) error.value = message(cause)
    } finally { if (serial === sequence) loading.value = false }
  }
  async function send<T>(path: string, data: unknown) {
    if (pending.value) return
    pending.value = true
    error.value = ''
    ++sequence
    try { return await request<T>(root + path, json('POST', data)) }
    catch (cause) { error.value = message(cause) }
    finally { pending.value = false; await refresh() }
  }
  async function command(action: string, extra: { playerId?: string; categoryIds?: string[]; confirm?: boolean } = {}) {
    if (!state.value) return
    return send<TournamentView | null>('/command', {
      tournamentId: state.value.id, commandId: crypto.randomUUID(), version: state.value.version, action, ...extra,
    })
  }
  async function poll() {
    if (!document.hidden && !pending.value) await refresh()
    if (mounted) timer = window.setTimeout(() => void poll(), 650)
  }
  watch(id, () => { view.value = undefined; loading.value = true; error.value = ''; void refresh() })
  onMounted(() => { mounted = true; void poll() })
  onUnmounted(() => { mounted = false; ++sequence; window.clearTimeout(timer) })
  return { view, list, pool, error, pending, loading, state, refresh, command, send }
}
