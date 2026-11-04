import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  server: {
    proxy: {
      '/api': 'http://localhost:5080',
      '/live': { target: 'http://localhost:5080', ws: true },
    },
  },
  test: { environment: 'jsdom', include: ['src/**/*.test.ts'] },
})
