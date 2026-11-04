import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { request } from './api'
import type { AudienceDuel, HostState } from './contracts'

export type Role = 'host' | 'audience'
export type ConnectionStatus = 'connecting' | 'live' | 'reconnecting'
export type LiveUpdate = { role: 'host'; state: HostState } | { role: 'audience'; state: AudienceDuel | null }

export function openLiveConnection(
  role: Role,
  receive: (update: LiveUpdate) => void,
  report: (status: ConnectionStatus) => void,
) {
  const lifetime = new AbortController()
  const connection = new HubConnectionBuilder()
    .withUrl('/live?role=' + role)
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
  else connection.on('duel', (state: AudienceDuel | null) => deliver({ role, state }))

  async function refresh() {
    const id = ++requestId
    const before = revision
    const options = { signal: AbortSignal.any([lifetime.signal, AbortSignal.timeout(5000)]) }
    const update: LiveUpdate = role === 'host'
      ? { role, state: await request<HostState>('/api/host/state', options) }
      : { role, state: await request<AudienceDuel | null>('/api/display/state', options, false) }
    if (id === requestId && before === revision) deliver(update)
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
      void connection.stop().catch(() => undefined)
    },
  }
}
