import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { useEffect } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
import { apiRequest } from '../../../shared/api/api'
import { AuthProvider } from '../../../shared/auth/auth'
import { ProtectedRoute } from '../../../shared/auth/ProtectedRoute'
import { useAuth } from '../../../shared/auth/auth-context'
import { ProductFormPage } from '../../products/pages/ProductFormPage'
import { validateProductForm } from '../../../shared/validation/productValidation'
import '../../../shared/i18n/i18n'

function renderWithAuth(element: ReactNode, path = '/admin/products/new') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <AuthProvider>
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[path]}>{element}</MemoryRouter>
      </QueryClientProvider>
    </AuthProvider>,
  )
}

describe('admin form and access handling', () => {
  afterEach(() => {
    sessionStorage.clear()
    vi.unstubAllGlobals()
  })

  it('validates required fields before sending a product form', async () => {
    sessionStorage.setItem('store-admin-token', 'admin-token')
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    renderWithAuth(
      <Routes>
        <Route path="/admin/products/new" element={<ProductFormPage />} />
      </Routes>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Save product' }))

    expect(await screen.findAllByRole('alert')).toHaveLength(4)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('validates numeric fields and accepts a valid product', () => {
    const messages = {
      required: 'Required',
      positivePrice: 'Price must be positive',
      nonNegative: 'Must be non-negative',
      invalidNumber: 'Enter a number',
      tooLong: 'Too long',
    }
    expect(validateProductForm({
      name: 'Product',
      sku: 'SKU',
      category: 'Category',
      price: 0,
      stock: -1,
      weightKg: 0,
    }, messages)).toEqual({
      price: 'Price must be positive',
      stock: 'Must be non-negative',
    })
    expect(validateProductForm({
      name: 'Product',
      sku: 'SKU',
      category: 'Category',
      price: 0.29,
      stock: 0,
      weightKg: null,
    }, messages)).toEqual({})
  })

  it('redirects protected routes when no admin token exists', async () => {
    renderWithAuth(
      <Routes>
        <Route path="/admin/products" element={
          <ProtectedRoute><h1>Private products</h1></ProtectedRoute>
        } />
        <Route path="/login" element={<h1>Login route</h1>} />
      </Routes>,
      '/admin/products',
    )

    expect(await screen.findByRole('heading', { name: 'Login route' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Private products' })).not.toBeInTheDocument()
  })

  it('clears the session and redirects after an API 401 response', async () => {
    sessionStorage.setItem('store-admin-token', 'expired-token')
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized' }), {
        status: 401,
        headers: { 'Content-Type': 'application/problem+json' },
      }),
    ))
    function UnauthorizedRequest() {
      const { token } = useAuth()
      useEffect(() => {
        void apiRequest('/api/admin/products', token).catch(() => undefined)
      }, [token])
      return <h1>Private products</h1>
    }

    renderWithAuth(
      <Routes>
        <Route path="/admin/products" element={
          <ProtectedRoute><UnauthorizedRequest /></ProtectedRoute>
        } />
        <Route path="/login" element={<h1>Login route</h1>} />
      </Routes>,
      '/admin/products',
    )

    expect(await screen.findByRole('heading', { name: 'Login route' })).toBeInTheDocument()
    expect(sessionStorage.getItem('store-admin-token')).toBeNull()
  })
})
