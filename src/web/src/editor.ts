import { computed, ref, watch } from 'vue'
import { message } from './api'
import type { Entry } from './contracts'

export function useDraft<T extends { id: string }>(id: () => string, getEntry: () => Entry<T> | undefined) {
  const draft = ref<T>()
  const revision = ref<string>()
  const baseline = ref('')
  const dirty = computed(() => JSON.stringify(draft.value) !== baseline.value && !!draft.value)
  const stale = computed(() => !!draft.value && revision.value !== getEntry()?.revision)

  function load(entry = getEntry()) {
    draft.value = entry && JSON.parse(JSON.stringify(entry.definition))
    revision.value = entry?.revision
    baseline.value = JSON.stringify(draft.value) ?? ''
  }

  watch(id, () => load(), { immediate: true, flush: 'sync' })
  watch(() => getEntry()?.revision, () => {
    if (!dirty.value) load()
  })
  return { draft, revision, dirty, stale, load }
}

export function useEditorAction() {
  const busy = ref(false)
  const error = ref('')
  async function run<T>(operation: () => Promise<T>) {
    if (busy.value) return
    busy.value = true
    error.value = ''
    try {
      return await operation()
    } catch (cause) {
      error.value = message(cause)
    } finally {
      busy.value = false
    }
  }
  return { busy, error, run }
}

export function moveItem<T>(items: T[], index: number, offset: number) {
  const other = index + offset
  if (index < 0 || other < 0 || index >= items.length || other >= items.length) return
  const item = items[index]
  items[index] = items[other]
  items[other] = item
}
