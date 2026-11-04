import { defineStore } from 'pinia'
import { onMounted, onUnmounted, ref, shallowRef } from 'vue'
import { json, message, request } from './api'
import type { AudienceDuel, Configuration, DuelAction, HostDuel, HostState, Player } from './contracts'
import { openLiveConnection } from './liveConnection'
import type { ConnectionStatus, LiveUpdate, Role } from './liveConnection'

export interface ClockSample { duel: AudienceDuel; receivedAt: number }

export const useLive = defineStore('live', () => {
  const sample = shallowRef<ClockSample>()
  const host = shallowRef<HostDuel>()
  const audienceCount = ref(0)
  const status = ref<ConnectionStatus>('connecting')
  const pending = ref(false)
  const error = ref('')
  let connection: ReturnType<typeof openLiveConnection> | undefined
  let revision = 0

  function receive(update: LiveUpdate) {
    const duel = update.role === 'host' ? update.state.session?.duel : update.state
    const previous = sample.value?.duel
    if (duel && previous?.id === duel.id && (
      duel.version < previous.version ||
      (duel.version === previous.version && duel.sampledAt < previous.sampledAt)
    )) return
    revision++
    sample.value = duel ? { duel, receivedAt: performance.now() } : undefined
    host.value = update.role === 'host' ? update.state.session ?? undefined : undefined
    audienceCount.value = update.role === 'host' ? update.state.audienceCount : 0
  }

  function connect(role: Role) {
    connection?.stop()
    revision++
    sample.value = undefined
    host.value = undefined
    audienceCount.value = 0
    pending.value = false
    error.value = ''
    const current = openLiveConnection(role, receive, next => { status.value = next })
    connection = current
    return () => {
      current.stop()
      if (connection === current) connection = undefined
    }
  }

  async function send(path: string, data: unknown) {
    const current = connection
    if (pending.value || !current || status.value !== 'live') return
    pending.value = true
    error.value = ''
    const before = revision
    try {
      const state = await request<HostState>(path, {
        ...json('POST', data), signal: AbortSignal.timeout(10000),
      })
      if (connection === current && before === revision) receive({ role: 'host', state })
    } catch (cause) {
      if (connection === current) {
        error.value = message(cause)
        await current.refresh().catch(() => undefined)
      }
    } finally {
      if (connection === current) pending.value = false
    }
  }

  function prepare(setup: Configuration) {
    return send('/api/host/duel', setup)
  }

  function command(action: DuelAction, winner?: Player) {
    if (!host.value) return
    const duel = host.value.duel
    return send('/api/host/command', {
      sessionId: duel.id, version: duel.version, commandId: crypto.randomUUID(), action, winner,
    })
  }

  return { sample, host, audienceCount, status, pending, error, connect, prepare, command }
})

export function useLiveDuel(role: Role) {
  const live = useLive()
  let stop: (() => void) | undefined
  onMounted(() => { stop = live.connect(role) })
  onUnmounted(() => stop?.())
  return live
}
