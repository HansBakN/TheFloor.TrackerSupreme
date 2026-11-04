import { onMounted, onUnmounted } from 'vue'
import type { DuelAction } from './contracts'

const actions: Record<string, DuelAction> = { ' ': 'correct', p: 'pass', escape: 'pause', r: 'resume', u: 'undo' }

export function useShortcuts(can: (action: DuelAction) => boolean, run: (action: DuelAction) => void) {
  function keydown(event: KeyboardEvent) {
    if (event.defaultPrevented || event.repeat || event.isComposing || event.ctrlKey || event.metaKey || event.altKey) return
    if (event.target instanceof Element && (event.target.closest('input, textarea, select, [contenteditable], [role="textbox"]') ||
      (event.key === ' ' && event.target.closest('button, a')))) return
    const action = actions[event.key.toLowerCase()]
    if (!action || !can(action)) return
    event.preventDefault()
    run(action)
  }

  onMounted(() => window.addEventListener('keydown', keydown))
  onUnmounted(() => window.removeEventListener('keydown', keydown))
}
