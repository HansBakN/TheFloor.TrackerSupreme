<script setup lang="ts">
import { computed, ref } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { json } from './api'
import type { CategorySet, Entry } from './contracts'
import { moveItem, useDraft, useEditorAction } from './editor'
import LibraryStatus from './LibraryStatus.vue'
import PackageImport from './PackageImport.vue'
import { useLibraryUpdates } from './library'

const library = useLibraryUpdates()
const selected = ref('')
const newName = ref('')
const adding = ref('')
const independent = ref(false)
const entry = computed(() => library.data.sets.find(item => item.definition.id === selected.value))
const { draft, revision, dirty, stale, load } = useDraft(() => selected.value, () => entry.value)
const { busy, error, run } = useEditorAction()
const available = computed(() => library.data.categories.filter(item => !draft.value?.categoryIds.includes(item.definition.id)))

function confirmDiscard() {
  return !dirty.value || window.confirm('Discard unsaved set edits?')
}
onBeforeRouteLeave(confirmDiscard)

function select(id: string) {
  if (id === selected.value || !confirmDiscard()) return
  selected.value = id
  adding.value = ''
  independent.value = false
}

async function create() {
  if (!newName.value.trim() || !confirmDiscard()) return
  const created = await run(() => library.saveSet({
    id: crypto.randomUUID(), schemaVersion: 1, name: newName.value.trim(), categoryIds: [],
  }))
  if (created) {
    selected.value = created.definition.id
    newName.value = ''
  }
}

async function save() {
  if (!draft.value || !revision.value) return
  const definition = draft.value
  const saved = await run(() => library.saveSet(definition, revision.value))
  if (saved) load(saved)
}

async function duplicate() {
  const id = selected.value
  if (!id || !confirmDiscard()) return
  const copy = await run(() => library.mutate<Entry<CategorySet>>('/sets/' + id + '/duplicate',
    json('POST', { copyCategories: independent.value })))
  if (copy) selected.value = copy.definition.id
}

async function activate(id: string) {
  await run(() => library.mutate('/sets/' + id + '/activate', json('POST')))
}

async function remove() {
  if (!draft.value || !revision.value || stale.value) return
  if (!window.confirm('Delete the set “' + draft.value.name + '”? Its categories stay in the library.')) return
  const deleted = await run(async () => {
    await library.mutate('/sets/' + selected.value + '/delete', json('POST', { revision: revision.value }))
    return true
  })
  if (deleted) selected.value = ''
}

function addCategory() {
  if (!draft.value || !adding.value || !available.value.some(item => item.definition.id === adding.value)) return
  draft.value.categoryIds.push(adding.value)
  adding.value = ''
}
</script>

<template>
  <main class="workspace">
    <header class="page-header">
      <div><p class="eyebrow">Content library</p><h1>Category sets</h1></div>
      <button type="button" @click="library.refresh(true)">Reload files</button>
    </header>
    <LibraryStatus />
    <p v-if="error" class="notice" role="alert">{{ error }}</p>
    <div class="editor-grid">
      <section class="panel editor-list">
        <form class="editor-form" @submit.prevent="create">
          <label>New set name<input v-model="newName" maxlength="150" required></label>
          <button class="primary" :disabled="busy">Create set</button>
        </form>
        <PackageImport kind="sets" @imported="select" />
        <p v-if="!library.data.sets.length" class="muted">Create a set to start organizing categories.</p>
        <ul class="entry-list">
          <li v-for="{ definition } in library.data.sets" :key="definition.id" :class="{ selected: definition.id === selected }">
            <button class="entry-button" type="button" @click="select(definition.id)">
              {{ definition.name }} <span v-if="library.data.activeSetId === definition.id" class="tag">Active</span>
            </button>
            <p v-if="definition.description" class="muted">{{ definition.description }}</p>
            <p class="muted">{{ definition.categoryIds.length }} categories</p>
          </li>
        </ul>
      </section>

      <section v-if="draft" class="panel editor-form">
        <div class="actions">
          <h2 class="mr-auto">Edit set</h2>
          <button type="button" :disabled="busy || library.data.activeSetId === selected || !entry" @click="activate(selected)">
            {{ library.data.activeSetId === selected ? 'Active set' : 'Make active' }}
          </button>
        </div>
        <div v-if="stale" class="notice" role="alert">
          This set changed on disk. Reload the latest version before saving.
          <button type="button" @click="confirmDiscard() && load()">Load latest version</button>
        </div>
        <form class="editor-form" @submit.prevent="save">
          <fieldset class="editor-form" :disabled="busy">
            <label>Set name<input v-model.trim="draft.name" maxlength="150" required></label>
            <label>Description<textarea v-model="draft.description" rows="2" maxlength="5000"></textarea></label>
            <h3 class="eyebrow">Category order</h3>
            <p class="muted">Categories are shared. Removing a reference here keeps the category in the library.</p>
            <ol class="ordered-list">
              <li v-for="(id, index) in draft.categoryIds" :key="id">
                <RouterLink v-if="library.byId.has(id)" :to="'/categories/' + id">{{ library.byId.get(id)?.definition.name }}</RouterLink>
                <span v-else>Missing category {{ id }}</span>
                <div class="actions">
                  <button type="button" :disabled="index === 0" :aria-label="'Move category ' + (index + 1) + ' up'" @click="moveItem(draft.categoryIds, index, -1)">↑</button>
                  <button type="button" :disabled="index === draft.categoryIds.length - 1" :aria-label="'Move category ' + (index + 1) + ' down'" @click="moveItem(draft.categoryIds, index, 1)">↓</button>
                  <button type="button" @click="draft.categoryIds.splice(index, 1)">Remove</button>
                </div>
              </li>
            </ol>
            <div class="actions">
              <select v-model="adding" aria-label="Existing category">
                <option value="">Choose a category</option>
                <option v-for="item in available" :key="item.definition.id" :value="item.definition.id">{{ item.definition.name }}</option>
              </select>
              <button type="button" :disabled="!adding" @click="addCategory">Add category</button>
            </div>
            <button class="primary" :disabled="busy || !dirty || stale">Save set</button>
          </fieldset>
        </form>
        <hr>
        <div class="actions">
          <label class="check"><input v-model="independent" type="checkbox"> Copy categories independently</label>
          <button type="button" :disabled="busy || stale" @click="duplicate">Duplicate set</button>
          <button type="button" :disabled="busy || stale" @click="remove">Delete set</button>
          <a v-if="entry" class="button" :href="'/api/library/sets/' + selected + '/export'" download>Export set ZIP</a>
        </div>
        <p class="muted">A duplicate normally references the same categories. Choose independent copies to edit the duplicates separately.</p>
      </section>
      <section v-else class="panel muted">Select a set to edit its details and category order.</section>
    </div>
    <p v-if="library.data.dataDirectory" class="muted data-path">Data directory {{ library.data.dataDirectory }}</p>
  </main>
</template>
