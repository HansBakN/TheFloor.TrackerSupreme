import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { request } from './api'
import type { AudienceTournament } from './tournament'
import type { AudienceDuel, HostState } from './contracts'

// getRandomValues also works on a display served over HTTP on the LAN.
const idHex = Array.from(crypto.getRandomValues(new Uint8Array(16)), byte => byte.toString(16).padStart(2, '0')).join('')
export const displayId = [idHex.slice(0, 8), idHex.slice(8, 12), idHex.slice(12, 16), idHex.slice(16, 20), idHex.slice(20)].join('-')
export const displayName = 'DISPLAY ' + idHex.slice(0, 6).toUpperCase()

export type Role = 'host' | 'audience'
export type ConnectionStatus = 'connecting' | 'live' | 'reconnecting'
export type LiveUpdate = { role: 'host'; state: HostState } | { role: 'audience'; state: AudienceDuel | null } | { role: 'tournament'; state: AudienceTournament | null }

export function openLiveConnection(
  role: Role,
  receive: (update: LiveUpdate) => void,
  report: (status: ConnectionStatus) => void,
) {
  const lifetime = new AbortController()
  const connection = new HubConnectionBuilder()
    .withUrl('/live?role=' + role + (role === 'audience' ? '&displayId=' + displayId : ''))
    .configureLogging(LogLevel.None)
    .build()
  let timer: number | undefined
  let revision = 0
  let requestId = 0
  let failures = 0

  function deliver(update: LiveUpdate) {
    if (lifetime.signal.aborted) return
    revision++
    receive(update)
  }

  if (role === 'host') connection.on('hostDuel', (state: HostState) => deliver({ role, state }))
  else {
    connection.on('duel', (state: AudienceDuel | null) => deliver({ role, state }))
    connection.on('tournament', (state: AudienceTournament | null) => deliver({ role: 'tournament', state }))
  }

  async function refresh() {
    const id = ++requestId
    const before = revision
    const options = { signal: AbortSignal.any([lifetime.signal, AbortSignal.timeout(5000)]) }
    if (role === 'host') {
      const state = await request<HostState>('/api/host/state', options)
      if (id === requestId && before === revision) deliver({ role, state })
    } else {
      const [state, tournament] = await Promise.all([
        request<AudienceDuel | null>('/api/display/state', options, false),
        request<AudienceTournament | null>('/api/display/tournament', options, false),
      ])
      if (id === requestId && before === revision) {
        deliver({ role, state })
        deliver({ role: 'tournament', state: tournament })
      }
    }
  }

  function retry() {
    if (lifetime.signal.aborted) return
    report('reconnecting')
    window.clearTimeout(timer)
    const delay = Math.min(5000, 250 * 2 ** Math.min(failures++, 5))
    timer = window.setTimeout(() => void connect(), delay)
  }

  async function connect() {
    try {
      await refresh()
      if (lifetime.signal.aborted) return
      await connection.start()
    } catch {
      retry()
      return
    }
    if (lifetime.signal.aborted) return
    failures = 0
    report('live')
    void refresh().catch(() => undefined)
  }

  connection.onclose(retry)
  document.addEventListener('visibilitychange', () => {
    if (!document.hidden) void refresh().catch(() => undefined)
  }, { signal: lifetime.signal })
  report('connecting')
  void connect()

  return {
    refresh,
    stop() {
      lifetime.abort()
      window.clearTimeout(timer)
      connection.off('duel')
      connection.off('hostDuel')
      connection.off('tournament')
      void connection.stop().catch(() => undefined)
    },
  }
}
