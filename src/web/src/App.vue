<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()
</script>

<template>
  <nav v-if="route.path !== '/display'" class="site-nav" aria-label="Main navigation">
    <RouterLink class="brand" to="/">TheFloor.TrackerSupreme</RouterLink>
    <RouterLink to="/host">Host</RouterLink>
    <RouterLink to="/sets">Category sets</RouterLink>
    <RouterLink to="/categories">Categories</RouterLink>
  </nav>
  <RouterView />
</template>
