<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
import { categoryMedia, json, request } from './api'
import type { Category, Entry, Question } from './contracts'
import { moveItem, useDraft, useEditorAction } from './editor'
import LibraryStatus from './LibraryStatus.vue'
import PackageImport from './PackageImport.vue'
import { useLibraryUpdates } from './library'
import QuestionMedia from './QuestionMedia.vue'

const route = useRoute()
const router = useRouter()
const library = useLibraryUpdates()
const id = computed(() => typeof route.params.id === 'string' ? route.params.id : '')
const entry = computed(() => library.byId.get(id.value))
const { draft, revision, dirty, stale, load } = useDraft(() => id.value, () => entry.value)
const { busy, error, run } = useEditorAction()
const newName = ref('')
const search = ref('')
const questionSearch = ref('')
const questionFilter = ref<'all' | 'enabled' | 'disabled'>('all')
const selectedQuestion = ref('')
const showPreview = ref(false)
const selected = computed(() => draft.value?.questions.find(question => question.id === selectedQuestion.value))
const questionIndex = computed(() => draft.value?.questions.findIndex(q => q.id === selectedQuestion.value) ?? -1)
const categoryResults = computed(() => library.data.categories.filter(({ definition }) =>
  (definition.name + ' ' + (definition.description ?? '')).toLowerCase().includes(search.value.toLowerCase().trim())))
const questionResults = computed(() => (draft.value?.questions ?? []).filter(question =>
  (questionFilter.value === 'all' || question.enabled === (questionFilter.value === 'enabled')) &&
  [question.prompt, question.answer, ...question.alternatives].join(' ').toLowerCase().includes(questionSearch.value.toLowerCase().trim())))
const memberships = computed(() => library.data.sets.filter(set => set.definition.categoryIds.includes(id.value)))
const preview = computed(() => selected.value ? {
  id: selected.value.id, type: selected.value.type, prompt: selected.value.prompt ?? null,
  mediaUrl: selected.value.media ? categoryMedia(id.value, selected.value.media) : null,
} : null)
const alternatives = computed({
  get: () => selected.value?.alternatives.join('\n') ?? '',
  set: text => { if (selected.value) selected.value.alternatives = text.split('\n') },
})
let internalNavigation = false

function confirmDiscard() {
  return internalNavigation || !dirty.value || window.confirm('Discard unsaved category edits?')
}
onBeforeRouteLeave(confirmDiscard)
onBeforeRouteUpdate(confirmDiscard)
watch(id, () => {
  selectedQuestion.value = ''
  questionSearch.value = ''
  questionFilter.value = 'all'
  showPreview.value = false
})

async function goTo(categoryId: string) {
  internalNavigation = true
  try {
    await router.push(categoryId ? '/categories/' + categoryId : '/categories')
  } finally {
    internalNavigation = false
  }
}

function openImported(categoryId: string) {
  if (categoryId === id.value || !confirmDiscard()) return
  void goTo(categoryId)
}

async function create() {
  if (!newName.value.trim() || !confirmDiscard()) return
  const created = await run(() => library.saveCategory({
    schemaVersion: 1, id: crypto.randomUUID(), name: newName.value.trim(), questions: [],
  }))
  if (created) {
    newName.value = ''
    await goTo(created.definition.id)
  }
}

async function save() {
  if (!draft.value || !revision.value || stale.value) return
  const definition = { ...draft.value, questions: draft.value.questions.map(question => ({
    ...question, media: question.type === 'text' ? null : question.media,
    alternatives: question.alternatives.map(answer => answer.trim()).filter(Boolean),
  })) }
  const saved = await run(() => library.saveCategory(definition, revision.value))
  if (saved) load(saved)
}

function selectQuestion(question: Question) {
  selectedQuestion.value = question.id
  showPreview.value = false
}

function addQuestion(source?: Question) {
  if (!draft.value) return
  const question: Question = source
    ? { ...source, id: crypto.randomUUID(), alternatives: [...source.alternatives] }
    : { id: crypto.randomUUID(), type: 'text', answer: '', alternatives: [], enabled: true }
  const index = source ? questionIndex.value + 1 : draft.value.questions.length
  draft.value.questions.splice(index, 0, question)
  questionSearch.value = ''
  questionFilter.value = 'all'
  selectQuestion(question)
}

function removeQuestion() {
  if (!draft.value || questionIndex.value < 0 || !window.confirm('Remove this question from the category?')) return
  draft.value.questions.splice(questionIndex.value, 1)
  selectedQuestion.value = ''
}

async function upload(event: Event, question?: Question) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  const category = draft.value
  input.value = ''
  if (!file || !category) return
  const form = new FormData()
  form.set('file', file)
  const uploaded = await run(() => request<{ path: string }>('/api/library/categories/' + category.id + '/media',
    { method: 'POST', body: form }))
  if (uploaded) {
    if (question) question.media = uploaded.path
    else category.coverImage = uploaded.path
  }
}

async function toggleMembership(setId: string, checked: boolean) {
  const entry = library.data.sets.find(item => item.definition.id === setId)
  if (!entry || busy.value) return
  const ids = entry.definition.categoryIds.filter(categoryId => categoryId !== id.value)
  if (checked) ids.push(id.value)
  await run(() => library.saveSet({ ...entry.definition, categoryIds: ids }, entry.revision))
}

async function duplicate() {
  if (!draft.value || !confirmDiscard()) return
  const copy = await run(() => library.mutate<Entry<Category>>('/categories/' + id.value + '/duplicate', json('POST')))
  if (copy) await goTo(copy.definition.id)
}

async function remove() {
  if (!draft.value || !revision.value || stale.value) return
  const names = memberships.value.map(set => set.definition.name)
  const impact = names.length ? ' This removes its references from ' + names.join(', ') + '.' : ''
  if (!window.confirm('Delete “' + draft.value.name + '” from the shared library?' + impact)) return
  const deleted = await run(async () => {
    await library.mutate('/categories/' + id.value + '/delete', json('POST', {
      revision: revision.value, confirmedSets: memberships.value.map(set => set.definition.id),
    }))
    return true
  })
  if (deleted) await goTo('')
}
</script>

<template>
  <main class="workspace">
    <header class="page-header">
      <div><p class="eyebrow">Content library</p><h1>Categories</h1></div>
      <button type="button" @click="library.refresh(true)">Reload files</button>
    </header>
    <LibraryStatus />
    <p v-if="error" class="notice" role="alert">{{ error }}</p>
    <div class="editor-grid">
      <section class="panel editor-list">
        <form class="editor-form" @submit.prevent="create">
          <label>New category name<input v-model="newName" required maxlength="150"></label>
          <button class="primary" :disabled="busy">Create category</button>
        </form>
        <PackageImport kind="categories" @imported="openImported" />
        <label>Search categories<input v-model="search" type="search"></label>
        <p v-if="!categoryResults.length" class="muted">No matching categories.</p>
        <ul class="entry-list">
          <li v-for="{ definition } in categoryResults" :key="definition.id" :class="{ selected: definition.id === id }">
            <RouterLink :to="'/categories/' + definition.id">{{ definition.name }}</RouterLink>
            <p class="muted">{{ definition.questions.length }} {{ definition.questions.length === 1 ? 'question' : 'questions' }} ·
              Sets {{ library.data.sets.filter(set => set.definition.categoryIds.includes(definition.id)).length }}</p>
            <p v-if="definition.description" class="muted">{{ definition.description }}</p>
          </li>
        </ul>
      </section>

      <section v-if="draft" class="panel editor-form">
        <div class="actions">
          <h2 class="mr-auto">Edit category</h2>
          <button type="button" :disabled="busy || stale" @click="duplicate">Duplicate independently</button>
          <button type="button" :disabled="busy || stale" @click="remove">Delete category</button>
        </div>
        <div v-if="stale" class="notice" role="alert">
          This category changed on disk. Load the latest version before saving.
          <button type="button" @click="confirmDiscard() && load()">Load latest version</button>
        </div>
        <form class="editor-form" @submit.prevent="save">
          <fieldset class="editor-form" :disabled="busy">
            <label>Category name<input v-model.trim="draft.name" maxlength="150" required></label>
            <label>Description<textarea v-model="draft.description" rows="2" maxlength="5000"></textarea></label>
            <div class="actions">
              <label>Cover image
                <input type="file" accept="image/jpeg,image/png,image/webp,image/gif" :disabled="busy" @change="upload">
              </label>
              <img v-if="draft.coverImage" class="cover-preview" :src="categoryMedia(id, draft.coverImage)" alt="Category cover">
              <button v-if="draft.coverImage" type="button" @click="draft.coverImage = null">Clear cover</button>
            </div>

            <div class="actions">
              <h3 class="eyebrow mr-auto">Questions · {{ draft.questions.length }}</h3>
              <button type="button" @click="addQuestion()">Add question</button>
            </div>
            <div class="form-grid">
              <label>Search questions<input v-model="questionSearch" type="search"></label>
              <label>Question status<select v-model="questionFilter">
                <option value="all">All questions</option><option value="enabled">Enabled</option><option value="disabled">Disabled</option>
              </select></label>
            </div>
            <p v-if="!questionResults.length" class="muted">No matching questions.</p>
            <ol class="entry-list question-list">
              <li v-for="question in questionResults" :key="question.id" :class="{ selected: question.id === selectedQuestion }">
                <button class="entry-button" type="button" @click="selectQuestion(question)">
                  {{ draft.questions.indexOf(question) + 1 }}. {{ question.prompt || question.answer || 'New question' }}
                </button>
                <p class="muted">{{ question.type }} · {{ question.enabled ? 'Enabled' : 'Disabled' }}<span v-if="question.answer"> · {{ question.answer }}</span></p>
              </li>
            </ol>
            <fieldset v-if="selected" class="question-editor editor-form">
              <legend>Edit question {{ questionIndex + 1 }}</legend>
              <div class="actions">
                <button type="button" :disabled="questionIndex <= 0" @click="moveItem(draft.questions, questionIndex, -1)">Move up</button>
                <button type="button" :disabled="questionIndex === draft.questions.length - 1" @click="moveItem(draft.questions, questionIndex, 1)">Move down</button>
                <button type="button" @click="addQuestion(selected)">Duplicate question</button>
                <button type="button" @click="removeQuestion">Remove question</button>
              </div>
              <label>Question type<select v-model="selected.type" @change="selected.media = null">
                <option value="text">Text</option><option value="image">Image</option><option value="audio">Audio</option>
              </select></label>
              <label>Prompt<textarea v-model="selected.prompt" rows="2" maxlength="5000" :required="selected.type === 'text'"></textarea></label>
              <label v-if="selected.type !== 'text'">Question media
                <input type="file" :accept="selected.type === 'image' ? 'image/jpeg,image/png,image/webp,image/gif' : 'audio/mpeg,audio/wav,audio/ogg,.mp3,.wav,.ogg'"
                       :disabled="busy" @change="upload($event, selected)">
              </label>
              <p v-if="selected.type !== 'text'" class="muted">{{ selected.media ?? 'Upload a file for this question.' }}</p>
              <label>Correct answer<input v-model.trim="selected.answer" required maxlength="2000"></label>
              <label>Alternative answers (one per line)<textarea v-model="alternatives" rows="2"></textarea></label>
              <label>Host notes<textarea v-model="selected.notes" rows="2" maxlength="5000"></textarea></label>
              <label class="check"><input v-model="selected.enabled" type="checkbox"> Enabled for duels</label>
              <button type="button" @click="showPreview = !showPreview">{{ showPreview ? 'Hide preview' : 'Preview question' }}</button>
              <QuestionMedia v-if="showPreview" :question="preview" />
            </fieldset>
            <button class="primary" :disabled="busy || !dirty || stale">Save category</button>
          </fieldset>
        </form>
        <hr>
        <div v-if="entry" class="actions">
          <a class="button" :href="'/api/library/categories/' + id + '/export'" download>Export category ZIP</a>
          <a v-if="!entry.definition.coverImage && entry.definition.questions.every(q => q.type === 'text')"
             class="button" :href="'/api/library/categories/' + id + '/export.json'" download>Export text JSON</a>
        </div>
        <h3 class="eyebrow">Category set membership</h3>
        <p class="muted">Every set that includes this category sees edits to its questions and media.</p>
        <p v-if="!library.data.sets.length" class="muted">Create a category set to use this category in a duel.</p>
        <label v-for="{ definition } in library.data.sets" :key="definition.id" class="check">
          <input type="checkbox" :checked="definition.categoryIds.includes(id)" :disabled="busy || !entry"
                 @change="toggleMembership(definition.id, ($event.target as HTMLInputElement).checked)">
          {{ definition.name }}
        </label>
      </section>
      <section v-else class="panel muted">{{ id ? 'This category is unavailable.' : 'Select a category to edit its details and questions.' }}</section>
    </div>
    <p v-if="library.data.dataDirectory" class="muted data-path">Data directory {{ library.data.dataDirectory }}</p>
  </main>
</template>
