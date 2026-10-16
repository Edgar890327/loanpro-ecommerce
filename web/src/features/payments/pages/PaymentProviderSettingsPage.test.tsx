import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../../../shared/auth/auth'
import i18n from '../../../shared/i18n/i18n'
import { PaymentProviderSettingsPage } from './PaymentProviderSettingsPage'

const settings = [
  {
    key: 'simulated',
    name: 'Simulated',
    configurationFields: [{
      key: 'mode',
      label: 'Payment mode',
      type: 'select',
      required: true,
      secret: false,
      options: ['AlwaysApprove', 'AlwaysReject'],
    }],
    secretFields: [],
    publicConfiguration: { mode: 'AlwaysApprove' },
    isActive: true,
  },
  {
    key: 'paypal',
    name: 'PayPal',
    configurationFields: [
      { key: 'clientId', label: 'Client ID', type: 'text', required: true, secret: false, options: null },
      { key: 'sandbox', label: 'Sandbox', type: 'boolean', required: true, secret: false, options: null },
      { key: 'clientSecret', label: 'Secret', type: 'password', required: true, secret: true, options: null },
    ],
    secretFields: ['clientSecret'],
    publicConfiguration: { clientId: 'public-id', sandbox: 'true' },
    isActive: false,
  },
]

function renderSettingsPage() {
  sessionStorage.setItem('store-admin-token', 'admin-token')
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <AuthProvider>
      <QueryClientProvider client={queryClient}>
        <PaymentProviderSettingsPage />
      </QueryClientProvider>
    </AuthProvider>,
  )
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('payment provider settings page', () => {
  afterEach(() => {
    cleanup()
    sessionStorage.clear()
    vi.unstubAllGlobals()
    void i18n.changeLanguage('en-US')
  })

  it('switches the active provider without displaying or resending stored secrets', async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'PUT') {
        return jsonResponse({ ...settings[1], isActive: true })
      }
      return jsonResponse(settings)
    })
    vi.stubGlobal('fetch', fetchMock)
    renderSettingsPage()

    await screen.findByRole('heading', { name: 'Payment providers' })
    fireEvent.change(screen.getByLabelText('Payment provider'), { target: { value: 'paypal' } })

    expect(screen.getByLabelText('PayPal client secret (stored value is not shown)')).toHaveValue('')
    fireEvent.click(screen.getByRole('button', { name: 'Save and activate' }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    const [, request] = fetchMock.mock.calls[1]
    expect(String(fetchMock.mock.calls[1][0])).toBe('/api/admin/payment-providers/paypal')
    expect(JSON.parse(String(request?.body))).toMatchObject({
      publicConfiguration: { clientId: 'public-id', sandbox: 'true' },
      secrets: {},
      activate: true,
    })
    expect(await screen.findByRole('status')).toHaveTextContent('Payment provider settings saved.')
  })
})
