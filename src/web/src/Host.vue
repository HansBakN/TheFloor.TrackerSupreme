<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { message } from './api'
import { useClocks } from './clocks'
import type { Configuration, DuelAction, Player } from './contracts'
import AudienceSetup from './AudienceSetup.vue'
import DuelPresets from './DuelPresets.vue'
import LibraryStatus from './LibraryStatus.vue'
import { useLibraryUpdates } from './library'
import { useLiveDuel } from './live'
import QuestionMedia from './QuestionMedia.vue'
import Scoreboard from './Scoreboard.vue'
import { useShortcuts } from './shortcuts'

defineProps<{ embedded?: boolean }>()
const live = useLiveDuel('host')
const library = useLibraryUpdates()
const setup = reactive<Configuration>({
  playerA: '', playerB: '', categoryId: '', seconds: 45, passSeconds: 3, starter: 'random', order: 'shuffled',
})
const available = computed(() => (library.active?.categoryIds ?? []).flatMap(id => {
  const category = library.byId.get(id)?.definition
  return category?.questions.some(q => q.enabled) ? [category] : []
}))
const duel = computed(() => live.host?.duel)
const clocks = useClocks(() => live.sample)
const duelSet = computed(() => library.data.sets.find(set => set.definition.id === live.host?.categorySetId)?.definition.name)
const fullscreenError = ref('')
const readyToPrepare = computed(() => live.status === 'live' && !live.pending &&
  available.value.some(category => category.id === setup.categoryId))

function can(action: DuelAction) {
  const session = live.host
  if (!session || live.pending || live.status !== 'live') return false
  switch (action) {
    case 'start': return session.duel.status === 'ready'
    case 'correct':
    case 'pass': return session.duel.status === 'running' && !session.duel.passMilliseconds
    case 'pause': return session.duel.status === 'running'
    case 'resume': return session.duel.status === 'paused' && (!session.exhausted || !!session.duel.passMilliseconds)
    case 'undo': return session.canUndo
    case 'append': return session.duel.status === 'paused' && session.exhausted && !session.duel.passMilliseconds
    case 'end': return session.duel.status !== 'finished'
    case 'return': return !session.tournamentId
    case 'restart': return !session.tournamentId || session.duel.status !== 'finished'
  }
}

function command(action: DuelAction, winner?: Player) {
  if (!can(action)) return
  if (action === 'restart' && !window.confirm('Restart the duel with fresh clocks?')) return
  if (action === 'end' && !window.confirm(winner ? 'End the duel and declare the selected winner?' : 'End the duel without a winner?')) return
  if (action === 'return' && duel.value?.status !== 'finished' && !window.confirm('End the duel without a winner and return to setup?')) return
  void live.command(action, winner)
}

async function toggleFullscreen() {
  fullscreenError.value = ''
  try {
    if (document.fullscreenElement) await document.exitFullscreen()
    else await document.documentElement.requestFullscreen()
  } catch (cause) {
    fullscreenError.value = message(cause)
  }
}

useShortcuts(can, command)
</script>

<template>
  <component :is="embedded ? 'section' : 'main'" class="host-workspace" :class="embedded ? 'panel embedded-duel' : 'workspace'">
    <header v-if="!embedded" class="page-header">
      <div><p class="eyebrow">Live control</p><h1>Host dashboard</h1></div>
      <div class="actions">
        <span class="connection" :class="{ 'connection-live': live.status === 'live' }" role="status">
          {{ live.status === 'live' ? live.audienceCount + (live.audienceCount === 1 ? ' display connected' : ' displays connected') : 'Connecting…' }}
        </span>
        <a class="button" href="/display" target="_blank" rel="noopener">Open audience display</a>
        <RouterLink class="button" to="/rehearsal">Rehearsal</RouterLink>
        <button type="button" @click="toggleFullscreen">Toggle fullscreen</button>
      </div>
    </header>

    <p v-if="live.error || fullscreenError" class="notice" role="alert">{{ live.error || fullscreenError }}</p>
    <LibraryStatus v-if="!embedded" />
    <p v-if="!embedded && live.host?.tournamentId" class="notice">This duel belongs to Mini Floor. <RouterLink :to="'/tournaments/' + live.host.tournamentId">Resume tournament controls and results</RouterLink>.</p>

    <form v-if="!duel" class="panel setup-form" @submit.prevent="live.prepare({ ...setup })">
      <h2>Prepare a duel</h2>
      <p>Active set <strong>{{ library.active?.name ?? 'No active set' }}</strong></p>
      <p v-if="!available.length" class="muted">Select a set with at least one enabled question to prepare a duel.</p>
      <div class="form-grid">
        <label>Player A<input v-model.trim="setup.playerA" required maxlength="80" autocomplete="off"></label>
        <label>Player B<input v-model.trim="setup.playerB" required maxlength="80" autocomplete="off"></label>
        <label>Category<select v-model="setup.categoryId" aria-label="Category" required>
          <option value="">Choose a category</option>
          <option v-for="category in available" :key="category.id" :value="category.id">{{ category.name }}</option>
        </select></label>
        <label>Starting time per player (seconds)<input v-model.number="setup.seconds" type="number" required min="1" max="3600"></label>
        <label>Pass penalty (seconds)<input v-model.number="setup.passSeconds" type="number" required min="0" max="3600"></label>
        <label>Starting player<select v-model="setup.starter">
          <option value="random">Random</option><option value="a">Player A</option><option value="b">Player B</option>
        </select></label>
        <label>Question order<select v-model="setup.order">
          <option value="shuffled">Shuffled</option><option value="ordered">Category order</option>
        </select></label>
      </div>
      <DuelPresets :options="setup" @apply="Object.assign(setup, $event)" />
      <button class="primary" :disabled="!readyToPrepare">Prepare duel</button>
    </form>

    <section v-else class="host-live">
      <div class="host-round">
        <h2>{{ duel.categoryName }}</h2>
        <p class="eyebrow">{{ duel.status }}</p>
      </div>
      <p v-if="duel.status === 'ready' && live.host" class="muted">
        {{ duelSet ?? 'Prepared set' }} · {{ live.host.configuration.seconds }} seconds per player ·
        {{ live.host.configuration.passSeconds }}-second pass penalty ·
        {{ live.host.configuration.order === 'ordered' ? 'Category order' : 'Shuffled questions' }}
      </p>
      <Scoreboard :duel="duel" :clocks="clocks" />
      <div class="actions controls">
        <button v-if="duel.status === 'ready'" class="primary" :disabled="!can('start')" @click="command('start')">Start</button>
        <button class="primary" :disabled="!can('correct')" @click="command('correct')">Correct <kbd>Space</kbd></button>
        <button :disabled="!can('pass')" @click="command('pass')">Pass <kbd>P</kbd></button>
        <button v-if="duel.status !== 'paused'" :disabled="!can('pause')" @click="command('pause')">Pause <kbd>Esc</kbd></button>
        <button v-else :disabled="!can('resume')" @click="command('resume')">Resume <kbd>R</kbd></button>
        <button :disabled="!can('undo')" @click="command('undo')">Undo <kbd>U</kbd></button>
        <button v-if="!live.host?.tournamentId" :disabled="!can('return')" @click="command('return')">{{ duel.status === 'finished' ? 'Return to setup' : 'End duel and return to setup' }}</button>
      </div>

      <p v-if="duel.status === 'finished'" class="notice" role="status">
        {{ duel.winner ? (duel.winner === 'a' ? duel.playerA : duel.playerB) + ' wins!' : 'Duel ended.' }}
      </p>
      <div v-if="live.host?.exhausted && duel.status === 'paused' && !duel.passMilliseconds" class="notice">
        <p>All snapshot questions have been used. Add new questions to the category, then bring them into this duel.</p>
        <button :disabled="!can('append')" @click="command('append')">Include new questions</button>
      </div>
      <p v-if="duel.passMilliseconds" class="notice" role="status">Pass lockout — waiting for the next question.</p>
      <div v-if="duel.question" class="host-question">
        <section class="panel">
          <h3 class="eyebrow">Question preview</h3>
          <QuestionMedia :question="duel.question" />
        </section>
        <section class="panel">
          <h3 class="eyebrow">Host answer</h3>
          <p class="answer">{{ live.host?.answer }}</p>
          <p v-if="live.host?.alternatives.length">Also accept {{ live.host.alternatives.join(', ') }}</p>
          <p v-if="live.host?.notes" class="muted">{{ live.host.notes }}</p>
        </section>
      </div>
      <details class="panel">
        <summary>Duel options</summary>
        <div class="actions mt-3">
          <button :disabled="!can('restart')" @click="command('restart')">Restart duel</button>
          <button v-if="!live.host?.tournamentId" :disabled="!can('end')" @click="command('end')">End duel</button>
          <button :disabled="!can('end')" @click="command('end', 'a')">Declare {{ duel.playerA }} winner</button>
          <button :disabled="!can('end')" @click="command('end', 'b')">Declare {{ duel.playerB }} winner</button>
        </div>
      </details>
    </section>
    <div class="mt-3"><AudienceSetup :audience="live.audience" /></div>
  </component>
</template>
