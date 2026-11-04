let signingIn: Promise<void> | undefined

export class ApiError extends Error {
  constructor(readonly status: number, description: string) {
    super(description)
  }
}

async function check(response: Response) {
  if (response.ok) return response
  const problem = await response.json().catch(() => null) as { error?: string } | null
  throw new ApiError(response.status, problem?.error ?? `Request failed (${response.status}).`)
}

async function localHost() {
  signingIn ??= fetch('/api/local-host', { method: 'POST' })
    .then(check)
    .then(() => undefined)
    .finally(() => { signingIn = undefined })
  return signingIn
}

export async function request<T>(path: string, init?: RequestInit, host = true): Promise<T> {
  let response = await fetch(path, { ...init, cache: 'no-store' })
  if (host && response.status === 401) {
    await localHost()
    response = await fetch(path, { ...init, cache: 'no-store' })
  }
  await check(response)
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

export function json(method: 'PUT' | 'POST', data?: unknown): RequestInit {
  return data === undefined ? { method } : {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  }
}

export function categoryMedia(id: string, path: string) {
  return `/api/library/categories/${id}/media/${path.split('/').map(encodeURIComponent).join('/')}`
}

export function message(error: unknown) {
  return error instanceof Error ? error.message : 'The request could not be completed.'
}
