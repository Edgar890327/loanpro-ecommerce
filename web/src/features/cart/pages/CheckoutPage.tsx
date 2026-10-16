import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useCart } from '../state/useCart'
import { apiRequest, translateApiError } from '../../../shared/api/api'
import { formatCurrency } from '../../../shared/i18n/format'
import type { CartValidation } from '../types/CartValidation'
import type { Order } from '../types/Order'

type ShippingQuote = {
  strategyKey: string
  subtotal: number
  shipping: number
  total: number
}

export function CheckoutPage() {
  const { t, i18n } = useTranslation()
  const { items, clear } = useCart()
  const navigate = useNavigate()
  const idempotencyKey = useRef<string | null>(null)
  const [customerName, setCustomerName] = useState('')
  const [customerEmail, setCustomerEmail] = useState('')
  const [shippingAddress, setShippingAddress] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [quote, setQuote] = useState<ShippingQuote | null>(null)
  const [quoteError, setQuoteError] = useState(false)

  useEffect(() => {
    setQuote(null)
    setQuoteError(false)
    if (!shippingAddress.trim()) return
    let active = true
    const timeout = window.setTimeout(() => {
      void apiRequest<ShippingQuote>('/api/shipping/quote', null, {
        method: 'POST',
        body: JSON.stringify({
          items: items.map(({ productId, quantity }) => ({ productId, quantity })),
          destination: shippingAddress,
        }),
      }).then((result) => {
        if (active) setQuote(result)
      }).catch(() => {
        if (active) setQuoteError(true)
      })
    }, 300)
    return () => {
      active = false
      window.clearTimeout(timeout)
    }
  }, [items, shippingAddress])

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
    } catch (error) {
      setError(translateApiError(error, t, 'checkoutFailed'))
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
        {quote && <p role="status">{t('shippingQuote', { amount: formatCurrency(quote.shipping, i18n.language) })}</p>}
        {quoteError && <p role="alert">{t('shippingQuoteFailed')}</p>}
        {error && <p role="alert">{error}</p>}
        <button className="primary-button" type="submit" disabled={submitting}>
          {submitting ? t('loading') : t('placeOrder')}
        </button>
      </form>
    </section>
  )
}
