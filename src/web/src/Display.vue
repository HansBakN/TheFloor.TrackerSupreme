<script setup lang="ts">
import { computed } from 'vue'
import { useClocks } from './clocks'
import { useLiveDuel } from './live'
import { displayId, displayName } from './liveConnection'
import QuestionMedia from './QuestionMedia.vue'
import Scoreboard from './Scoreboard.vue'
import TournamentAudience from './TournamentAudience.vue'

const live = useLiveDuel('audience')
const duel = computed(() => live.sample?.duel)
const showTournament = computed(() => !!live.tournament && !(live.tournament.phase === 'playing' && live.tournament.activeDuelId === duel.value?.id && duel.value?.status !== 'finished'))
const clocks = useClocks(() => live.sample)
const winner = computed(() => duel.value?.winner === 'a' ? duel.value.playerA : duel.value?.playerB)
</script>

<template>
  <main class="arena">
    <div class="arena-meta">
      <p class="eyebrow">Quiz duel · {{ displayName }}</p>
      <p v-if="live.status !== 'live'" role="status" class="connection">Connecting to the game…</p>
    </div>
    <TournamentAudience v-if="showTournament && live.tournament" :tournament="live.tournament" />
    <template v-else-if="duel">
      <p v-if="live.tournament?.matchup" class="audience-live-context">Mini Floor · Duel {{ live.tournament.matchup.duelNumber }} · {{ live.tournament.players.find(p => p.id === live.tournament?.matchup?.challengerId)?.name }} {{ live.tournament.matchup.challengerScore }} – {{ live.tournament.matchup.defenderScore }} {{ live.tournament.players.find(p => p.id === live.tournament?.matchup?.defenderId)?.name }}</p>
      <h1 class="arena-category">{{ duel.categoryName }}</h1>
      <Scoreboard :duel="duel" :clocks="clocks" />
      <div v-if="duel.status === 'finished'" class="winner" role="status">
        <p class="eyebrow">Duel complete</p>
        <h2>{{ duel.winner ? winner + ' wins!' : 'Duel ended' }}</h2>
      </div>
      <div v-else class="arena-question">
        <p v-if="duel.status === 'paused'" class="pause-badge" role="status">Paused</p>
        <p v-else-if="duel.status === 'ready'" class="pause-badge" role="status">Ready to start</p>
        <QuestionMedia v-if="duel.question" :question="duel.question" audience :playing="duel.status === 'running'" :audio-allowed="duel.audioDisplayId === displayId" />
        <h2 v-else>{{ duel.passMilliseconds ? 'Pass lockout' : 'Waiting for the next question' }}</h2>
      </div>
    </template>
    <div v-else class="arena-idle">
      <p class="eyebrow">TheFloor.TrackerSupreme</p>
      <h1>Waiting for the host</h1>
      <p>The duel will appear here when it is ready.</p>
    </div>
  </main>
</template>
