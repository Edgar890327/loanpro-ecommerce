import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
import { ProductFormPage } from './ProductFormPage'
import { validateProductForm } from '../../../shared/validation/productValidation'
import '../../../shared/i18n/i18n'

function renderForm(element: ReactNode, path = '/admin/products/new') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>{element}</MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('admin product form validation', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('validates required fields before sending a product form', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    renderForm(
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
})
