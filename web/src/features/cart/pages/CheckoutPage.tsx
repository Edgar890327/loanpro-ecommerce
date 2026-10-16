import { useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useCart } from '../state/useCart'
import { apiRequest } from '../../../shared/api/api'
import type { CartValidation } from '../types/CartValidation'
import type { Order } from '../types/Order'

export function CheckoutPage() {
  const { t } = useTranslation()
  const { items, clear } = useCart()
  const navigate = useNavigate()
  const idempotencyKey = useRef<string | null>(null)
  const [customerName, setCustomerName] = useState('')
  const [customerEmail, setCustomerEmail] = useState('')
  const [shippingAddress, setShippingAddress] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    setError('')
    try {
      const retrying = idempotencyKey.current !== null
      idempotencyKey.current ??= crypto.randomUUID()
      if (!retrying) {
        const cart = await apiRequest<CartValidation>('/api/cart/validate', null, {
          method: 'POST',
          body: JSON.stringify({ items: items.map(({ productId, quantity }) => ({ productId, quantity })) }),
        })
        if (!cart.isAvailable) {
          setError(t('stockChanged'))
          idempotencyKey.current = null
          return
        }
      }

      const order = await apiRequest<Order>('/api/orders', null, {
        method: 'POST',
        headers: { 'Idempotency-Key': idempotencyKey.current },
        body: JSON.stringify({
          customerName,
          customerEmail,
          shippingAddress,
          items: items.map(({ productId, quantity }) => ({ productId, quantity })),
        }),
      })
      window.localStorage.setItem(`store:order-access:${order.id}`, idempotencyKey.current)
      idempotencyKey.current = null
      clear()
      navigate(`/orders/${order.id}`)
    } catch {
      setError(t('checkoutFailed'))
    } finally {
      setSubmitting(false)
    }
  }

  if (items.length === 0) return <section className="panel"><p>{t('cartEmpty')}</p></section>

  return (
    <section className="panel checkout-page">
      <h1>{t('checkoutTitle')}</h1>
      <form onSubmit={(event) => void submit(event)}>
        <label>{t('customerName')}<input required maxLength={200} value={customerName} onChange={(event) => setCustomerName(event.target.value)} /></label>
        <label>{t('customerEmail')}<input required type="email" maxLength={320} value={customerEmail} onChange={(event) => setCustomerEmail(event.target.value)} /></label>
        <label>{t('shippingAddress')}<textarea required maxLength={1000} value={shippingAddress} onChange={(event) => setShippingAddress(event.target.value)} /></label>
        {error && <p role="alert">{error}</p>}
        <button className="primary-button" type="submit" disabled={submitting}>
          {submitting ? t('loading') : t('placeOrder')}
        </button>
      </form>
    </section>
  )
}
