import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  use: { baseURL: 'http://127.0.0.1:5088', trace: 'retain-on-failure' },
  webServer: {
    command: 'dotnet TheFloor.Server.dll --urls http://127.0.0.1:5088',
    cwd: '../../artifacts/app',
    url: 'http://127.0.0.1:5088/api/health',
  },
})
