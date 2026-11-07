<script setup lang="ts">
import type { TournamentState } from './tournament'
import { playerColors } from './tournament'
const props = defineProps<{ state: TournamentState; eligible: string[]; pending?: boolean }>()
const emit = defineEmits<{ challenge: [id: string] }>()
const player = (id: string) => props.state.players.find(p => p.id === id)!
const category = (id: string | null) => props.state.categories.find(c => c.id === id)?.name
</script>

<template>
  <section class="panel floor-panel" aria-label="Tournament floor">
    <div class="page-header"><h2>The floor</h2><span class="tag">{{ state.players.filter(p => p.eliminationOrder === null).length }} {{ state.phase === 'completed' ? 'champion' : 'players remain' }} · 6 territories</span></div>
    <div class="floor-grid" :style="{ gridTemplateColumns: `repeat(${state.width}, minmax(0, 1fr))` }">
      <button v-for="cell in state.floor" :key="`${cell.x}-${cell.y}`" type="button"
        class="territory" :class="{ eligible: eligible.includes(cell.ownerId), challenger: state.challengerId === cell.ownerId }"
        :style="{ '--player-color': playerColors[player(cell.ownerId).color] }"
        :data-x="cell.x" :data-y="cell.y" :data-owner="cell.ownerId" :data-eligible="eligible.includes(cell.ownerId)"
        :disabled="pending || !eligible.includes(cell.ownerId)" :aria-label="`Territory ${cell.x},${cell.y}: ${player(cell.ownerId).name}${eligible.includes(cell.ownerId) ? ', eligible opponent' : ''}`"
        @click="emit('challenge', cell.ownerId)">
        <span class="eyebrow">{{ state.championId === cell.ownerId ? 'Champion' : state.challengerId === cell.ownerId ? 'Challenger' : eligible.includes(cell.ownerId) ? 'Challenge' : `Territory ${cell.x + 1} · ${cell.y + 1}` }}</span>
        <strong>{{ player(cell.ownerId).name }}</strong>
        <span>{{ state.phase === 'completed' ? 'Tournament complete' : category(player(cell.ownerId).categoryId) ?? 'Category played / awaiting selection' }}</span>
      </button>
    </div>
    <p class="muted wrap-legend">↔ Left and right edges connect. ↕ Top and bottom edges connect. Orthogonal neighbors only; no diagonals.</p>
  </section>
</template>
