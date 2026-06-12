import { describe, expect, it, vi, beforeEach } from 'vitest'
import { api, ApiError, decodeJwt, getSession, saveSession } from '../api/client'

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('api client', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.unstubAllGlobals()
  })

  it('parses ProblemDetails title into the error message', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      jsonResponse(409, { title: 'Invalid state transition', status: 409 })))

    await expect(api('/api/tickets/x/transition', { method: 'POST' }))
      .rejects.toMatchObject({ status: 409, message: 'Invalid state transition' })
  })

  it('returns parsed JSON on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(200, { ok: true })))

    const result = await api<{ ok: boolean }>('/api/health-ish')
    expect(result.ok).toBe(true)
  })

  it('on 401 refreshes the token once and retries the request', async () => {
    saveSession({
      token: 'old-token', refreshToken: 'refresh-1',
      role: 'SupportAgent', name: 'A', userId: 'u1',
    })

    const fetchMock = vi.fn()
      // 1st call: original request -> 401
      .mockResolvedValueOnce(jsonResponse(401, { title: 'Unauthorized' }))
      // 2nd call: refresh -> new pair
      .mockResolvedValueOnce(jsonResponse(200, { token: 'new-token', refreshToken: 'refresh-2' }))
      // 3rd call: retried request -> 200
      .mockResolvedValueOnce(jsonResponse(200, { data: 42 }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await api<{ data: number }>('/api/agent/queue')

    expect(result.data).toBe(42)
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(getSession()?.token).toBe('new-token')
    expect(getSession()?.refreshToken).toBe('refresh-2')
    // The retried call must carry the NEW access token
    const retryHeaders = fetchMock.mock.calls[2][1].headers as Record<string, string>
    expect(retryHeaders.Authorization).toBe('Bearer new-token')
  })

  it('clears the session and throws when refresh fails', async () => {
    saveSession({
      token: 'old-token', refreshToken: 'refresh-1',
      role: 'SupportAgent', name: 'A', userId: 'u1',
    })

    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse(401, { title: 'Unauthorized' }))
      .mockResolvedValueOnce(jsonResponse(401, { title: 'Invalid or expired refresh token' }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(api('/api/agent/queue')).rejects.toBeInstanceOf(ApiError)
    expect(getSession()).toBeNull()
  })
})

describe('decodeJwt', () => {
  it('decodes a base64url payload', () => {
    const payload = { role: 'Admin', pnr: 'ABC123' }
    const token = `h.${btoa(JSON.stringify(payload))}.s`

    expect(decodeJwt(token)).toEqual(payload)
  })
})
