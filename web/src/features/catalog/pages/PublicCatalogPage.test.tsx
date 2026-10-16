import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PublicCatalogPage } from './PublicCatalogPage'
import { PublicProductDetailPage } from './PublicProductDetailPage'
import i18n from '../../../shared/i18n/i18n'
import { CartProvider } from '../../cart/state/CartProvider'

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{location.pathname}{location.search}</output>
}

function renderCatalog(initialEntry: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <CartProvider>
        <MemoryRouter initialEntries={[initialEntry]}>
          <LocationProbe />
          <Routes>
            <Route path="/" element={<PublicCatalogPage />} />
            <Route path="/products/:id" element={<PublicProductDetailPage />} />
          </Routes>
        </MemoryRouter>
      </CartProvider>
    </QueryClientProvider>,
  )
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('public catalog pages', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    void i18n.changeLanguage('en-US')
  })

  it('debounces search into the shareable URL and resets pagination', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) =>
      jsonResponse(String(input).includes('/categories') ? [] : {
        items: [],
        page: 1,
        pageSize: 12,
        totalCount: 0,
      }),
    ))
    renderCatalog('/?category=Drinks&page=3')

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search products' }), {
      target: { value: 'café' },
    })

    await waitFor(() => {
      expect(screen.getByTestId('location')).toHaveTextContent('/?category=Drinks&q=caf%C3%A9')
    })
    expect(screen.getByTestId('location')).not.toHaveTextContent('page=3')
  })

  it('renders the empty state when no products match', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) =>
      jsonResponse(String(input).includes('/categories') ? [] : {
        items: [],
        page: 1,
        pageSize: 12,
        totalCount: 0,
      }),
    ))
    renderCatalog('/')

    expect(await screen.findByText('No products match your filters.')).toBeInTheDocument()
  })

  it('shows a catalog error when the request fails', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) =>
      String(input).includes('/categories')
        ? jsonResponse([])
        : jsonResponse({ title: 'Unavailable' }, 500),
    ))
    renderCatalog('/')

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The catalog could not be loaded. Please try again.',
    )
  })

  it('renders untrusted product markup as plain text', async () => {
    const product = {
      id: 'f1b182f1-e856-4431-939c-e3b3b5669faf',
      name: '<script>alert("safe")</script>',
      sku: 'PLAIN-1',
      description: '<b>literal description</b>',
      category: 'Markup',
      price: 7,
      stock: 2,
      weightKg: 1.25,
    }
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) =>
      jsonResponse(String(input).includes('/categories') ? [] : {
        items: [product],
        page: 1,
        pageSize: 12,
        totalCount: 1,
      }),
    ))
    const { container } = renderCatalog('/')

    expect(await screen.findByText(product.name)).toBeInTheDocument()
    expect(screen.getByText(product.description)).toBeInTheDocument()
    expect(container.querySelector('script, b')).toBeNull()
  })

  it('shows product weight and preserves catalog filters in the back link', async () => {
    const product = {
      id: '7c7f0ad6-30c6-47a2-85d1-760fc0f8d707',
      name: 'Coffee',
      sku: 'COFFEE-1',
      description: 'Ground coffee',
      category: 'Drinks',
      price: 8.5,
      stock: 8,
      weightKg: 0.75,
    }
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(product)))
    renderCatalog(`/products/${product.id}?q=cafe&category=Drinks&page=2`)

    expect(await screen.findByRole('heading', { name: 'Coffee' })).toBeInTheDocument()
    expect(screen.getByText('0.75')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to catalog' })).toHaveAttribute(
      'href',
      '/?q=cafe&category=Drinks&page=2',
    )
  })
})
