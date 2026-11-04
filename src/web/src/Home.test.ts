import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, expect, it, vi } from 'vitest'
import Home from './Home.vue'

afterEach(() => vi.unstubAllGlobals())

it.each([
  [true, 'Local server ready'],
  [false, 'Local server unavailable'],
])('reports local server availability (%s)', async (ok, message) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok }))
  const page = mount(Home, { global: { stubs: { RouterLink: true } } })
  await flushPromises()
  expect(page.get('[role=status]').text()).toBe(message)
  page.unmount()
})
