import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../../../shared/auth/auth'
import { LoginPage } from './LoginPage'
import { RegisterPage } from './RegisterPage'
import i18n from '../../../shared/i18n/i18n'

function Destination() {
  const location = useLocation()
  return <p>{`destination:${location.pathname}`}</p>
}

function respond(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
  })
}

function renderPage(path: string) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="*" element={<Destination />} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  )
}

function mockFetch(handler: (path: string, init?: RequestInit) => Response) {
  const fetchMock = vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/me') return respond(401, { title: 'Unauthorized' })
    return handler(path, init)
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('login and register forms', () => {
  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
    void i18n.changeLanguage('en-US')
  })

  it('validates login fields before calling the server', async () => {
    const fetchMock = mockFetch(() => respond(200, {}))
    renderPage('/login')
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/me', expect.anything()))

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findAllByRole('alert')).toHaveLength(2)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('shows the translated message for invalid credentials', async () => {
    mockFetch(() => respond(401, { code: 'INVALID_CREDENTIALS', title: 'Unauthorized' }))
    renderPage('/login')

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ana@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'wrong-password' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The email or password is incorrect.')
  })

  it('returns a customer to the return url after login', async () => {
    mockFetch(() => respond(200, { id: '1', fullName: 'Ana', role: 'Customer', preferredLocale: null }))
    renderPage('/login?returnUrl=%2Fcheckout')

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ana@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'correct-password' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('destination:/checkout')).toBeInTheDocument()
  })

  it('sends an admin to the administration area', async () => {
    mockFetch(() => respond(200, { id: '2', fullName: 'Root', role: 'Admin', preferredLocale: null }))
    renderPage('/login?returnUrl=%2Fcheckout')

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'admin@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'correct-password' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('destination:/admin/products')).toBeInTheDocument()
  })

  it('uses the correct autocomplete attributes', () => {
    mockFetch(() => respond(200, {}))
    renderPage('/register')

    expect(screen.getByLabelText('Email')).toHaveAttribute('autocomplete', 'email')
    expect(screen.getByLabelText('Password')).toHaveAttribute('autocomplete', 'new-password')
  })

  it('validates the register password limits', async () => {
    const fetchMock = mockFetch(() => respond(201, {}))
    renderPage('/register')

    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'Ana' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ana@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'short' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign up' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('8 to 128')
    expect(fetchMock).not.toHaveBeenCalledWith('/auth/register', expect.anything())
  })

  it('shows the duplicate email error from the server', async () => {
    mockFetch(() => respond(409, { code: 'EMAIL_TAKEN', title: 'Conflict' }))
    renderPage('/register')

    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'Ana' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ana@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'long-enough-password' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign up' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('already exists')
  })

  it('signs the user in after registering', async () => {
    const fetchMock = mockFetch(() => respond(201, {
      id: '3', fullName: 'Ana', role: 'Customer', preferredLocale: 'en-US',
    }))
    renderPage('/register?returnUrl=%2Fcheckout')

    fireEvent.change(screen.getByLabelText('Full name'), { target: { value: 'Ana' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ana@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'long-enough-password' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign up' }))

    expect(await screen.findByText('destination:/checkout')).toBeInTheDocument()
    const registerCall = fetchMock.mock.calls.find(([path]) => path === '/auth/register')
    expect(new Headers(registerCall?.[1]?.headers).get('X-Requested-With')).toBe('store')
  })
})
