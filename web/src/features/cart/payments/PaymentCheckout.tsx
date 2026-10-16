import { useTranslation } from 'react-i18next'
import type { ComponentType } from 'react'
import { PayPalCheckout } from './PayPalCheckout'
import { SimulatedCheckout } from './SimulatedCheckout'
import type { PaymentProvider } from './paymentTypes'

type CheckoutProps = {
  orderId: string
  capability: string
  clientId?: string
  onCaptured: () => void
}

type Props = Omit<CheckoutProps, 'clientId'> & {
  provider: PaymentProvider
}

const checkoutComponents: Record<string, ComponentType<CheckoutProps>> = {
  simulated: SimulatedCheckout,
  paypal: PayPalCheckout,
}

export function PaymentCheckout({ orderId, capability, provider, onCaptured }: Props) {
  const { t } = useTranslation()
  const Checkout = checkoutComponents[provider.key]

  if (!Checkout) return <p role="alert">{t('paymentProviderUnsupported')}</p>

  return (
    <Checkout
      orderId={orderId}
      capability={capability}
      clientId={provider.publicConfiguration.clientId}
      onCaptured={onCaptured}
    />
  )
}
