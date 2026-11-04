import { describe, expect, it } from 'vitest'
import { clockValues, formatClock } from './clocks'
import type { ClockSample } from './live'

const sample: ClockSample = {
  receivedAt: 1000,
  duel: {
    id: 'duel', version: 3, status: 'running', categoryName: 'Geography',
    playerA: 'Martin', playerB: 'Thomas', aMilliseconds: 2500, bMilliseconds: 5000,
    active: 'a', winner: null, question: null, sampledAt: 0,
  },
}

describe('clock presentation', () => {
  it('interpolates only the active clock and never drops below zero', () => {
    expect(clockValues(sample, 1255)).toEqual({ a: 2245, b: 5000 })
    expect(clockValues(sample, 4000)).toEqual({ a: 0, b: 5000 })
    expect(formatClock(2245)).toBe('2.3')
  })

  it('preserves both server values while paused', () => {
    const paused: ClockSample = { ...sample, duel: { ...sample.duel, status: 'paused' } }
    expect(clockValues(paused, 60_000)).toEqual({ a: 2500, b: 5000 })
  })
})
