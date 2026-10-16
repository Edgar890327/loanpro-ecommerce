import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { apiRequest, translateApiError } from '../../../shared/api/api'
import { useCart } from '../state/useCart'
import type { CartValidation } from '../types/CartValidation'
import { formatCurrency } from '../../../shared/i18n/format'
import { LoadingSkeleton } from '../../../components/atoms/LoadingSkeleton'

export function CartPage() {
  const { t, i18n } = useTranslation()
  const { items, setQuantity, removeProduct, updateAvailability } = useCart()
  const [stockChanged, setStockChanged] = useState(false)
  const validation = useQuery({
    queryKey: ['cart-validation', items.map((item) => `${item.productId}:${item.quantity}`).join(',')],
    queryFn: () => apiRequest<CartValidation>('/api/cart/validate', null, {
      method: 'POST',
      body: JSON.stringify({ items: items.map(({ productId, quantity }) => ({ productId, quantity })) }),
    }),
    enabled: items.length > 0,
    retry: false,
  })
  useEffect(() => {
    if (!validation.data) return
    for (const item of items) {
      const issue = validation.data.issues.find((entry) => entry.productId === item.productId)
      const line = validation.data.items.find((entry) => entry.productId === item.productId)
      const available = issue?.available ?? line?.available ?? 0
      if (available !== item.available ||
          (line && (line.unitPrice !== item.price || line.name !== item.name || line.sku !== item.sku))) {
        setStockChanged(true)
      }
      updateAvailability(item.productId, available, line
        ? { name: line.name, sku: line.sku, price: line.unitPrice }
        : undefined)
    }
  }, [items, updateAvailability, validation.data])
  const unavailable = validation.data?.issues ?? []
  const subtotal = validation.data?.subtotal ?? items.reduce((sum, item) => sum + item.price * item.quantity, 0)
  const shipping = validation.data?.shipping ?? 0

  if (items.length === 0) {
    return <section className="panel"><h1>{t('cartTitle')}</h1><p>{t('cartEmpty')}</p><Link to="/">{t('backToCatalog')}</Link></section>
  }

  return (
    <section className="panel cart-page">
      <h1>{t('cartTitle')}</h1>
      {validation.isLoading && <LoadingSkeleton rows={3} />}
      {validation.isError && <p role="alert">{translateApiError(validation.error, t, 'cartValidationFailed')}</p>}
      {(unavailable.length > 0 || stockChanged) && <p role="alert">{t('stockChanged')}</p>}
      <div className="cart-lines">
        {items.map((item) => {
          const issue = unavailable.find((entry) => entry.productId === item.productId)
          const currentLine = validation.data?.items.find((line) => line.productId === item.productId)
          const available = issue?.available ?? currentLine?.available ?? item.available
          return (
            <article className="cart-line" key={item.productId}>
              <div>
                <h2>{item.name}</h2>
                <p>{item.sku}</p>
                <p>{formatCurrency(currentLine?.subtotal ?? item.price * item.quantity, i18n.language)}</p>
                {issue && <p className="form-error">{t('stockAvailable', { count: issue.available })}</p>}
              </div>
              <label>
                {t('quantity')}
                <input
                  aria-label={t('quantityFor', { name: item.name })}
                  type="number"
                  min={1}
                  max={available}
                  value={item.quantity}
                  disabled={available <= 0}
                  onChange={(event) => setQuantity(item.productId, Number(event.target.value))}
                />
              </label>
              <button type="button" className="text-button" onClick={() => removeProduct(item.productId)}>{t('remove')}</button>
            </article>
          )
        })}
      </div>
      <dl className="order-totals">
        <div><dt>{t('subtotal')}</dt><dd>{formatCurrency(subtotal, i18n.language)}</dd></div>
        <div><dt>{t('shipping')}</dt><dd>{formatCurrency(shipping, i18n.language)}</dd></div>
        <div><dt>{t('total')}</dt><dd>{formatCurrency(subtotal + shipping, i18n.language)}</dd></div>
      </dl>
      <Link
        className="primary-button"
        to="/checkout"
        aria-disabled={validation.isLoading || validation.isError || unavailable.length > 0}
        onClick={(event) => {
          if (validation.isLoading || validation.isError || unavailable.length > 0) event.preventDefault()
        }}
      >
        {t('continueToCheckout')}
      </Link>
    </section>
  )
}
