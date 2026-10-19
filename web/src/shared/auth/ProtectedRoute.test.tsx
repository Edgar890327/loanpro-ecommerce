import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { AuthContext } from './auth-context'
import type { AuthContextValue, SessionUser } from './auth-context'
import { ProtectedRoute } from './ProtectedRoute'
import '../i18n/i18n'

function LoginProbe() {
  const location = useLocation()
  return <p>{`login${location.search}`}</p>
}

function renderGuard(user: SessionUser | null, role: 'Admin' | 'Customer', isLoading = false) {
  const value: AuthContextValue = {
    user,
    isLoading,
    login: async () => { throw new Error('unused') },
    register: async () => { throw new Error('unused') },
    logout: async () => undefined,
  }
  return render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/checkout?step=1']}>
        <Routes>
          <Route path="/checkout" element={
            <ProtectedRoute role={role}><h1>Private</h1></ProtectedRoute>
          } />
          <Route path="/login" element={<LoginProbe />} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

const customer: SessionUser = { id: '1', fullName: 'Ana', email: 'user@example.com', role: 'Customer', preferredLocale: null }
const admin: SessionUser = { id: '2', fullName: 'Root', email: 'user@example.com', role: 'Admin', preferredLocale: null }

describe('role guard', () => {
  afterEach(cleanup)

  it('redirects visitors to login with the return url', () => {
    renderGuard(null, 'Customer')

    expect(screen.getByText('login?returnUrl=%2Fcheckout%3Fstep%3D1')).toBeInTheDocument()
  })

  it('renders the content for the required role', () => {
    renderGuard(customer, 'Customer')

    expect(screen.getByRole('heading', { name: 'Private' })).toBeInTheDocument()
  })

  it('shows a forbidden message when the role does not match', () => {
    renderGuard(admin, 'Customer')

    expect(screen.queryByRole('heading', { name: 'Private' })).not.toBeInTheDocument()
    expect(screen.getByRole('alert')).toBeInTheDocument()
  })

  it('waits while the session is loading', () => {
    renderGuard(null, 'Admin', true)

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByText(/login/)).not.toBeInTheDocument()
  })
})
