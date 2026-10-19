import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiRequest } from '../../../shared/api/api'
import i18n from '../../../shared/i18n/i18n'
import { MyOrderDetailPage } from './MyOrderDetailPage'

vi.mock('../../../shared/api/api', async () => {
  const actual = await vi.importActual<typeof import('../../../shared/api/api')>('../../../shared/api/api')
  return { ...actual, apiRequest: vi.fn() }
})

function renderDetail() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/account/orders/order-1']}>
        <Routes><Route path="/account/orders/:id" element={<MyOrderDetailPage />} /></Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('purchase detail page', () => {
  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
    void i18n.changeLanguage('en-US')
  })

  it('shows the frozen items, totals, address, provider and reference', async () => {
    vi.mocked(apiRequest).mockResolvedValue({
      id: 'order-1',
      orderNumber: 'ORD-2026-000123',
      state: 'Paid',
      subtotal: 50,
      shipping: 5,
      total: 55,
      createdAtUtc: '2026-05-15T12:30:00.000Z',
      reservedUntilUtc: '2026-05-15T12:45:00.000Z',
      contactEmail: 'buyer@example.com',
      paymentProviderKey: 'simulated',
      paymentReference: 'SIM-42',
      shippingAddress: { recipientName: 'Buyer', line1: '1 Main St', line2: null, city: 'Austin', region: 'TX', postalCode: '73301', country: 'US' },
      items: [{ productName: 'Widget', sku: 'W-1', quantity: 2, unitPrice: 25, subtotal: 50 }],
    })

    renderDetail()

    expect(await screen.findByRole('heading', { name: 'ORD-2026-000123' })).toBeInTheDocument()
    expect(screen.getByText('Widget')).toBeInTheDocument()
    expect(screen.getAllByText('$25.00').length).toBeGreaterThan(0)
    expect(screen.getByText('$55.00')).toBeInTheDocument()
    expect(screen.getByText('SIM-42')).toBeInTheDocument()
    expect(screen.getByText(/1 Main St/)).toBeInTheDocument()
    expect(apiRequest).toHaveBeenCalledWith('/me/orders/order-1')
  })

  it('shows a not found message with a way back when the order is not accessible', async () => {
    vi.mocked(apiRequest).mockRejectedValue(new Error('missing'))

    renderDetail()

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to my purchases' })).toHaveAttribute('href', '/account/orders')
  })
})
