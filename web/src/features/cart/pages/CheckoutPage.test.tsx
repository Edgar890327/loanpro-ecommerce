import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthContext } from '../../../shared/auth/auth-context'
import type { AuthContextValue, SessionUser } from '../../../shared/auth/auth-context'
import { ProtectedRoute } from '../../../shared/auth/ProtectedRoute'
import { CartProvider } from '../state/CartProvider'
import { CheckoutPage } from './CheckoutPage'
import '../../../shared/i18n/i18n'

const customer: SessionUser = { id: '1', fullName: 'Ana Buyer', email: 'ana@example.com', role: 'Customer', preferredLocale: null }
const admin: SessionUser = { id: '2', fullName: 'Root', email: 'root@example.com', role: 'Admin', preferredLocale: null }
const cartItem = { productId: 'p1', name: 'Tea', sku: 'TEA', price: 5, available: 4, quantity: 2 }

function LoginProbe() {
  const location = useLocation()
  return <p>{`login${location.search}`}</p>
}

function renderCheckout(user: SessionUser | null) {
  const value: AuthContextValue = {
    user,
    isLoading: false,
    login: async () => { throw new Error('unused') },
    register: async () => { throw new Error('unused') },
    logout: async () => undefined,
  }
  return render(
    <AuthContext.Provider value={value}>
      <CartProvider>
        <MemoryRouter initialEntries={['/checkout']}>
          <Routes>
            <Route path="/checkout" element={<ProtectedRoute role="Customer"><CheckoutPage /></ProtectedRoute>} />
            <Route path="/login" element={<LoginProbe />} />
            <Route path="/orders/:id" element={<p>order page</p>} />
          </Routes>
        </MemoryRouter>
      </CartProvider>
    </AuthContext.Provider>,
  )
}

describe('checkout', () => {
  beforeEach(() => {
    window.localStorage.clear()
    window.localStorage.setItem('store:cart', JSON.stringify([cartItem]))
  })

  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
  })

  it('redirects visitors to login with the checkout returnUrl and keeps the cart', () => {
    renderCheckout(null)

    expect(screen.getByText('login?returnUrl=%2Fcheckout')).toBeInTheDocument()
    expect(JSON.parse(window.localStorage.getItem('store:cart') ?? '[]')).toHaveLength(1)
  })

  it('shows the translated forbidden message to administrators', () => {
    renderCheckout(admin)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.queryByRole('form')).not.toBeInTheDocument()
  })

  it('revalidates the cart on entry, prefills the name and validates the address', async () => {
    const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(async () => new Response(JSON.stringify({ isAvailable: false, items: [], issues: [] }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)
    renderCheckout(customer)

    expect(await screen.findByText(/Inventory changed/)).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith('/api/cart/validate', expect.anything())
    expect(screen.getByDisplayValue('Ana Buyer')).toBeInTheDocument()
    expect(screen.getByText(/ana@example.com/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /Reserve and place order/ }))

    await waitFor(() => expect(screen.getAllByText('This address field is required.').length).toBeGreaterThan(0))
    expect(fetchMock.mock.calls.filter(([url]) => url === '/api/orders')).toHaveLength(0)
  })
})
