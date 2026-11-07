<script setup lang="ts">
import { computed } from 'vue'
import type { AudienceTournament } from './tournament'
import { playerColor } from './tournament'
import TournamentFloor from './TournamentFloor.vue'
const props = defineProps<{ tournament: AudienceTournament }>()
const name = (id: string | null | undefined) => props.tournament.players.find(p => p.id === id)?.name ?? '—'
const category = (id: string | null | undefined) => props.tournament.categories.find(c => c.id === id)?.name ?? '—'
const matchup = computed(() => props.tournament.matchup)
const choosing = computed(() => ['initialSelection', 'winnerCategory'].includes(props.tournament.phase))
const banning = computed(() => ['defenderBan', 'challengerBans', 'defenderPick'].includes(props.tournament.phase))
const title = computed(() => ({
  initialSelection: `${name(props.tournament.selectingPlayerId)}, choose your category`,
  opponentSelection: `${name(props.tournament.challengerId)}, choose your opponent`,
  duelReady: `Next: duel ${matchup.value?.duelNumber ?? 1}`,
  playing: 'Duel in progress',
  defenderBan: `${name(matchup.value?.defenderId)} bans one category`,
  challengerBans: `${name(matchup.value?.challengerId)} bans two categories`,
  defenderPick: `${name(matchup.value?.defenderId)} chooses the deciding category`,
  matchupResult: `${name(matchup.value?.winnerId)} wins the matchup!`,
  winnerCategory: `${name(props.tournament.selectingPlayerId)}, choose a fresh category`,
  winnerDecision: `${name(matchup.value?.winnerId)}, continue or return?`,
  completed: `${name(props.tournament.championId)} is the Mini Floor champion!`,
})[props.tournament.phase])
</script>

<template>
  <section class="tournament-audience" :data-phase="tournament.phase" aria-live="polite">
    <header class="audience-tournament-header">
      <p class="eyebrow">Mini Floor · {{ tournament.completedMatchups }} / {{ tournament.totalMatchups }} matchups complete</p>
      <h1 data-testid="audience-phase-title">{{ title }}</h1>
      <p v-if="matchup" class="audience-matchup-score" data-testid="audience-matchup-score">
        <span>Challenger {{ name(matchup.challengerId) }}</span>
        <strong>{{ matchup.challengerScore }} – {{ matchup.defenderScore }}</strong>
        <span>{{ name(matchup.defenderId) }} Defender</span>
      </p>
    </header>
    <div class="audience-tournament-grid">
      <TournamentFloor :state="tournament" :eligible="tournament.eligibleOpponents" audience />
      <section class="panel audience-next-action">
        <template v-if="choosing">
          <h2>{{ tournament.phase === 'initialSelection' ? `Category pick ${tournament.selectionIndex + 1} of ${tournament.players.length}` : 'Choose one fresh category' }}</h2>
          <p>Choose one of these five. The other four stay available.</p>
          <ol class="audience-options" aria-label="Category options"><li v-for="id in tournament.offer" :key="id">{{ category(id) }}</li></ol>
        </template>
        <template v-else-if="tournament.phase === 'opponentSelection'">
          <h2>Eligible opponents</h2>
          <p>{{ tournament.challengerSource === 'continued' ? 'Continuing after a victory.' : 'Randomly selected challenger.' }} Opposite board edges connect.</p>
          <ul class="audience-options" aria-label="Opponent options"><li v-for="id in tournament.eligibleOpponents" :key="id" :style="{ borderColor: playerColor(tournament.players.find(p => p.id === id)!.color) }">
            <strong>{{ name(id) }}</strong><span>{{ category(tournament.players.find(p => p.id === id)?.categoryId) }}</span>
          </li></ul>
        </template>
        <template v-else-if="banning && matchup">
          <h2>{{ tournament.phase === 'defenderBan' ? 'Defender bans 1' : tournament.phase === 'challengerBans' ? 'Challenger bans 2' : 'Defender chooses from 2' }}</h2>
          <p>Defender bans one → challenger bans two → defender picks. Challenger answers first.</p>
          <ul class="audience-options" aria-label="Deciding category options"><li v-for="id in matchup.deciderOffer" :key="id" :class="{ banned: matchup.bans.includes(id) }">
            <span>{{ category(id) }}</span><small>{{ matchup.bans.includes(id) ? 'Banned' : 'Available' }}</small>
          </li></ul>
        </template>
        <template v-else-if="tournament.phase === 'duelReady' && matchup">
          <h2>{{ category(matchup.categoryId) }}</h2>
          <p class="audience-first-player">{{ name(matchup.startingPlayerId) }} answers first</p>
          <p>Duel 1: defender’s category, defender first.<br>Duel 2: challenger’s category, challenger first.</p>
          <ul v-if="matchup.deciderCategoryId" class="audience-options" aria-label="Deciding category selection"><li v-for="id in matchup.deciderOffer" :key="id" :class="{ banned: matchup.bans.includes(id), chosen: id === matchup.deciderCategoryId }">{{ category(id) }}<small>{{ id === matchup.deciderCategoryId ? 'Selected' : matchup.bans.includes(id) ? 'Banned' : 'Not selected' }}</small></li></ul>
          <p class="muted">Waiting for the host to prepare the duel.</p>
        </template>
        <template v-else-if="tournament.phase === 'matchupResult' && matchup">
          <h2>{{ name(matchup.winnerId) }} captures {{ matchup.territoriesGained }} {{ matchup.territoriesGained === 1 ? 'territory' : 'territories' }}</h2>
          <p>{{ name(matchup.loserId) }} is eliminated. The floor shows the new ownership.</p>
          <p>Next: {{ name(matchup.winnerId) }} chooses a fresh category.</p>
        </template>
        <template v-else-if="tournament.phase === 'winnerDecision'">
          <h2>Your next move</h2>
          <ul class="audience-options"><li><strong>Continue challenging</strong><span>Choose a neighboring opponent using all your territories.</span></li><li><strong>Return to the floor</strong><span>Another active player becomes challenger at random.</span></li></ul>
        </template>
        <template v-else-if="tournament.phase === 'completed'">
          <h2>One champion. The entire floor.</h2>
          <p>All {{ tournament.floor.length }} territories · {{ tournament.totalMatchups }} completed matchups.</p>
          <p>{{ tournament.players.length }} players started. {{ name(tournament.championId) }} is the last player standing.</p>
        </template>
        <template v-if="matchup?.duels.length">
          <h3>Matchup results</h3><ol class="audience-duel-results"><li v-for="(duel, i) in matchup.duels" :key="duel.id">Duel {{ i + 1 }} · {{ category(duel.categoryId) }} · {{ name(duel.winnerId) }} won</li></ol>
        </template>
      </section>
    </div>
    <ul class="audience-player-strip" aria-label="Tournament standings"><li v-for="player in tournament.players" :key="player.id" :class="{ eliminated: player.eliminationOrder !== null }" :style="{ '--player-color': playerColor(player.color) }">
      <strong>{{ player.name }}</strong><span>{{ player.eliminationOrder !== null ? `Eliminated ${player.eliminationOrder}` : `${player.territories} ${player.territories === 1 ? 'territory' : 'territories'}` }}</span>
      <small>Matchups {{ player.matchupsWon }}–{{ player.matchupsLost }} · Duels {{ player.duelsWon }}–{{ player.duelsLost }}</small>
    </li></ul>
  </section>
</template>
