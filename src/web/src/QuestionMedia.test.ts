import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import QuestionMedia from './QuestionMedia.vue'

it('plays video questions as a muted, controls-free loop', () => {
  const wrapper = mount(QuestionMedia, {
    props: { audience: true, question: { id: 'q', type: 'video', prompt: 'Name this', mediaUrl: '/clip.mp4' } as never },
  })
  const video = wrapper.get('video')
  expect(video.attributes('src')).toBe('/clip.mp4')
  expect(video.element.loop).toBe(true)
  expect(video.element.muted).toBe(true)
  expect(video.attributes('controls')).toBeUndefined()
  expect(wrapper.find('img, audio').exists()).toBe(false)
})
