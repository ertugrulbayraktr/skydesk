import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { AuthProvider } from '../auth/AuthContext'
import LoginPage from '../pages/LoginPage'

function renderLogin() {
  return render(
    <AuthProvider>
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    </AuthProvider>,
  )
}

describe('LoginPage', () => {
  it('defaults to the passenger tab with PNR fields', () => {
    renderLogin()
    expect(screen.getByText('PNR Code')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('ABC123')).toBeInTheDocument()
  })

  it('switches to the staff tab showing email/password fields', async () => {
    renderLogin()
    await userEvent.click(screen.getByRole('button', { name: /Staff/ }))

    expect(screen.getByText('Email')).toBeInTheDocument()
    expect(screen.getByText('Password')).toBeInTheDocument()
    expect(screen.queryByText('PNR Code')).not.toBeInTheDocument()
  })
})
