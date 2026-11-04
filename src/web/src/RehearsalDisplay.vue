<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { message, request } from './api'
import type { RehearsalPreview } from './contracts'
import QuestionMedia from './QuestionMedia.vue'

const route = useRoute()
const preview = ref<RehearsalPreview | null>(null)
const error = ref('')
const fullscreenError = ref('')
const lifetime = new AbortController()
let timeout: number | undefined
let generation = 0

async function refresh() {
  const current = ++generation
  const id = String(route.params.id)
  try {
    const next = await request<RehearsalPreview | null>('/api/display/rehearsal/' + encodeURIComponent(id), {
      signal: AbortSignal.any([lifetime.signal, AbortSignal.timeout(5000)]),
    }, false)
    if (current !== generation || lifetime.signal.aborted) return
    preview.value = next
    error.value = ''
  } catch (cause) {
    if (current === generation && !lifetime.signal.aborted) error.value = message(cause)
  } finally {
    if (current === generation && !lifetime.signal.aborted) timeout = window.setTimeout(() => void refresh(), 600)
  }
}

async function fullscreen() {
  fullscreenError.value = ''
  try {
    if (document.fullscreenElement) await document.exitFullscreen()
    else await document.documentElement.requestFullscreen()
  } catch (cause) {
    fullscreenError.value = message(cause)
  }
}

onMounted(() => void refresh())
watch(() => route.params.id, () => {
  window.clearTimeout(timeout)
  preview.value = null
  void refresh()
})
onUnmounted(() => {
  lifetime.abort()
  window.clearTimeout(timeout)
})
</script>

<template>
  <main class="arena">
    <div class="arena-meta">
      <p class="eyebrow">Rehearsal · clocks off</p>
      <button type="button" @click="fullscreen">Toggle fullscreen</button>
    </div>
    <p v-if="error || fullscreenError" class="notice" role="alert">{{ error || fullscreenError }}</p>
    <template v-if="preview">
      <h1 class="arena-category">{{ preview.categoryName }}</h1>
      <div class="arena-question"><QuestionMedia :question="preview.question" adjust-volume /></div>
    </template>
    <div v-else class="arena-idle"><h1>Rehearsal is waiting for a preview</h1></div>
  </main>
</template>
