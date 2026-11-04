<script setup lang="ts">
import { ref } from 'vue'
import { json, message, request } from './api'
import type { AudienceStatus } from './contracts'
import PairingLink from './PairingLink.vue'

const props = defineProps<{ audience?: AudienceStatus }>()
const pending = ref(false)
const error = ref('')

async function choose(event: Event) {
  const input = event.target as HTMLSelectElement
  pending.value = true
  error.value = ''
  try {
    await request<void>('/api/host/audio-display', json('PUT', { displayId: input.value || null }))
  } catch (cause) {
    error.value = message(cause)
    input.value = props.audience?.selectedAudioDisplayId ?? ''
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <details class="panel">
    <summary>Pair displays and choose audio</summary>
    <div class="editor-form mt-3">
      <PairingLink v-if="audience" path="/display" />
      <label>Audio output display
        <select :value="audience?.selectedAudioDisplayId ?? ''" :disabled="pending || !audience" @change="choose">
          <option value="">Automatic · one connected display</option>
          <option v-if="audience?.selectedAudioDisplayId && !audience.displays.some(display => display.id === audience?.selectedAudioDisplayId)"
                  :value="audience.selectedAudioDisplayId">Selected display disconnected</option>
          <option v-for="display in audience?.displays ?? []" :key="display.id" :value="display.id">
            {{ display.name }}
          </option>
        </select>
      </label>
      <p class="muted">The code at the top of each audience screen identifies it. An explicitly selected screen keeps its assignment through a connection retry.</p>
      <p v-if="error" class="notice" role="alert">{{ error }}</p>
    </div>
  </details>
</template>
