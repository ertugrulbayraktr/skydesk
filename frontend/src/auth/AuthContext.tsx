import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import { api, clearSession, decodeJwt, getSession, saveSession, type AuthSession } from '../api/client'

interface AuthContextValue {
  session: AuthSession | null
  staffLogin: (email: string, password: string) => Promise<AuthSession>
  passengerLogin: (pnr: string, lastName: string) => Promise<AuthSession>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

// The backend writes claims with full URI types (no outbound short-name
// mapping); accept both forms to stay robust.
const XMLSOAP = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims'
const MSROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

function claim(payload: Record<string, unknown>, ...keys: string[]): string | undefined {
  for (const key of keys) {
    const value = payload[key]
    if (typeof value === 'string') return value
  }
  return undefined
}

function sessionFromToken(token: string, refreshToken?: string): AuthSession {
  const payload = decodeJwt(token)
  return {
    token,
    refreshToken,
    role: claim(payload, MSROLE, 'role') as AuthSession['role'],
    name:
      claim(payload, `${XMLSOAP}/name`, 'unique_name') ??
      claim(payload, `${XMLSOAP}/emailaddress`, 'email') ??
      'User',
    userId: claim(payload, `${XMLSOAP}/nameidentifier`, 'nameid') ?? '',
    pnr: claim(payload, 'pnr'),
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(getSession())

  const staffLogin = useCallback(async (email: string, password: string) => {
    const result = await api<{ token: string; refreshToken: string }>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    const next = sessionFromToken(result.token, result.refreshToken)
    saveSession(next)
    setSession(next)
    return next
  }, [])

  const passengerLogin = useCallback(async (pnr: string, lastName: string) => {
    const result = await api<{ token: string }>('/api/auth/passenger/verify-pnr', {
      method: 'POST',
      body: JSON.stringify({ pnr, lastName }),
    })
    const next = sessionFromToken(result.token)
    saveSession(next)
    setSession(next)
    return next
  }, [])

  const logout = useCallback(() => {
    const current = getSession()
    if (current?.refreshToken) {
      // Fire-and-forget revocation; local state is cleared regardless
      api('/api/auth/logout', {
        method: 'POST',
        body: JSON.stringify({ refreshToken: current.refreshToken }),
      }).catch(() => {})
    }
    clearSession()
    setSession(null)
  }, [])

  return (
    <AuthContext.Provider value={{ session, staffLogin, passengerLogin, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
