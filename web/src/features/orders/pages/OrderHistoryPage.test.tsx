import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { OrderHistoryPage } from './OrderHistoryPage'
import { apiRequest } from '../../../shared/api/api'
import i18n from '../../../shared/i18n/i18n'

vi.mock('../../../shared/api/api', () => ({ apiRequest: vi.fn() }))

describe('admin order history', () => {
  afterEach(() => {
    vi.clearAllMocks()
    void i18n.changeLanguage('en-US')
  })

  it('renders order status, payment provider and locale-formatted total', async () => {
    vi.mocked(apiRequest).mockResolvedValue({
      items: [{
        id: 'order-123',
        orderNumber: 'ORD-2026-000123',
        customerName: 'Ada Lovelace',
        customerEmail: 'ada@example.com',
        state: 'Paid',
        createdAtUtc: '2026-05-15T12:30:00.000Z',
        subtotal: 24.5,
        shipping: 3,
        total: 27.5,
        paymentProviderKey: 'simulated',
      }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    render(
      <QueryClientProvider client={client}>
        <OrderHistoryPage />
      </QueryClientProvider>,
    )

    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(screen.getByText('ORD-2026-000123')).toBeInTheDocument()
    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument()
    expect(screen.getByText('ada@example.com')).toBeInTheDocument()
    expect(screen.getByText('Paid')).toBeInTheDocument()
    expect(screen.getByText('Simulated')).toBeInTheDocument()
    expect(screen.getByText('$27.50')).toBeInTheDocument()
  })
})
