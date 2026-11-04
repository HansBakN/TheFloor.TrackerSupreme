<script setup lang="ts">
import { useLibrary } from './library'

const library = useLibrary()
</script>

<template>
  <aside v-if="library.error || library.data.errors.length" class="notice" role="alert">
    <p v-if="library.error">{{ library.error }}</p>
    <template v-if="library.data.errors.length">
      <strong>Some library files need attention.</strong>
      <ul class="list-disc pl-5">
        <li v-for="error in library.data.errors" :key="error">{{ error }}</li>
      </ul>
    </template>
  </aside>
</template>
