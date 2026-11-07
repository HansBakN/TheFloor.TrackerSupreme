<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Host from './Host.vue'
import TournamentFloor from './TournamentFloor.vue'
import { playerColor, minimumCategories, useTournament } from './tournament'
import type { TournamentView } from './tournament'
const route = useRoute()
const router = useRouter()
const tournament = useTournament(() => typeof route.params.id === 'string' ? route.params.id : undefined)
const { state, view, pool, list, pending, error, loading } = tournament
const setup = reactive({ names: ['', '', '', '', '', ''], seconds: 45, passSeconds: 3, order: 'shuffled' as 'shuffled' | 'ordered', width: 3, height: 2 })
const playerCount = ref(6)
const requiredCategories = computed(() => minimumCategories(playerCount.value))
const boardCells = computed(() => setup.width * setup.height)
const previewCells = computed(() => Number.isInteger(boardCells.value) && boardCells.value > 0 && boardCells.value <= 4096 ? boardCells.value : 0)
const validBoard = computed(() => Number.isInteger(playerCount.value) && playerCount.value >= 2 && playerCount.value <= 256 &&
  Number.isInteger(setup.width) && setup.width >= 1 && setup.width <= 64 && Number.isInteger(setup.height) && setup.height >= 1 && setup.height <= 64 && boardCells.value >= playerCount.value)
watch(playerCount, count => {
  if (!Number.isInteger(count) || count < 2 || count > 256) return
  setup.names = Array.from({ length: count }, (_, i) => setup.names[i] ?? '')
})
const bans = ref<string[]>([])
const current = computed(() => state.value?.matchups.at(-1))
const unfinished = computed(() => list.value.find(t => t.phase !== 'completed'))
const name = (id: string | null | undefined) => state.value?.players.find(p => p.id === id)?.name ?? '—'
const category = (id: string | null | undefined) => state.value?.categories.find(c => c.id === id)?.name ?? '—'
const score = (id: string | undefined) => current.value?.duels.filter(d => d.winnerId === id).length ?? 0
const nextCategory = computed(() => current.value && (current.value.duels.length === 0 ? current.value.defenderCategoryId : current.value.duels.length === 1 ? current.value.challengerCategoryId : current.value.deciderCategoryId))
const nextStarter = computed(() => current.value?.duels.length === 0 ? current.value.defenderId : current.value?.challengerId)
const banStage = computed(() => state.value && ['defenderBan', 'challengerBans', 'defenderPick'].includes(state.value.phase))
const phaseTitle = computed(() => ({
  initialSelection: `Initial category · ${name(state.value?.selectingPlayerId)}`,
  opponentSelection: `${name(state.value?.challengerId)} chooses an opponent`,
  duelReady: `Duel ${(current.value?.duels.length ?? 0) + 1} · ${category(nextCategory.value)}`,
  playing: `Duel ${(current.value?.duels.length ?? 0) + 1} in progress`,
  defenderBan: `Defender ${name(current.value?.defenderId)} bans one category`,
  challengerBans: `Challenger ${name(current.value?.challengerId)} bans two categories`,
  defenderPick: `Defender ${name(current.value?.defenderId)} chooses from the remaining two`,
  matchupResult: `${name(current.value?.winnerId)} wins the matchup`,
  winnerCategory: `${name(state.value?.selectingPlayerId)} chooses a fresh category`,
  winnerDecision: `${name(current.value?.winnerId)} decides what happens next`,
  completed: `${name(state.value?.championId)} is the Mini Floor champion!`,
})[state.value?.phase ?? 'initialSelection'])
watch(() => state.value?.phase, () => { bans.value = [] })

async function create() {
  const result = await tournament.send<TournamentView>('', { ...setup, commandId: crypto.randomUUID() })
  if (result) await router.push('/tournaments/' + result.state.id)
}
function toggleBan(id: string) {
  bans.value = bans.value.includes(id) ? bans.value.filter(b => b !== id) : [...bans.value, id].slice(-2)
}
async function destructive(action: 'undoResult' | 'restart' | 'delete') {
  const prompt = {
    undoResult: 'Undo the latest recorded duel result and ALL later choices? Scores, floor, eliminations and categories will be restored. The corrected duel will be prepared again with fresh clocks; replay it or declare the correct winner in Duel options.',
    restart: 'Restart this tournament? All results, territory assignments, category picks and progress will be reset. The same participants, settings and content pool will be used.',
    delete: 'Delete this tournament and all its tournament history? Its current duel will be retired. This cannot be undone.',
  }[action]
  if (!window.confirm(prompt)) return
  const result = await tournament.command(action, { confirm: true })
  if (action === 'delete' && result === null) await router.push('/tournaments')
}
</script>

<template>
  <main class="workspace tournament-workspace" :data-phase="state?.phase">
    <header class="page-header">
      <div><p class="eyebrow">{{ state?.players.length ?? playerCount }} players · one champion</p><h1>Mini Floor</h1></div>
      <div class="actions">
        <RouterLink class="button" to="/tournaments">Tournaments</RouterLink>
        <a class="button" href="/display" target="_blank" rel="noopener">Open audience display</a>
        <button :disabled="pending" @click="tournament.refresh">Refresh state</button>
      </div>
    </header>
    <p v-if="error" class="notice" role="alert">{{ error }}</p>
    <p v-if="loading" role="status">Restoring tournament state…</p>

    <template v-if="!route.params.id && !loading">
      <section v-if="list.length" class="panel mb-4">
        <h2>Resume or review</h2>
        <ul class="entry-list"><li v-for="item in list" :key="item.id">
          <RouterLink :to="'/tournaments/' + item.id">{{ item.champion ? 'Review champion ' + item.champion : 'Resume Mini Floor' }}</RouterLink>
          <p class="muted">{{ item.players.join(' · ') }} · {{ new Date(item.created).toLocaleString() }}</p>
        </li></ul>
      </section>
      <form class="panel setup-form" @submit.prevent="create">
        <h2>Create a Mini Floor tournament</h2>
        <p>Choose your player count and board dimensions. Players receive random starting cells, then spare cells are randomly assigned to adjacent owners until the floor is full.</p>
        <div class="form-grid">
          <label>Number of players<input v-model.number="playerCount" type="number" required min="2" max="256"></label>
          <label>Board columns<input v-model.number="setup.width" type="number" required min="1" max="64"></label>
          <label>Board rows<input v-model.number="setup.height" type="number" required min="1" max="64"></label>
        </div>
        <p>{{ boardCells }} cells · {{ playerCount }} players · {{ Math.max(0, boardCells - playerCount) }} spare cells · {{ Math.max(0, playerCount - 1) }} matchups</p>
        <div class="board-preview" aria-label="Board layout preview" :style="{ gridTemplateColumns: `repeat(${Math.max(1, setup.width)}, minmax(0, 1fr))` }"><span v-for="cell in previewCells" :key="cell" class="preview-cell">{{ cell }}</span></div>
        <p class="muted">Preview shows cells only; positions are randomized on creation.</p>
        <p class="muted">Spare cells go to a random adjacent owner, including across wrapped edges. Newly assigned cells help fill later gaps. Assignments are saved once.</p>
        <p v-if="!validBoard" class="notice">Choose 2–256 players and 1–64 rows/columns, with at least one cell per player.</p>
        <p>Active set: <strong>{{ pool?.setName ?? 'Choose a category set' }}</strong> · {{ pool?.categories.length ?? 0 }} eligible categories</p>
        <p v-if="(pool?.categories.length ?? 0) < requiredCategories" class="notice">At least {{ requiredCategories }} distinct playable categories in the active set are required to guarantee every five-category draw. Add categories with enabled questions before starting.</p>
        <p v-for="problem in pool?.errors" :key="problem" class="notice">{{ problem }}</p>
        <RouterLink to="/sets">Manage the active set</RouterLink>
        <details><summary>Review eligible category pool ({{ pool?.categories.length ?? 0 }})</summary><ul class="pool-list"><li v-for="item in pool?.categories" :key="item.id">{{ item.name }}</li></ul></details>
        <div class="form-grid"><label v-for="(_, i) in setup.names" :key="i">Participant {{ i + 1 }}<input v-model.trim="setup.names[i]" required maxlength="80" autocomplete="off"></label></div>
        <div class="form-grid">
          <label>Seconds per player<input v-model.number="setup.seconds" type="number" required min="1" max="3600"></label>
          <label>Pass seconds<input v-model.number="setup.passSeconds" type="number" required min="0" max="3600"></label>
          <label>Question order<select v-model="setup.order"><option value="shuffled">Shuffled</option><option value="ordered">Category order</option></select></label>
        </div>
        <p class="muted">Duel 1: defender’s category, defender first. Duel 2: challenger’s category, challenger first. At 1–1: defender bans one, challenger bans two, defender picks, challenger starts.</p>
        <p v-if="unfinished" class="notice">Resume or delete the unfinished tournament before creating another.</p>
        <button class="primary" :disabled="pending || !!unfinished || !pool || pool.categories.length < requiredCategories || !validBoard || !!pool.errors.length">Create tournament</button>
      </form>
    </template>

    <template v-if="state && view">
      <section class="panel tournament-context" aria-live="polite">
        <p class="eyebrow">{{ state.setName }} · {{ state.matchups.filter(m => m.completed).length }} / {{ state.players.length - 1 }} matchups complete · {{ state.seconds }}s / {{ state.passSeconds }}s pass</p>
        <h2 :class="{ 'champion-title': state.phase === 'completed' }" data-testid="phase-title">{{ phaseTitle }}</h2>
        <p v-if="state.challengerId && state.phase === 'opponentSelection'">{{ state.challengerSource === 'random' ? 'Randomly selected challenger' : 'Continuing after a victory' }}. Choose one highlighted opponent below.</p>
        <p v-if="state.phase === 'initialSelection'">Pick {{ state.selectionIndex + 1 }} of {{ state.players.length }}. Choose one; the other four remain available. Selection order: {{ state.selectionOrder.map(name).join(' → ') }}.</p>
        <template v-if="current">
          <p data-testid="matchup-score"><strong>Challenger {{ name(current.challengerId) }} {{ score(current.challengerId) }} – {{ score(current.defenderId) }} {{ name(current.defenderId) }} Defender</strong></p>
          <p class="muted">Duel 1: {{ category(current.defenderCategoryId) }} · {{ name(current.defenderId) }} first. Duel 2: {{ category(current.challengerCategoryId) }} · {{ name(current.challengerId) }} first. Decider: {{ name(current.challengerId) }} first.</p>
          <ol v-if="current.duels.length" class="result-list"><li v-for="(duel, i) in current.duels" :key="duel.id">Duel {{ i + 1 }} · {{ category(duel.categoryId) }} · {{ name(duel.startingPlayerId) }} started · {{ name(duel.winnerId) }} won</li></ol>
        </template>
      </section>

      <TournamentFloor :state="state" :eligible="view.eligibleOpponents" :pending="pending" @challenge="tournament.command('challenge', { playerId: $event })" />
      <section v-if="state.phase === 'opponentSelection'" class="panel">
        <h2>Eligible opponents</h2>
        <div class="actions"><button v-for="id in view.eligibleOpponents" :key="id" :disabled="pending" @click="tournament.command('challenge', { playerId: id })">Challenge {{ name(id) }}</button></div>
      </section>
      <section v-if="state.phase === 'initialSelection' || state.phase === 'winnerCategory'" class="panel">
        <h2>{{ name(state.selectingPlayerId) }}: select one category</h2>
        <div class="category-offer"><button v-for="id in state.offer" :key="id" :disabled="pending" @click="tournament.command('pick', { categoryIds: [id] })">Select {{ category(id) }}</button></div>
      </section>
      <section v-if="banStage && current" class="panel">
        <h2>Deciding category · {{ state.phase === 'defenderBan' ? 'Step 1 of 3' : state.phase === 'challengerBans' ? 'Step 2 of 3' : 'Step 3 of 3' }}</h2>
        <p>{{ phaseTitle }}</p>
        <div class="category-offer">
          <button v-for="id in current.deciderOffer" :key="id" :class="{ banned: current.bans.includes(id), 'ban-selected': bans.includes(id) }"
            :disabled="pending || current.bans.includes(id)" :aria-pressed="state.phase === 'challengerBans' ? bans.includes(id) : undefined"
            @click="state.phase === 'challengerBans' ? toggleBan(id) : tournament.command(state.phase === 'defenderBan' ? 'ban' : 'deciderPick', { categoryIds: [id] })">
            {{ current.bans.includes(id) ? 'Banned · ' : state.phase === 'defenderPick' ? 'Choose · ' : 'Ban · ' }}{{ category(id) }}
          </button>
        </div>
        <button v-if="state.phase === 'challengerBans'" class="primary mt-3" :disabled="pending || bans.length !== 2" @click="tournament.command('ban', { categoryIds: bans })">Confirm two bans ({{ bans.length }}/2)</button>
        <p class="muted">Banned and unchosen categories remain available for later draws.</p>
      </section>
      <section v-if="state.phase === 'duelReady'" class="panel">
        <h2>Prepare duel {{ (current?.duels.length ?? 0) + 1 }} · {{ category(nextCategory) }}</h2>
        <p><strong>{{ name(nextStarter) }} answers first.</strong> {{ current?.deciderCategoryId ? 'Selected deciding category is highlighted below.' : 'The category owner starts; roles stay fixed.' }}</p>
        <p v-if="current?.deciderCategoryId" class="chosen-category">Selected · {{ category(current.deciderCategoryId) }}</p>
        <button class="primary mt-3" :disabled="pending" @click="tournament.command('launch')">Prepare tournament duel</button>
      </section>
      <Host v-if="state.phase === 'playing'" embedded />
      <section v-if="state.phase === 'matchupResult' && current" class="panel">
        <h2>{{ name(current.winnerId) }} wins {{ score(current.winnerId!) }}–{{ score(current.loserId!) }}</h2>
        <p>{{ name(current.loserId) }} is eliminated. {{ current.transferred.length }} {{ current.transferred.length === 1 ? 'territory transferred' : 'territories transferred' }}. The floor above is updated.</p>
        <button class="primary mt-3" :disabled="pending" @click="tournament.command('next')">Choose winner’s fresh category</button>
      </section>
      <section v-if="state.phase === 'winnerDecision'" class="panel">
        <h2>Continue or return to the floor?</h2>
        <p>{{ name(current?.winnerId) }} has a fresh category. Return selects another remaining player at random.</p>
        <div class="actions mt-3"><button class="primary" :disabled="pending" @click="tournament.command('continue')">Continue challenging</button><button :disabled="pending" @click="tournament.command('return')">Return to floor</button></div>
      </section>
      <section v-if="state.phase === 'completed'" class="panel champion-panel" role="status">
        <h2>{{ name(state.championId) }} wins Mini Floor!</h2><p>All {{ state.floor.length }} territories · {{ state.players.length - 1 }} completed matchups · {{ state.matchups.flatMap(m => m.duels).length }} duels. Review the final floor, standings and results below.</p>
      </section>

      <section class="panel standings"><h2>Standings</h2><div class="table-scroll"><table>
        <thead><tr><th>Player</th><th>Status</th><th>Category</th><th>Territories</th><th>Matchups W–L</th><th>Duels W–L</th><th>Eliminated</th></tr></thead>
        <tbody><tr v-for="player in view.standings" :key="player.id" :style="{ '--player-color': playerColor(player.color) }"><th class="player-name">{{ player.name }}</th><td>{{ player.id === state.championId ? 'Champion' : player.active ? 'Active' : 'Eliminated' }}</td><td>{{ category(player.categoryId) }}</td><td>{{ player.territories }}</td><td>{{ player.matchupsWon }}–{{ player.matchupsLost }} ({{ player.matchupsPlayed }} played)</td><td>{{ player.duelsWon }}–{{ player.duelsLost }}</td><td>{{ player.eliminationOrder ?? '—' }}</td></tr></tbody>
      </table></div></section>
      <details class="panel" :open="state.phase === 'completed'"><summary>Matchup history ({{ state.matchups.length }})</summary>
        <article v-for="(matchup, i) in state.matchups" :key="matchup.id" class="history-entry"><h3>Matchup {{ i + 1 }} · {{ name(matchup.challengerId) }} vs {{ name(matchup.defenderId) }}</h3>
          <p>{{ matchup.completed ? name(matchup.winnerId) + ' won · ' + matchup.transferred.length + (matchup.transferred.length === 1 ? ' territory captured' : ' territories captured') : 'In progress' }} · {{ matchup.duels.filter(d => d.winnerId === matchup.challengerId).length }}–{{ matchup.duels.filter(d => d.winnerId === matchup.defenderId).length }}</p>
          <ol><li v-for="(duel, j) in matchup.duels" :key="duel.id">Duel {{ j + 1 }}: {{ category(duel.categoryId) }} · {{ name(duel.startingPlayerId) }} first · {{ name(duel.winnerId) }} won</li></ol>
        </article>
      </details>
      <details class="panel"><summary>Tournament controls and recovery</summary><p class="muted">Every confirmed action is saved. Correction restores the state before the latest recorded result, rolls back later choices and prepares that duel again with fresh clocks.</p>
        <div class="actions mt-3"><button :disabled="pending || !view.canUndo" @click="destructive('undoResult')">Undo latest duel result</button><button :disabled="pending" @click="destructive('restart')">Restart tournament</button><button :disabled="pending" @click="destructive('delete')">Delete tournament</button></div>
        <button class="mt-3" :disabled="pending" @click="tournament.command('present')">Show tournament on audience display</button>
        <p class="data-path">Tournament {{ state.id }} · revision {{ state.version }} · {{ state.phase }}</p>
      </details>
    </template>
  </main>
</template>
