import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import type { AudienceDuel, HostState } from './contracts'
import { useLive } from './live'
import type { LiveUpdate } from './liveConnection'

const wire = vi.hoisted(() => ({
  receive: undefined as ((update: LiveUpdate) => void) | undefined,
  stop: vi.fn(),
  refresh: vi.fn(async () => undefined),
}))

vi.mock('./liveConnection', () => ({
  openLiveConnection(_role: string, receive: (update: LiveUpdate) => void, report: (status: string) => void) {
    wire.receive = receive
    report('live')
    return { stop: wire.stop, refresh: wire.refresh }
  },
}))

const duel: AudienceDuel = {
  id: 'duel', version: 4, status: 'running', active: 'a', playerA: 'Martin', playerB: 'Thomas',
  categoryName: 'Geography', aMilliseconds: 1200, bMilliseconds: 2300,
  winner: null, sampledAt: 100, question: null,
}

beforeEach(() => {
  setActivePinia(createPinia())
  vi.clearAllMocks()
})

afterEach(() => vi.unstubAllGlobals())

it('keeps the latest duel sample and cleans up the subscription', () => {
  const live = useLive()
  const close = live.connect('audience')
  wire.receive?.({ role: 'audience', state: duel })
  wire.receive?.({ role: 'audience', state: { ...duel, version: 3, sampledAt: 200 } })
  wire.receive?.({ role: 'audience', state: { ...duel, sampledAt: 99, aMilliseconds: 0 } })
  expect(live.sample?.duel).toEqual(duel)
  wire.receive?.({ role: 'audience', state: { ...duel, status: 'paused', version: 5 } })
  expect(live.sample?.duel.status).toBe('paused')
  expect(live.host).toBeUndefined()
  close()
  expect(wire.stop).toHaveBeenCalledOnce()
})

it('posts a versioned host command and refreshes after a rejected transition', async () => {
  const live = useLive()
  const close = live.connect('host')
  const state: HostState = {
    audienceCount: 2,
    session: {
      duel, categorySetId: 'set', answer: 'Host-only', alternatives: [], notes: null, canUndo: false, exhausted: false,
      configuration: { playerA: 'Martin', playerB: 'Thomas', categoryId: 'geo', seconds: 45, passSeconds: 3, starter: 'a', order: 'ordered' },
    },
  }
  wire.receive?.({ role: 'host', state })
  const fetch = vi.fn(async (_url: string, _options: RequestInit) =>
    Response.json({ error: 'The duel has changed.' }, { status: 409 }))
  vi.stubGlobal('fetch', fetch)

  await live.command('correct')
  expect(live.audienceCount).toBe(2)
  expect(fetch.mock.calls[0][0]).toBe('/api/host/command')
  expect(JSON.parse(String(fetch.mock.calls[0][1].body))).toMatchObject({
    sessionId: 'duel', commandId: expect.any(String), version: 4, action: 'correct',
  })
  expect(live.error).toBe('The duel has changed.')
  expect(live.pending).toBe(false)
  expect(wire.refresh).toHaveBeenCalledOnce()
  close()
})

it('ignores a rapid repeated answer even after the first request completes', async () => {
  const live = useLive()
  const close = live.connect('host')
  const state: HostState = {
    audienceCount: 0,
    session: {
      duel, categorySetId: 'set', answer: 'Answer', alternatives: [], notes: null, canUndo: false, exhausted: false,
      configuration: { playerA: 'Martin', playerB: 'Thomas', categoryId: 'geo', seconds: 45, passSeconds: 3, starter: 'a', order: 'ordered' },
    },
  }
  const now = vi.spyOn(performance, 'now').mockReturnValue(1000)
  const fetch = vi.fn(async () => Response.json(state))
  vi.stubGlobal('fetch', fetch)
  try {
    wire.receive?.({ role: 'host', state })
    await live.command('correct')
    expect(live.pending).toBe(false)
    now.mockReturnValue(1100)
    await live.command('correct')
    expect(fetch).toHaveBeenCalledOnce()
    now.mockReturnValue(1300)
    await live.command('correct')
    expect(fetch).toHaveBeenCalledTimes(2)
  } finally {
    now.mockRestore()
    close()
  }
})
