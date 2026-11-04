import { spawn } from 'node:child_process'
import { once } from 'node:events'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const app = fileURLToPath(new URL('../../../artifacts/app', import.meta.url))

export async function newDataRoot() {
  const path = await mkdtemp(join(tmpdir(), 'thefloor-browser-'))
  return { path, remove: () => rm(path, { recursive: true, force: true }) }
}

export async function startServer(dataRoot: string, port = 0) {
  const server = spawn('dotnet', ['TheFloor.Server.dll', '--urls', 'http://127.0.0.1:' + port], {
    cwd: app, env: { ...process.env, THEFLOOR_DATA_DIR: dataRoot }, stdio: ['ignore', 'pipe', 'pipe'],
  })
  const exit = once(server, 'exit')
  // Retain the exit promise for cleanup, including spawn errors.
  void exit.catch(() => undefined)
  let output = ''
  let stopTimer: NodeJS.Timeout | undefined

  async function stop() {
    if (server.exitCode !== null || server.signalCode !== null) return
    server.kill()
    stopTimer = setTimeout(() => server.kill('SIGKILL'), 3000)
    try { await exit } finally { clearTimeout(stopTimer) }
  }

  try {
    const origin = await new Promise<string>((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Local server did not start.\n' + output)), 10_000)
      server.stdout.on('data', chunk => {
        output += String(chunk)
        const found = /Now listening on: (http:\/\/127\.0\.0\.1:\d+)/.exec(output)
        if (found) {
          clearTimeout(timer)
          resolve(found[1])
        }
      })
      server.stderr.on('data', chunk => { output += String(chunk) })
      void exit.then(() => reject(new Error('Local server exited.\n' + output)), reject)
        .finally(() => clearTimeout(timer))
    })
    return { origin, stop }
  } catch (error) {
    await stop().catch(() => undefined)
    throw error
  }
}
