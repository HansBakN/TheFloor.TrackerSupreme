<script setup lang="ts">
import { onMounted, ref } from 'vue'

const status = ref('Connecting to local server…')

onMounted(async () => {
  try {
    const response = await fetch('/api/health')
    if (!response.ok) throw new Error()
    status.value = 'Local server ready'
  } catch {
    status.value = 'Local server unavailable'
  }
})
</script>

<template>
  <main class="mx-auto max-w-4xl px-8 py-24">
    <p class="text-sm font-bold tracking-widest text-cyan-300 uppercase">Quiz duels</p>
    <h1 class="mt-4 text-4xl font-bold">TheFloor.TrackerSupreme</h1>
    <p class="mt-6 text-slate-300" role="status">{{ status }}</p>
    <nav class="actions mt-6" aria-label="Duel">
      <RouterLink class="button primary" to="/host">Host dashboard</RouterLink>
      <RouterLink class="button" to="/tournaments">Mini Floor tournament</RouterLink>
      <RouterLink class="button" to="/display">Audience display</RouterLink>
    </nav>
  </main>
</template>
