import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ErrorLogPage } from './ErrorLogPage'
import { apiRequest } from '../../../shared/api/api'
import i18n from '../../../shared/i18n/i18n'

vi.mock('../../../shared/api/api', () => ({ apiRequest: vi.fn() }))
vi.mock('../../../shared/auth/auth-context', () => ({
  useAuth: () => ({ token: 'admin-token' }),
}))
vi.mock('../../../shared/notifications/useNotifications', () => ({
  useNotifications: () => ({ notify: vi.fn() }),
}))

const log = {
  id: 'log-1',
  timestampUtc: '2026-05-15T12:30:00.000Z',
  level: 'Error',
  correlationId: 'correlation-123',
  message: 'A test failure',
  exceptionType: 'System.InvalidOperationException',
  stackTrace: 'at Store.Test()',
  httpMethod: 'GET',
  path: '/test',
  statusCode: 500,
  orderId: null,
  paymentProviderKey: null,
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ErrorLogPage />
    </QueryClientProvider>,
  )
}

describe('admin error log screen', () => {
  afterEach(() => {
    vi.clearAllMocks()
    void i18n.changeLanguage('en-US')
  })

  it('filters by date, level and correlation ID, then renders accessible details', async () => {
    vi.mocked(apiRequest).mockResolvedValue({
      items: [log],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    renderPage()

    expect(await screen.findByRole('table')).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('From date'), { target: { value: '2026-05-15' } })
    fireEvent.change(screen.getByLabelText('To date'), { target: { value: '2026-05-16' } })
    fireEvent.change(screen.getByLabelText('Level'), { target: { value: 'Error' } })
    fireEvent.change(screen.getByLabelText('Correlation ID'), {
      target: { value: 'correlation-123' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Apply filters' }))

    await waitFor(() => {
      const requestPath = vi.mocked(apiRequest).mock.calls.at(-1)?.[0]
      expect(requestPath).toContain('level=Error')
      expect(requestPath).toContain('correlationId=correlation-123')
      expect(requestPath).toContain('fromUtc=')
      expect(requestPath).toContain('toUtc=')
    })

    fireEvent.click(await screen.findByRole('button', { name: 'View details' }))
    expect(screen.getByRole('region', { name: 'Error details' })).toBeInTheDocument()
    expect(screen.getByText('at Store.Test()')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Close details' }))
    expect(screen.queryByRole('region', { name: 'Error details' })).not.toBeInTheDocument()
  })

  it('shows the translated empty state', async () => {
    vi.mocked(apiRequest).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderPage()

    expect(await screen.findByText('No error logs match these filters.')).toBeInTheDocument()
  })
})
