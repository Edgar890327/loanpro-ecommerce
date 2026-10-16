import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../../../shared/auth/auth'
import i18n from '../../../shared/i18n/i18n'
import { SettingsPage } from './SettingsPage'

const settings = {
  paymentProviders: [
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
      maskedSecrets: {},
      isEnabled: true,
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
      maskedSecrets: { clientSecret: '••••••••' },
      isEnabled: true,
      isActive: false,
    },
  ],
  shippingStrategies: [
    {
      key: 'flat-rate',
      name: 'Flat rate',
      configurationFields: [{
        key: 'amountUsd',
        label: 'Flat shipping amount (USD)',
        type: 'number',
        required: true,
        secret: false,
        options: null,
      }],
      secretFields: [],
      publicConfiguration: { amountUsd: '4.50' },
      maskedSecrets: {},
      isEnabled: true,
      isActive: true,
    },
  ],
}

function renderSettingsPage() {
  sessionStorage.setItem('store-admin-token', 'admin-token')
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <AuthProvider>
      <QueryClientProvider client={queryClient}>
        <SettingsPage />
      </QueryClientProvider>
    </AuthProvider>,
  )
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('store settings page', () => {
  afterEach(() => {
    cleanup()
    sessionStorage.clear()
    vi.unstubAllGlobals()
    void i18n.changeLanguage('en-US')
  })

  it('renders payment and shipping fields from their schemas and preserves stored secrets', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse(settings)))
    renderSettingsPage()

    await screen.findByRole('heading', { name: 'Settings' })
    expect(screen.getByLabelText('Flat shipping amount (USD)')).toHaveValue(4.5)
    fireEvent.change(screen.getByLabelText('PayPal client secret (stored value is not shown)'), {
      target: { value: '' },
    })
    expect(screen.getByLabelText('PayPal client secret (stored value is not shown)')).toHaveValue('')
  })

  it('saves settings and activates a provider without resending a stored secret', async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'PUT') {
        return jsonResponse({ ...settings.paymentProviders[1], isActive: true })
      }
      return jsonResponse(settings)
    })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('confirm', vi.fn(() => true))
    renderSettingsPage()
    await screen.findByRole('heading', { name: 'Settings' })

    const activate = screen.getAllByRole('button', { name: 'Save and activate' })[0]
    fireEvent.click(activate)

    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(true)
    })
    const [, request] = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(String(fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')?.[0]))
      .toBe('/api/admin/payment-providers/paypal')
    expect(JSON.parse(String(request?.body))).toMatchObject({
      publicConfiguration: { clientId: 'public-id', sandbox: 'true' },
      secrets: {},
      enabled: true,
      activate: true,
    })
    expect(await screen.findByRole('status')).toHaveTextContent('Settings saved.')
  })
})
