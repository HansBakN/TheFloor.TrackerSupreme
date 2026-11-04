<script setup lang="ts">
import { formatClock } from './clocks'
import type { AudienceDuel } from './contracts'

defineProps<{ duel: AudienceDuel; clocks: { a: number; b: number } }>()
const players = ['a', 'b'] as const
</script>

<template>
  <div class="scoreboard">
    <section v-for="player in players" :key="player" class="clock" :class="{
      'clock-active': duel.status !== 'finished' && duel.active === player,
      'clock-low': duel.active === player && clocks[player] < 10000,
      'clock-urgent': duel.active === player && clocks[player] < 5000,
    }" :aria-label="'Contestant ' + player.toUpperCase()">
      <span class="clock-name">{{ player === 'a' ? duel.playerA : duel.playerB }}</span>
      <span class="clock-value" :data-clock="player">{{ formatClock(clocks[player]) }}</span>
      <span class="clock-caption">{{ duel.winner === player ? 'Winner' : duel.status !== 'finished' && duel.active === player ? 'Active player' : 'Time remaining' }}</span>
    </section>
  </div>
</template>
