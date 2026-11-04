import { onUnmounted, shallowRef, watch } from 'vue'
import type { ClockSample } from './live'

export function clockValues(sample: ClockSample | undefined, now: number) {
  if (!sample) return { a: 0, b: 0 }
  const { duel, receivedAt } = sample
  const elapsed = duel.status === 'running' ? Math.max(0, now - receivedAt) : 0
  return {
    a: Math.max(0, duel.aMilliseconds - (duel.active === 'a' ? elapsed : 0)),
    b: Math.max(0, duel.bMilliseconds - (duel.active === 'b' ? elapsed : 0)),
  }
}

export function formatClock(ms: number) {
  return (Math.ceil(ms / 100) / 10).toFixed(1)
}

export function useClocks(readSample: () => ClockSample | undefined) {
  const clocks = shallowRef(clockValues(undefined, 0))
  let frame = 0

  function render(now: number) {
    const sample = readSample()
    clocks.value = clockValues(sample, now)
    if (sample?.duel.status === 'running' && clocks.value[sample.duel.active] > 0) {
      frame = requestAnimationFrame(render)
    }
  }

  watch(readSample, () => {
    cancelAnimationFrame(frame)
    render(performance.now())
  }, { immediate: true })

  onUnmounted(() => cancelAnimationFrame(frame))
  return clocks
}
