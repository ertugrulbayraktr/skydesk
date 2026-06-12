import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AuthProvider, useAuth } from '../auth/AuthContext'

const XMLSOAP = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims'

function makeToken(payload: Record<string, unknown>) {
  return `header.${btoa(JSON.stringify(payload))}.sig`
}

function Probe() {
  const { session, passengerLogin } = useAuth()
  return (
    <div>
      <button onClick={() => passengerLogin('ABC123', 'Doe')}>login</button>
      <span data-testid="role">{session?.role ?? 'none'}</span>
      <span data-testid="userId">{session?.userId ?? 'none'}</span>
      <span data-testid="pnr">{session?.pnr ?? 'none'}</span>
    </div>
  )
}

describe('AuthContext claim parsing', () => {
  it('resolves role/userId/pnr from full-URI claim types (backend token format)', async () => {
    const token = makeToken({
      [`${XMLSOAP}/nameidentifier`]: 'user-guid-123',
      [`${XMLSOAP}/name`]: 'John Doe',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Passenger',
      pnr: 'ABC123',
    })

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ token }), { status: 200, headers: { 'Content-Type': 'application/json' } })))

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    await userEvent.click(screen.getByText('login'))

    await waitFor(() => {
      expect(screen.getByTestId('role')).toHaveTextContent('Passenger')
      expect(screen.getByTestId('userId')).toHaveTextContent('user-guid-123')
      expect(screen.getByTestId('pnr')).toHaveTextContent('ABC123')
    })
  })
})

