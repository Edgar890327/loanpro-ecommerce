import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useCart } from '../state/useCart'
import { useAuth } from '../../../shared/auth/auth-context'
import { AuthField } from '../../../components/molecules/AuthField'
import { validateShippingAddress } from '../validation/addressValidation'
import type { ShippingAddressErrors, ShippingAddressValues } from '../validation/addressValidation'
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
  const { user } = useAuth()
  const navigate = useNavigate()
  const idempotencyKey = useRef<string | null>(null)
  const [address, setAddress] = useState<ShippingAddressValues>({
    recipientName: user?.fullName ?? '',
    line1: '',
    line2: '',
    city: '',
    region: '',
    postalCode: '',
    country: '',
  })
  const [errors, setErrors] = useState<ShippingAddressErrors>({})
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [quote, setQuote] = useState<ShippingQuote | null>(null)
  const [quoteError, setQuoteError] = useState(false)

  const cartLines = useMemo(() => items.map(({ productId, quantity }) => ({ productId, quantity })), [items])

  useEffect(() => {
    if (items.length === 0) return
    let active = true
    void apiRequest<CartValidation>('/api/cart/validate', {
      method: 'POST',
      body: JSON.stringify({ items: cartLines }),
    }).then((cart) => {
      if (active && !cart.isAvailable) setError(t('stockChanged'))
    }).catch(() => undefined)
    return () => {
      active = false
    }
  }, [cartLines, items.length, t])

  useEffect(() => {
    setQuote(null)
    setQuoteError(false)
    if (!address.country.trim()) return
    let active = true
    const timeout = window.setTimeout(() => {
      void apiRequest<ShippingQuote>('/api/shipping/quote', {
        method: 'POST',
        body: JSON.stringify({ items: cartLines, destination: address.country }),
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
  }, [cartLines, address.country])

  function update(key: keyof ShippingAddressValues) {
    return (value: string) => setAddress((current) => ({ ...current, [key]: value }))
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    const validation = validateShippingAddress(address, { required: t('addressRequired'), tooLong: t('addressTooLong') })
    setErrors(validation)
    if (Object.keys(validation).length > 0) return
    setSubmitting(true)
    try {
      const retrying = idempotencyKey.current !== null
      idempotencyKey.current ??= createIdempotencyKey()
      if (!retrying) {
        const cart = await apiRequest<CartValidation>('/api/cart/validate', {
          method: 'POST',
          body: JSON.stringify({ items: cartLines }),
        })
        if (!cart.isAvailable) {
          setError(t('stockChanged'))
          idempotencyKey.current = null
          return
        }
      }

      const order = await apiRequest<Order>('/api/orders', {
        method: 'POST',
        headers: { 'Idempotency-Key': idempotencyKey.current },
        body: JSON.stringify({
          shippingAddress: {
            recipientName: address.recipientName.trim(),
            line1: address.line1.trim(),
            line2: address.line2.trim() || null,
            city: address.city.trim(),
            region: address.region.trim(),
            postalCode: address.postalCode.trim(),
            country: address.country.trim(),
          },
          items: cartLines,
        }),
      })
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
      <form onSubmit={(event) => void submit(event)} noValidate>
        <h2>{t('shippingAddress')}</h2>
        <p>{t('contactEmail', { email: user?.email ?? '' })}</p>
        <AuthField label={t('recipientName')} autoComplete="name" value={address.recipientName} error={errors.recipientName} onChange={update('recipientName')} />
        <AuthField label={t('addressLine1')} autoComplete="address-line1" value={address.line1} error={errors.line1} onChange={update('line1')} />
        <AuthField label={t('addressLine2')} autoComplete="address-line2" value={address.line2} error={errors.line2} onChange={update('line2')} />
        <AuthField label={t('addressCity')} autoComplete="address-level2" value={address.city} error={errors.city} onChange={update('city')} />
        <AuthField label={t('addressRegion')} autoComplete="address-level1" value={address.region} error={errors.region} onChange={update('region')} />
        <AuthField label={t('addressPostalCode')} autoComplete="postal-code" value={address.postalCode} error={errors.postalCode} onChange={update('postalCode')} />
        <AuthField label={t('addressCountry')} autoComplete="country-name" value={address.country} error={errors.country} onChange={update('country')} />
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

function createIdempotencyKey() {
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}
