<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import type { PublicQuestion } from './contracts'

const props = withDefaults(defineProps<{
  question: PublicQuestion | null
  audience?: boolean
  playing?: boolean
}>(), { audience: false, playing: true })

const player = ref<HTMLAudioElement>()
const enabled = ref(false)
const blocked = ref(false)

async function play() {
  await nextTick()
  if (!player.value || !props.playing || !props.audience || !enabled.value) return
  try {
    await player.value.play()
    blocked.value = false
  } catch {
    blocked.value = true
  }
}

function enableAudio() {
  enabled.value = true
  void play()
}

watch(() => [props.question?.id, props.question?.mediaUrl], () => {
  blocked.value = false
  void play()
}, { flush: 'post' })

watch(() => props.playing, (playing) => {
  if (playing) void play()
  else player.value?.pause()
})
</script>

<template>
  <div v-if="question" class="question-media">
    <p v-if="question.prompt" class="question-prompt">{{ question.prompt }}</p>
    <img v-if="question.type === 'image' && question.mediaUrl" :key="question.mediaUrl"
         :src="question.mediaUrl" alt="Quiz question image">
    <template v-if="question.type === 'audio' && question.mediaUrl">
      <audio :key="question.mediaUrl" ref="player" controls preload="metadata" :src="question.mediaUrl"
             aria-label="Question audio" />
      <button v-if="audience && (!enabled || blocked)" type="button" @click="enableAudio">
        {{ blocked ? 'Play question audio' : 'Enable automatic question audio' }}
      </button>
    </template>
  </div>
</template>
