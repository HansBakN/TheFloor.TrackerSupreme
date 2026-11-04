<script setup lang="ts">
import { ref } from 'vue'
import type { Configuration } from './contracts'

type Options = Pick<Configuration, 'seconds' | 'passSeconds' | 'starter' | 'order'>
type Preset = Options & { name: string }

const props = defineProps<{ options: Options }>()
const emit = defineEmits<{ apply: [options: Options] }>()
const storageKey = 'thefloor.duel-presets.v1'
const error = ref('')
const feedback = ref('')

function valid(options: Options) {
  return Number.isInteger(options.seconds) && options.seconds >= 1 && options.seconds <= 3600 &&
    Number.isInteger(options.passSeconds) && options.passSeconds >= 0 && options.passSeconds <= 3600 &&
    ['a', 'b', 'random'].includes(options.starter) && ['ordered', 'shuffled'].includes(options.order)
}

function load(): Preset[] {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(storageKey) ?? '[]')
    if (!Array.isArray(stored)) return []
    return stored.filter((item): item is Preset => item && typeof item.name === 'string' &&
      item.name.length > 0 && item.name.length <= 60 && valid(item)).slice(0, 30)
  } catch {
    error.value = 'Saved presets could not be read in this browser.'
    return []
  }
}

const presets = ref(load())
const selected = ref('')
const name = ref('')

function persist(next: Preset[]) {
  try {
    localStorage.setItem(storageKey, JSON.stringify(next))
    presets.value = next
    return true
  } catch {
    error.value = 'This browser could not save your duel presets.'
    return false
  }
}

function save() {
  error.value = ''
  feedback.value = ''
  const label = name.value.trim()
  if (!label || !valid(props.options)) {
    error.value = 'Enter a preset name and valid clock settings.'
    return
  }
  const remaining = presets.value.filter(preset => preset.name.toLowerCase() !== label.toLowerCase())
  if (remaining.length >= 30) {
    error.value = 'Delete a preset before adding another. This browser can store up to 30.'
    return
  }
  const { seconds, passSeconds, starter, order } = props.options
  if (persist([...remaining, { name: label, seconds, passSeconds, starter, order }])) {
    selected.value = label
    name.value = ''
    feedback.value = 'Preset saved.'
  }
}

function apply() {
  const preset = presets.value.find(item => item.name === selected.value)
  if (preset) {
    const { seconds, passSeconds, starter, order } = preset
    emit('apply', { seconds, passSeconds, starter, order })
    feedback.value = 'Preset applied.'
  }
}

function remove() {
  if (!selected.value) return
  if (persist(presets.value.filter(preset => preset.name !== selected.value))) {
    selected.value = ''
    feedback.value = 'Preset deleted.'
  }
}
</script>

<template>
  <details>
    <summary>Duel presets</summary>
    <div class="editor-form mt-3">
      <p class="muted">Presets are stored in this browser and apply only to clock settings, starting player, and question order.</p>
      <div class="form-grid">
        <label>Saved preset
          <select v-model="selected">
            <option value="">Choose a preset</option>
            <option v-for="preset in presets" :key="preset.name">{{ preset.name }}</option>
          </select>
        </label>
        <label>Preset name<input v-model="name" maxlength="60" @keydown.enter.prevent="save"></label>
      </div>
      <div class="actions">
        <button type="button" :disabled="!selected" @click="apply">Apply preset</button>
        <button type="button" :disabled="!name.trim()" @click="save">Save current settings</button>
        <button type="button" :disabled="!selected" @click="remove">Delete preset</button>
      </div>
      <p v-if="error" class="notice" role="alert">{{ error }}</p>
      <p v-if="feedback" role="status">{{ feedback }}</p>
    </div>
  </details>
</template>
