<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import type { PublicQuestion } from './contracts'

const props = withDefaults(defineProps<{
  question: PublicQuestion | null
  audience?: boolean
  playing?: boolean
  audioAllowed?: boolean
  adjustVolume?: boolean
}>(), { audience: false, playing: true, audioAllowed: true })

const player = ref<HTMLAudioElement>()
const enabled = ref(false)
const blocked = ref(false)
const mediaFailed = ref(false)
const volume = ref(0.7)
watch([player, volume], ([element, level]) => {
  if (props.adjustVolume && element) element.volume = level
})

async function play() {
  await nextTick()
  if (!player.value || !props.playing || !props.audioAllowed || !props.audience || !enabled.value) return
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
  mediaFailed.value = false
  void play()
}, { flush: 'post' })

watch(() => props.playing && props.audioAllowed, (canPlay) => {
  if (canPlay) void play()
  else player.value?.pause()
})
</script>

<template>
  <div v-if="question" class="question-media">
    <p v-if="question.prompt" class="question-prompt">{{ question.prompt }}</p>
    <img v-if="question.type === 'image' && question.mediaUrl" :key="question.mediaUrl"
         :src="question.mediaUrl" alt="Quiz question image" @error="mediaFailed = true">
    <video v-if="question.type === 'video' && question.mediaUrl" :key="question.mediaUrl"
           :src="question.mediaUrl" autoplay loop muted playsinline disablepictureinpicture
           aria-label="Quiz question video clip" @error="mediaFailed = true" />
    <template v-if="question.type === 'audio' && question.mediaUrl && audioAllowed">
      <audio :key="question.mediaUrl" ref="player" controls preload="metadata" :src="question.mediaUrl"
             aria-label="Question audio" @error="mediaFailed = true" />
      <label v-if="adjustVolume">Rehearsal volume
        <input v-model.number="volume" type="range" min="0" max="1" step="0.05">
      </label>
      <button v-if="audience && (!enabled || blocked)" type="button" @click="enableAudio">
        {{ blocked ? 'Play question audio' : 'Enable automatic question audio' }}
      </button>
    </template>
    <p v-else-if="question.type === 'audio' && !audioAllowed" class="muted">Question audio is assigned to another display.</p>
    <p v-if="mediaFailed" class="notice" role="alert">The question media could not be played or displayed.</p>
  </div>
</template>
