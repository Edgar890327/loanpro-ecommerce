import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { capturePayment, createPayment } from './paymentApi'

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
  capability: string
  clientId?: string
  onCaptured: () => void
}

export function PayPalCheckout({ orderId, capability, clientId = '', onCaptured }: Props) {
  const { t } = useTranslation()
  const container = useRef<HTMLDivElement>(null)
  const externalReference = useRef<string | null>(null)
  const [error, setError] = useState(false)
  const [ready, setReady] = useState(false)

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
            const payment = await createPayment(orderId, capability)
            externalReference.current = payment.externalReference
            return payment.externalReference
          },
          onApprove: async (data) => {
            const reference = externalReference.current
            if (!reference || reference !== data.orderID) {
              setError(true)
              return
            }

            try {
              const result = await capturePayment(orderId, capability, reference)
              if (result.paymentStatus === 'Approved' || result.paymentStatus === 'Failed') {
                onCaptured()
              } else {
                setError(true)
              }
            } catch {
              setError(true)
            }
          },
          onError: () => setError(true),
        }).render(`#${buttonsContainer.id}`)
          .then(() => setReady(true))
          .catch(() => setError(true))
      } catch {
        setError(true)
      }
    }

    if (window.paypal && (!window.paypalSdkClientId || window.paypalSdkClientId === clientId)) {
      showButtons()
    } else if (!clientId.trim()) {
      setError(true)
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
      script.onerror = () => setError(true)
      document.head.append(script)
    }

    return () => {
      cancelled = true
      if (script?.parentElement) script.remove()
      buttonsContainer?.replaceChildren()
    }
  }, [capability, clientId, onCaptured, orderId])

  return (
    <div className="payment-checkout">
      {!ready && !error && <p role="status">{t('paymentLoading')}</p>}
      <div id={`paypal-buttons-${orderId}`} ref={container} />
      {error && <p role="alert">{t('paymentFailedRetry')}</p>}
    </div>
  )
}
