import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import App from './App.vue'
import CategoryEditor from './CategoryEditor.vue'
import Display from './Display.vue'
import Host from './Host.vue'
import SetEditor from './SetEditor.vue'
import type { AudienceDuel, Library } from './contracts'
import { useLiveDuel } from './live'

vi.mock('./live', async () => {
  const { reactive } = await import('vue')
  const live = reactive({
    status: 'live', audienceCount: 0, sample: undefined, host: undefined,
    pending: false, error: '', prepare: vi.fn(), command: vi.fn(),
  })
  return { useLiveDuel: () => live }
})

const categoryId = '115c59c1-3fd8-4d4d-beb8-cba259bf4792'
const setId = '57a2aa61-4c0f-4919-838d-8c9071feb443'
const initialLibrary: Library = {
  activeSetId: null, errors: [], dataDirectory: '/isolated',
  sets: [
    { revision: 'set1', definition: { schemaVersion: 1, id: setId, name: 'Shared event', categoryIds: [categoryId] } },
    { revision: 'set2', definition: { schemaVersion: 1, id: 'second', name: 'Second event', categoryIds: [categoryId] } },
  ],
  categories: [{
    revision: 'cat1', definition: {
      schemaVersion: 1, id: categoryId, name: 'Geography', questions: [{
        id: 'question', type: 'text', prompt: 'Capital of Denmark?', answer: 'Copenhagen',
        alternatives: ['København'], notes: 'Host note', enabled: true,
      }],
    },
  }],
}
const duel: AudienceDuel = {
  id: 'duel', version: 3, status: 'paused', active: 'a', playerA: 'Martin', playerB: 'Thomas',
  categoryName: 'Geography', aMilliseconds: 12_320, bMilliseconds: 23_000, winner: null, passMilliseconds: 0, sampledAt: 100,
  question: { id: 'question', type: 'text', prompt: 'Capital of Denmark?', mediaUrl: null },
}

let library: Library

async function view(path: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<div />' } },
      { path: '/categories/:id?', component: CategoryEditor },
      { path: '/sets', component: SetEditor },
      { path: '/host', component: Host },
      { path: '/display', component: Display },
    ],
  })
  await router.push(path)
  const wrapper = mount(App, { global: { plugins: [router, createPinia()] } })
  await flushPromises()
  return wrapper
}

beforeEach(() => {
  library = structuredClone(initialLibrary)
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    if (url.endsWith('/activate')) {
      library.activeSetId = setId
      return new Response(null, { status: 204 })
    }
    return Response.json(library)
  }))
  const live = useLiveDuel('host')
  live.sample = undefined
  live.host = undefined
  live.error = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

it('lists set references and updates active selection, then stops refreshing when unmounted', async () => {
  const cleared = vi.spyOn(window, 'clearTimeout')
  const wrapper = await view('/sets')
  await wrapper.get('.entry-button').trigger('click')
  expect(wrapper.get('.ordered-list').text()).toContain('Geography')
  const activate = wrapper.findAll('button').find(button => button.text() === 'Make active')!
  await activate.trigger('click')
  await flushPromises()
  expect(wrapper.get('.tag').text()).toBe('Active')
  wrapper.unmount()
  expect(cleared).toHaveBeenCalled()
})

it('shows shared category memberships and edits and previews a question draft', async () => {
  const wrapper = await view('/categories/' + categoryId)
  expect(wrapper.text()).toContain('Shared event')
  expect(wrapper.text()).toContain('Second event')
  await wrapper.get('.question-list button').trigger('click')
  expect(wrapper.get('input[maxlength="2000"]').element).toHaveProperty('value', 'Copenhagen')
  expect(wrapper.findAll('textarea').map(field => field.element.value)).toContain('København')
  await wrapper.get('.question-editor textarea').setValue('Updated prompt?')
  const preview = wrapper.findAll('button').find(button => button.text() === 'Preview question')!
  await preview.trigger('click')
  expect(wrapper.get('.question-prompt').text()).toBe('Updated prompt?')
  wrapper.unmount()
})

it('shows host-only guidance and an error while the audience gets only public content', async () => {
  const live = useLiveDuel('host')
  live.sample = { duel, receivedAt: 0 }
  live.host = {
    duel, categorySetId: setId,
    configuration: { playerA: 'Martin', playerB: 'Thomas', categoryId, seconds: 45, passSeconds: 3, starter: 'a', order: 'ordered' },
    answer: 'Copenhagen', alternatives: ['København'], notes: 'Host note', canUndo: true, exhausted: false,
  }
  live.error = 'The duel changed. Reload state.'
  const host = await view('/host')
  expect(host.get('.answer').text()).toBe('Copenhagen')
  expect(host.get('[role="alert"]').text()).toContain('Reload state.')
  const resume = host.findAll('button').find(button => button.text().startsWith('Resume'))!
  await resume.trigger('click')
  expect(live.command).toHaveBeenCalledWith('resume', undefined)
  host.unmount()

  const display = await view('/display')
  expect(display.get('.question-prompt').text()).toBe('Capital of Denmark?')
  expect(display.get('[data-clock="a"]').text()).toBe('12.4')
  expect(display.text()).not.toContain('Copenhagen')
  expect(display.text()).not.toContain('Host note')
  display.unmount()
})

it('blocks answer buttons and shortcuts throughout a pass lockout and allows pause and undo', async () => {
  const live = useLiveDuel('host')
  vi.mocked(live.command).mockClear()
  const locked = { ...duel, status: 'running' as const, passMilliseconds: 3000, question: null }
  live.sample = { duel: locked, receivedAt: performance.now() }
  live.host = {
    duel: locked, categorySetId: setId,
    configuration: { playerA: 'Martin', playerB: 'Thomas', categoryId, seconds: 45, passSeconds: 3, starter: 'a', order: 'ordered' },
    answer: null, alternatives: [], notes: null, canUndo: true, exhausted: false,
  }
  const host = await view('/host')
  const button = (name: string) => host.findAll('button').find(item => item.text().startsWith(name))!
  expect(button('Correct').element).toHaveProperty('disabled', true)
  expect(button('Pass').element).toHaveProperty('disabled', true)
  expect(button('Pause').element).toHaveProperty('disabled', false)
  expect(button('Undo').element).toHaveProperty('disabled', false)
  expect(host.find('.host-question').exists()).toBe(false)
  window.dispatchEvent(new KeyboardEvent('keydown', { key: 'p' }))
  window.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }))
  expect(live.command).not.toHaveBeenCalled()
  live.host.duel = { ...duel, status: 'running', passMilliseconds: 0 }
  await flushPromises()
  expect(button('Correct').element).toHaveProperty('disabled', false)
  expect(button('Pass').element).toHaveProperty('disabled', false)
  host.unmount()
})

it('requires confirmation before ending an ongoing duel and returning directly to setup', async () => {
  const live = useLiveDuel('host')
  vi.mocked(live.command).mockClear()
  live.sample = { duel, receivedAt: performance.now() }
  live.host = {
    duel, categorySetId: setId,
    configuration: { playerA: 'Martin', playerB: 'Thomas', categoryId, seconds: 45, passSeconds: 3, starter: 'a', order: 'ordered' },
    answer: 'Copenhagen', alternatives: [], notes: null, canUndo: false, exhausted: false,
  }
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
  const host = await view('/host')
  const end = host.findAll('.controls button').find(button => button.text() === 'End duel and return to setup')!
  await end.trigger('click')
  expect(live.command).not.toHaveBeenCalled()
  confirm.mockReturnValue(true)
  await end.trigger('click')
  expect(confirm).toHaveBeenCalledWith('End the duel without a winner and return to setup?')
  expect(live.command).toHaveBeenCalledExactlyOnceWith('return', undefined)
  host.unmount()
})

it('shows waiting and lockout states without mounting question media', async () => {
  const live = useLiveDuel('audience')
  live.sample = { duel: { ...duel, status: 'ready', question: null }, receivedAt: performance.now() }
  const display = await view('/display')
  expect(display.text()).toContain('Ready to start')
  expect(display.find('.question-prompt').exists()).toBe(false)
  expect(display.find('img, audio').exists()).toBe(false)
  live.sample = { duel: { ...duel, status: 'running', passMilliseconds: 3000, question: null }, receivedAt: performance.now() }
  await flushPromises()
  expect(display.text()).toContain('Pass lockout')
  expect(display.find('.question-prompt').exists()).toBe(false)
  expect(display.find('img, audio').exists()).toBe(false)
  display.unmount()
})
