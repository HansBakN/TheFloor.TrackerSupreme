import { defineStore } from 'pinia'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { json, message, request } from './api'
import type { Category, CategorySet, Entry, Library } from './contracts'

const root = '/api/library'

export const useLibrary = defineStore('library', () => {
  const data = ref<Library>({ categories: [], sets: [], activeSetId: null, errors: [], dataDirectory: '' })
  const error = ref('')
  const active = computed(() => data.value.sets.find(entry => entry.definition.id === data.value.activeSetId)?.definition)
  const byId = computed(() => new Map(data.value.categories.map(entry => [entry.definition.id, entry])))
  let sequence = 0

  async function refresh(reload = false) {
    const current = ++sequence
    try {
      const result = await request<Library>(root + (reload ? '/reload' : ''), reload ? json('POST') : undefined)
      if (current === sequence) {
        data.value = result
        error.value = ''
      }
    } catch (cause) {
      if (current === sequence) error.value = message(cause)
    }
  }

  async function mutate<T>(path: string, options: RequestInit) {
    const value = await request<T>(root + path, options)
    await refresh()
    return value
  }

  function saveCategory(definition: Category, revision?: string) {
    return mutate<Entry<Category>>(`/categories/${definition.id}`, json('PUT', { definition, revision }))
  }

  function saveSet(definition: CategorySet, revision?: string) {
    return mutate<Entry<CategorySet>>(`/sets/${definition.id}`, json('PUT', { definition, revision }))
  }

  return { data, error, active, byId, refresh, mutate, saveCategory, saveSet }
})

export function useLibraryUpdates() {
  const library = useLibrary()
  let timer: number | undefined
  let mounted = false

  async function poll() {
    if (!document.hidden) await library.refresh()
    if (mounted) timer = window.setTimeout(() => void poll(), 1000)
  }

  onMounted(() => {
    mounted = true
    void poll()
  })
  onUnmounted(() => {
    mounted = false
    window.clearTimeout(timer)
  })
  return library
}
