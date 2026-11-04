import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  use: { baseURL: 'http://127.0.0.1:5088', trace: 'retain-on-failure' },
  webServer: {
    env: { THEFLOOR_DATA_DIR: mkdtempSync(join(tmpdir(), 'thefloor-playwright-')) },
    command: 'dotnet TheFloor.Server.dll --urls http://127.0.0.1:5088',
    cwd: '../../artifacts/app',
    url: 'http://127.0.0.1:5088/api/health',
  },
})
