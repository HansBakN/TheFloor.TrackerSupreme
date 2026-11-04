<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { json, message, request } from './api'
import type { RehearsalPreview } from './contracts'
import { useLibraryUpdates } from './library'
import LibraryStatus from './LibraryStatus.vue'
import PairingLink from './PairingLink.vue'
import QuestionMedia from './QuestionMedia.vue'

const library = useLibraryUpdates()
const minimum = ref(5)
const categoryId = ref('')
const questionId = ref('')
const preview = ref<RehearsalPreview | null>(null)
const error = ref('')
const busy = ref(false)
const categories = computed(() => (library.active?.categoryIds ?? []).map(id => {
  const category = library.byId.get(id)?.definition
  const enabled = category?.questions.filter(question => question.enabled).length ?? 0
  return { id, category, enabled, disabled: (category?.questions.length ?? 0) - enabled }
}))
const questions = computed(() => library.byId.get(categoryId.value)?.definition.questions ?? [])
watch(categoryId, () => { questionId.value = '' })

onMounted(async () => {
  try {
    preview.value = await request<RehearsalPreview | null>('/api/host/rehearsal')
    await library.refresh(true)
  } catch (cause) {
    error.value = message(cause)
  }
})

async function show() {
  error.value = ''
  busy.value = true
  try {
    preview.value = await request<RehearsalPreview>('/api/host/rehearsal', json('PUT', {
      categoryId: categoryId.value, questionId: questionId.value,
    }))
  } catch (cause) {
    error.value = message(cause)
  } finally {
    busy.value = false
  }
}

async function end() {
  error.value = ''
  busy.value = true
  try {
    await request<void>('/api/host/rehearsal/end', json('POST'))
    preview.value = null
  } catch (cause) {
    error.value = message(cause)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <main class="workspace">
    <header class="page-header">
      <div><p class="eyebrow">Event setup</p><h1>Rehearsal</h1></div>
      <RouterLink class="button" to="/host">Back to host</RouterLink>
    </header>
    <p v-if="error" class="notice" role="alert">{{ error }}</p>
    <LibraryStatus />
    <div class="editor-grid">
      <section class="panel editor-form">
        <h2>Active-set preflight</h2>
        <p>Active set <strong>{{ library.active?.name ?? 'None selected' }}</strong></p>
        <label>Minimum enabled questions
          <input v-model.number="minimum" type="number" min="1" max="2000">
        </label>
        <p v-if="!categories.length" class="muted">Choose a populated set to check its categories.</p>
        <ul class="entry-list">
          <li v-for="item in categories" :key="item.id">
            <strong>{{ item.category?.name ?? 'Unavailable category' }}</strong>
            <p v-if="item.category">{{ item.enabled }} enabled · {{ item.disabled }} disabled</p>
            <p v-if="item.category && item.enabled < minimum" class="muted">Below the minimum question count.</p>
            <p v-if="!item.category" class="muted">Check the library diagnostics for this category.</p>
          </li>
        </ul>
        <button type="button" @click="library.refresh(true)">Recheck content files</button>
      </section>
      <section class="panel editor-form">
        <h2>Rehearsal display</h2>
        <p class="muted">Send a question to a separate rehearsal link to check sizing, audio volume, and fullscreen. The live duel keeps its own clocks and questions.</p>
        <form class="editor-form" @submit.prevent="show">
          <label>Rehearsal category
            <select v-model="categoryId" required>
              <option value="">Choose a category</option>
              <option v-for="item in categories.filter(item => item.category)" :key="item.id" :value="item.id">{{ item.category?.name }}</option>
            </select>
          </label>
          <label>Rehearsal question
            <select v-model="questionId" required>
              <option value="">Choose a question</option>
              <option v-for="(question, index) in questions" :key="question.id" :value="question.id">
                {{ index + 1 }} · {{ question.type }} · {{ question.prompt || 'Media question' }}
              </option>
            </select>
          </label>
          <button class="primary" :disabled="busy || !questionId">Show on rehearsal display</button>
        </form>
        <template v-if="preview">
          <PairingLink :path="'/display/rehearsal/' + preview.id" />
          <QuestionMedia :question="preview.question" />
          <button type="button" :disabled="busy" @click="end">End rehearsal</button>
        </template>
      </section>
    </div>
  </main>
</template>
