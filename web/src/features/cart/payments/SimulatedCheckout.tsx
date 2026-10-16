import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { capturePayment, createPayment } from './paymentApi'

type Props = {
  orderId: string
  capability: string
  clientId?: string
  onCaptured: () => void
}

export function SimulatedCheckout({ orderId, capability, onCaptured }: Props) {
  const { t } = useTranslation()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState(false)

  async function pay() {
    setPending(true)
    setError(false)
    try {
      const payment = await createPayment(orderId, capability)
      const result = await capturePayment(orderId, capability, payment.externalReference)
      if (result.paymentStatus === 'Approved' || result.paymentStatus === 'Failed') {
        onCaptured()
      } else {
        setError(true)
      }
    } catch {
      setError(true)
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="payment-checkout">
      <button className="primary-button" type="button" onClick={() => void pay()} disabled={pending}>
        {pending ? t('paymentProcessing') : t('payWithSimulated')}
      </button>
      {error && <p role="alert">{t('paymentFailedRetry')}</p>}
    </div>
  )
}
