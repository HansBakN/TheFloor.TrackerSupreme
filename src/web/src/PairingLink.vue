<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { toDataURL } from 'qrcode'
import { message, request } from './api'

const props = defineProps<{ path: string }>()
const origin = ref(window.location.origin)
const origins = ref<string[]>([])
const url = computed(() => {
  try {
    const base = new URL(origin.value.trim())
    if (!['http:', 'https:'].includes(base.protocol) || base.username || base.password) return ''
    return new URL(props.path, base.origin).href
  } catch {
    return ''
  }
})
const qr = ref('')
const feedback = ref('')
const link = ref<HTMLInputElement>()

onMounted(async () => {
  try {
    origins.value = await request<string[]>('/api/host/pairing')
    if (origins.value.length === 1 && origin.value === window.location.origin) origin.value = origins.value[0]
  } catch (cause) {
    feedback.value = message(cause)
  }
})

watch(url, async (value, _, onCleanup) => {
  let stale = false
  onCleanup(() => { stale = true })
  qr.value = ''
  if (!value) return
  try {
    const image = await toDataURL(value, { width: 180, margin: 2 })
    if (!stale) qr.value = image
  } catch {
    if (!stale) feedback.value = 'The pairing link could not be encoded as a QR code.'
  }
}, { immediate: true })

async function copy() {
  try {
    await navigator.clipboard.writeText(url.value)
    feedback.value = 'Display link copied.'
  } catch {
    link.value?.select()
    feedback.value = 'Link selected. Copy it with your keyboard.'
  }
}
</script>

<template>
  <div class="pairing-grid">
    <div class="editor-form">
      <label>Server address
        <input v-model="origin" list="pairing-addresses" type="url" autocomplete="off">
      </label>
      <datalist id="pairing-addresses">
        <option v-for="address in origins" :key="address" :value="address" />
      </datalist>
      <p class="muted">For another device, choose the server’s LAN address. Both devices need to be on the same local network.</p>
      <label>Display link<input ref="link" :value="url" readonly></label>
      <div class="actions">
        <button type="button" :disabled="!url" @click="copy">Copy display link</button>
        <a v-if="url" class="button" :href="url" target="_blank" rel="noopener">Open display link</a>
      </div>
      <p v-if="feedback" role="status">{{ feedback }}</p>
    </div>
    <img v-if="qr" :src="qr" width="180" height="180" alt="QR code for the display link">
  </div>
</template>
