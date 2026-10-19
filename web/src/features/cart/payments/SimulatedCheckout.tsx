import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { capturePayment, createPayment } from './paymentApi'
import { translateApiError } from '../../../shared/api/api'

type Props = {
  orderId: string
  clientId?: string
  onCaptured: () => void
}

export function SimulatedCheckout({ orderId, onCaptured }: Props) {
  const { t } = useTranslation()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const showPaymentError = (reason?: unknown) => {
    setError(reason
      ? translateApiError(reason, t, 'paymentFailedRetry')
      : t('paymentFailedRetry'))
  }

  async function pay() {
    setPending(true)
    setError('')
    try {
      const payment = await createPayment(orderId)
      const result = await capturePayment(orderId, payment.externalReference)
      if (result.paymentStatus === 'Approved' || result.paymentStatus === 'Failed') {
        onCaptured()
      } else {
        showPaymentError()
      }
    } catch (reason) {
      showPaymentError(reason)
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="payment-checkout">
      <button className="primary-button" type="button" onClick={() => void pay()} disabled={pending}>
        {pending ? t('paymentProcessing') : t('payWithSimulated')}
      </button>
      {error && <p role="alert">{error}</p>}
    </div>
  )
}
