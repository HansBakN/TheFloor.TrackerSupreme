import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { expect, it, vi } from 'vitest'
import { useShortcuts } from './shortcuts'

it('routes game shortcuts, leaves text entry alone, and unregisters on unmount', async () => {
  const run = vi.fn()
  const component = mount(defineComponent({
    setup() {
      useShortcuts(() => true, run)
      return () => h('input')
    },
  }), { attachTo: document.body })

  window.dispatchEvent(new KeyboardEvent('keydown', { key: 'P' }))
  await component.trigger('keydown', { key: 'u' })
  window.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', repeat: true }))
  expect(run.mock.calls).toEqual([['pass']])
  component.unmount()
  window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
  expect(run.mock.calls).toEqual([['pass']])
})
