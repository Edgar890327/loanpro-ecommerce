import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiRequest } from '../../../shared/api/api'
import { formatCurrency, formatDate } from '../../../shared/i18n/format'
import i18n from '../../../shared/i18n/i18n'
import { MyOrdersPage } from './MyOrdersPage'

vi.mock('../../../shared/api/api', async () => {
  const actual = await vi.importActual<typeof import('../../../shared/api/api')>('../../../shared/api/api')
  return { ...actual, apiRequest: vi.fn() }
})

const order = (overrides: Record<string, unknown> = {}) => ({
  id: 'order-1',
  orderNumber: 'ORD-2026-000123',
  state: 'Paid',
  total: 1234.5,
  createdAtUtc: '2026-05-15T12:30:00.000Z',
  reservedUntilUtc: '2026-05-15T12:45:00.000Z',
  paymentProviderKey: 'simulated',
  ...overrides,
})

const page = (items: unknown[], totalCount = items.length) => ({ items, page: 1, pageSize: 10, totalCount })

function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{location.search}</output>
}

function renderPage(initial = '/account/orders') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[initial]}>
        <MyOrdersPage />
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const lastPath = () => String(vi.mocked(apiRequest).mock.calls.at(-1)?.[0])

describe('my purchases page', () => {
  beforeEach(() => {
    void i18n.changeLanguage('en-US')
  })

  afterEach(() => {
    cleanup()
    vi.useRealTimers()
    vi.clearAllMocks()
  })

  it('shows a skeleton while loading and then the orders with locale formatting', async () => {
    vi.mocked(apiRequest).mockResolvedValue(page([order()]))

    renderPage()

    expect(screen.getByRole('status', { name: 'Loading...' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'ORD-2026-000123' })).toHaveAttribute('href', '/account/orders/order-1')
    expect(screen.getByText('$1,234.50')).toBeInTheDocument()
    expect(within(screen.getByRole('table')).getByText('Paid')).toBeInTheDocument()
  })

  it('links to the catalog when the customer has no purchases', async () => {
    vi.mocked(apiRequest).mockResolvedValue(page([]))

    renderPage()

    expect(await screen.findByText(/have not made any purchases/i)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Browse the catalog' })).toHaveAttribute('href', '/')
  })

  it('offers retry after an error', async () => {
    vi.mocked(apiRequest).mockRejectedValueOnce(new Error('boom')).mockResolvedValue(page([order()]))

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('link', { name: 'ORD-2026-000123' })).toBeInTheDocument()
  })

  it('debounces the search by 300 ms and syncs it with the URL', async () => {
    vi.mocked(apiRequest).mockResolvedValue(page([order()]))
    renderPage()
    await screen.findByRole('link', { name: 'ORD-2026-000123' })
    vi.useFakeTimers()
    const callsBefore = vi.mocked(apiRequest).mock.calls.length

    fireEvent.change(screen.getByLabelText('Search by order number, product or SKU'), { target: { value: 'widget' } })
    await act(() => vi.advanceTimersByTimeAsync(299))
    expect(vi.mocked(apiRequest).mock.calls.length).toBe(callsBefore)
    expect(screen.getByTestId('location').textContent).toBe('')

    await act(() => vi.advanceTimersByTimeAsync(1))

    expect(screen.getByTestId('location')).toHaveTextContent('?q=widget')
    expect(lastPath()).toContain('q=widget')
  })

  it('initializes the filters from the URL and requests ten rows', async () => {
    vi.mocked(apiRequest).mockResolvedValue(page([order()]))

    renderPage('/account/orders?q=abc&status=Paid&from=2026-01-01&to=2026-01-31&sort=total_desc&page=2')

    await screen.findByRole('link', { name: 'ORD-2026-000123' })
    expect(lastPath()).toBe('/me/orders?q=abc&status=Paid&from=2026-01-01&to=2026-01-31&sort=total_desc&page=2&pageSize=10')
    expect(screen.getByLabelText('Search by order number, product or SKU')).toHaveValue('abc')
    expect(screen.getByLabelText('Status')).toHaveValue('Paid')
  })

  it('shows a no-results state whose button clears all filters', async () => {
    vi.mocked(apiRequest).mockResolvedValue(page([]))
    renderPage('/account/orders?q=zzz&status=Paid')

    expect(await screen.findByText('No purchases match your filters.')).toBeInTheDocument()
    fireEvent.click(screen.getAllByRole('button', { name: 'Clear filters' })[0])

    expect(screen.getByTestId('location').textContent).toBe('')
    expect(screen.getByLabelText('Search by order number, product or SKU')).toHaveValue('')
    expect(screen.getByLabelText('Status')).toHaveValue('')
  })

  it('resets to the first page when a filter changes', async () => {
    vi.mocked(apiRequest).mockResolvedValue(page([order()], 30))
    renderPage('/account/orders?page=3')
    await screen.findByRole('link', { name: 'ORD-2026-000123' })

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Paid' } })

    expect(screen.getByTestId('location')).toHaveTextContent('?status=Paid')
  })

  it.each(['en-US', 'es'])('formats the order number, date and total for %s', async (language) => {
    await i18n.changeLanguage(language)
    vi.mocked(apiRequest).mockResolvedValue(page([order()]))

    renderPage()

    const table = within(await screen.findByRole('table'))
    expect(table.getByRole('link', { name: 'ORD-2026-000123' })).toBeInTheDocument()
    const text = screen.getByRole('table').textContent
    expect(text).toContain(formatDate('2026-05-15T12:30:00.000Z', language))
    expect(text).toContain(formatCurrency(1234.5, language))
  })

  it('counts down the continue-payment action of a valid reservation and expires it', async () => {
    vi.useFakeTimers({ now: Date.parse('2026-05-15T12:00:00Z'), toFake: ['Date', 'setInterval', 'clearInterval', 'setTimeout', 'clearTimeout'] })
    vi.mocked(apiRequest).mockResolvedValue(page([
      order({ id: 'reserved-1', state: 'Reserved', reservedUntilUtc: '2026-05-15T12:00:03Z', total: 10 }),
    ]))

    renderPage()
    await act(() => vi.advanceTimersByTimeAsync(0))

    expect(screen.getByRole('link', { name: 'Continue payment' })).toHaveAttribute('href', '/orders/reserved-1')
    expect(screen.getByRole('timer')).toHaveTextContent('Reservation expires in 0:03')

    await act(() => vi.advanceTimersByTimeAsync(2000))
    expect(screen.getByRole('timer')).toHaveTextContent('Reservation expires in 0:01')

    await act(() => vi.advanceTimersByTimeAsync(1000))
    expect(screen.queryByRole('link', { name: 'Continue payment' })).not.toBeInTheDocument()
    expect(within(screen.getByRole('table')).getByText('Reservation expired')).toBeInTheDocument()
  })
})
