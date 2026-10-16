import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PaymentCheckout } from './PaymentCheckout'
import type { PaymentProvider } from './paymentTypes'
import i18n from '../../../shared/i18n/i18n'

const simulated: PaymentProvider = {
  key: 'simulated',
  name: 'Simulated',
  configurationFields: [],
  secretFields: [],
  publicConfiguration: {},
}

const paypal: PaymentProvider = {
  key: 'paypal',
  name: 'PayPal',
  configurationFields: [],
  secretFields: ['clientSecret'],
  publicConfiguration: { clientId: 'public-client-id' },
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('payment checkout registry', () => {
  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
    delete window.paypal
    void i18n.changeLanguage('en-US')
  })

  it('routes simulated checkout through create and capture endpoints', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({
        orderId: 'order-1',
        providerKey: 'simulated',
        externalReference: 'sim-order-1',
        provider: simulated,
      }))
      .mockResolvedValueOnce(jsonResponse({
        orderId: 'order-1',
        orderState: 'Paid',
        paymentStatus: 'Approved',
      }))
    vi.stubGlobal('fetch', fetchMock)
    const onCaptured = vi.fn()
    render(
      <PaymentCheckout
        orderId="order-1"
        capability="order-capability-1234567890"
        provider={simulated}
        onCaptured={onCaptured}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Pay now' }))

    await waitFor(() => expect(onCaptured).toHaveBeenCalledOnce())
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(String(fetchMock.mock.calls[0][0])).toBe('/api/orders/order-1/payment')
    expect(String(fetchMock.mock.calls[1][0])).toBe('/api/orders/order-1/payment/capture')
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get('Idempotency-Key'))
      .toBe('order-capability-1234567890')
  })

  it('uses the PayPal SDK createOrder and onApprove callbacks', async () => {
    let callbacks: {
      createOrder: () => Promise<string>
      onApprove: (data: { orderID: string }) => Promise<void>
    } | undefined
    window.paypal = {
      Buttons: (options) => {
        callbacks = options
        return { render: async () => undefined }
      },
    }
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({
        orderId: 'order-2',
        providerKey: 'paypal',
        externalReference: 'paypal-order-2',
        provider: paypal,
      }))
      .mockResolvedValueOnce(jsonResponse({
        orderId: 'order-2',
        orderState: 'Paid',
        paymentStatus: 'Approved',
      }))
    vi.stubGlobal('fetch', fetchMock)
    const onCaptured = vi.fn()
    render(
      <PaymentCheckout
        orderId="order-2"
        capability="paypal-capability-123456"
        provider={paypal}
        onCaptured={onCaptured}
      />,
    )

    await waitFor(() => expect(callbacks).toBeDefined())
    const externalReference = await callbacks!.createOrder()
    expect(externalReference).toBe('paypal-order-2')
    await callbacks!.onApprove(
      { orderID: externalReference },
    )

    expect(onCaptured).toHaveBeenCalledOnce()
    expect(fetchMock).toHaveBeenCalledTimes(2)
    const captureBody = JSON.parse(String(fetchMock.mock.calls[1][1]?.body)) as { externalReference: string }
    expect(captureBody.externalReference).toBe('paypal-order-2')
  })

  it('shows an explicit unsupported provider state', () => {
    render(
      <PaymentCheckout
        orderId="order-3"
        capability="order-capability-1234567890"
        provider={{ ...simulated, key: 'unknown' }}
        onCaptured={vi.fn()}
      />,
    )

    expect(screen.getByRole('alert')).toHaveTextContent(
      'The active payment provider is not supported by this storefront.',
    )
  })
})
