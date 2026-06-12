const TOKEN_KEY = 'airline.auth'

export interface AuthSession {
  token: string
  refreshToken?: string
  role: 'Passenger' | 'SupportAgent' | 'Admin'
  name: string
  userId: string
  pnr?: string
}

export function getSession(): AuthSession | null {
  const raw = localStorage.getItem(TOKEN_KEY)
  return raw ? (JSON.parse(raw) as AuthSession) : null
}

export function saveSession(session: AuthSession) {
  localStorage.setItem(TOKEN_KEY, JSON.stringify(session))
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY)
}

/** Decodes the JWT payload (no verification - display/routing only). */
export function decodeJwt(token: string): Record<string, unknown> {
  const payload = token.split('.')[1]
  return JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')))
}

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// Single in-flight refresh shared across concurrent 401s
let refreshPromise: Promise<boolean> | null = null

async function tryRefreshSession(): Promise<boolean> {
  const session = getSession()
  if (!session?.refreshToken) return false

  refreshPromise ??= (async () => {
    try {
      const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      })
      if (!response.ok) return false
      const result = (await response.json()) as { token: string; refreshToken: string }
      saveSession({ ...session, token: result.token, refreshToken: result.refreshToken })
      return true
    } catch {
      return false
    } finally {
      refreshPromise = null
    }
  })()

  return refreshPromise
}

async function rawRequest(path: string, options: RequestInit): Promise<Response> {
  const session = getSession()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  }
  if (session) headers.Authorization = `Bearer ${session.token}`
  return fetch(path, { ...options, headers })
}

export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response = await rawRequest(path, options)

  // Expired access token: one silent refresh attempt, then retry the call
  if (response.status === 401 && !path.startsWith('/api/auth/')) {
    const refreshed = await tryRefreshSession()
    if (refreshed) {
      response = await rawRequest(path, options)
    } else {
      clearSession()
      window.location.href = '/login'
      throw new ApiError(401, 'Session expired')
    }
  }

  if (!response.ok) {
    // Backend returns RFC 7807 ProblemDetails
    let message = `Request failed (${response.status})`
    try {
      const problem = await response.json()
      message = problem.title ?? problem.error ?? message
    } catch {
      /* non-JSON body */
    }
    throw new ApiError(response.status, message)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}
