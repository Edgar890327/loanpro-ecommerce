import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { capturePayment, createPayment } from './paymentApi'
import { translateApiError } from '../../../shared/api/api'

type PayPalButtons = {
  render: (selector: string) => Promise<void>
}

type PayPalSdk = {
  Buttons: (options: {
    createOrder: () => Promise<string>
    onApprove: (data: { orderID: string }) => Promise<void>
    onError: () => void
  }) => PayPalButtons
}

declare global {
  interface Window {
    paypal?: PayPalSdk
    paypalSdkClientId?: string
  }
}

type Props = {
  orderId: string
  clientId?: string
  onCaptured: () => void
}

export function PayPalCheckout({ orderId, clientId = '', onCaptured }: Props) {
  const { t } = useTranslation()
  const container = useRef<HTMLDivElement>(null)
  const externalReference = useRef<string | null>(null)
  const [error, setError] = useState('')
  const [ready, setReady] = useState(false)
  const showPaymentError = useRef<(reason?: unknown) => void>(() => undefined)
  showPaymentError.current = (reason) => {
    setError(reason
      ? translateApiError(reason, t, 'paymentFailedRetry')
      : t('paymentFailedRetry'))
  }

  useEffect(() => {
    let cancelled = false
    let script: HTMLScriptElement | null = null
    const buttonsContainer = container.current

    const showButtons = () => {
      if (cancelled || !buttonsContainer || !window.paypal) return
      buttonsContainer.replaceChildren()
      try {
        void window.paypal.Buttons({
          createOrder: async () => {
            try {
              const payment = await createPayment(orderId)
              externalReference.current = payment.externalReference
              return payment.externalReference
            } catch (reason) {
              showPaymentError.current(reason)
              throw reason
            }
          },
          onApprove: async (data) => {
            const reference = externalReference.current
            if (!reference || reference !== data.orderID) {
              showPaymentError.current()
              return
            }

            try {
              const result = await capturePayment(orderId, reference)
              if (result.paymentStatus === 'Approved' || result.paymentStatus === 'Failed') {
                onCaptured()
              } else {
                showPaymentError.current()
              }
            } catch (reason) {
              showPaymentError.current(reason)
            }
          },
          onError: () => showPaymentError.current(),
        }).render(`#${buttonsContainer.id}`)
          .then(() => setReady(true))
          .catch((reason) => showPaymentError.current(reason))
      } catch (reason) {
        showPaymentError.current(reason)
      }
    }

    if (window.paypal && (!window.paypalSdkClientId || window.paypalSdkClientId === clientId)) {
      showButtons()
    } else if (!clientId.trim()) {
      showPaymentError.current()
    } else {
      const previousScript = document.querySelector<HTMLScriptElement>(
        'script[data-payment-provider="paypal"]',
      )
      previousScript?.remove()
      if (window.paypalSdkClientId && window.paypalSdkClientId !== clientId) {
        delete window.paypal
      }
      script = document.createElement('script')
      script.src = `https://www.paypal.com/sdk/js?client-id=${encodeURIComponent(clientId)}&currency=USD&intent=capture`
      script.async = true
      script.dataset.paymentProvider = 'paypal'
      script.dataset.clientId = clientId
      script.onload = () => {
        window.paypalSdkClientId = clientId
        showButtons()
      }
      script.onerror = () => showPaymentError.current()
      document.head.append(script)
    }

    return () => {
      cancelled = true
      if (script?.parentElement) script.remove()
      buttonsContainer?.replaceChildren()
    }
  }, [clientId, onCaptured, orderId])

  return (
    <div className="payment-checkout">
      {!ready && !error && <p role="status">{t('paymentLoading')}</p>}
      <div id={`paypal-buttons-${orderId}`} ref={container} />
      {error && <p role="alert">{error}</p>}
    </div>
  )
}
