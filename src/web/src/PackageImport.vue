<script setup lang="ts">
import { ref } from 'vue'
import { ApiError, message } from './api'
import { useLibrary } from './library'

type ImportResult = { setId: string | null; categoryId: string | null; reusedCategories: number; copiedCategories: number }

const props = defineProps<{ kind: 'sets' | 'categories' }>()
const emit = defineEmits<{ imported: [id: string] }>()
const library = useLibrary()
const busy = ref(false)
const feedback = ref('')
const failure = ref('')
const conflict = ref<File>()

async function upload(file: File, copyConflicts = false) {
  if (busy.value) return
  busy.value = true
  failure.value = ''
  feedback.value = ''
  conflict.value = undefined
  const text = props.kind === 'categories' && file.name.toLowerCase().endsWith('.json')
  const path = '/' + props.kind + '/import' + (text ? '.json' : '') + (copyConflicts ? '?copyConflicts=true' : '')
  try {
    const result = await library.mutate<ImportResult>(path, {
      method: 'POST', body: file, headers: { 'Content-Type': text ? 'application/json' : 'application/zip' },
    })
    feedback.value = 'Imported. ' + result.reusedCategories + ' categories reused, ' + result.copiedCategories + ' copied.'
    const id = result.setId ?? result.categoryId
    if (id) emit('imported', id)
  } catch (cause) {
    failure.value = message(cause)
    if (cause instanceof ApiError && cause.status === 409) conflict.value = file
  } finally {
    busy.value = false
  }
}

function pick(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) void upload(file)
}
</script>

<template>
  <div class="editor-form">
    <label>
      {{ kind === 'sets' ? 'Import set ZIP' : 'Import category ZIP or text JSON' }}
      <input type="file" :accept="kind === 'sets' ? '.zip' : '.zip,.json'" :disabled="busy" @change="pick">
    </label>
    <p v-if="feedback" role="status" class="muted">{{ feedback }}</p>
    <p v-if="failure" role="alert" class="notice">{{ failure }}</p>
    <button v-if="conflict" type="button" :disabled="busy" @click="upload(conflict, true)">Import independent copies</button>
  </div>
</template>
