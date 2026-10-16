import { useQuery } from '@tanstack/react-query'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { apiRequest } from '../../../shared/api/api'
import { StockBadge } from '../../../components/atoms/StockBadge'
import type { CatalogProduct } from '../types/CatalogProduct'
import { useCart } from '../../cart/state/useCart'

export function PublicProductDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams()
  const [searchParams] = useSearchParams()
  const { addProduct } = useCart()
  const product = useQuery({
    queryKey: ['public-product', id],
    queryFn: () => apiRequest<CatalogProduct>(`/api/catalog/${id}`),
    enabled: Boolean(id),
  })

  if (product.isLoading) return <p role="status">{t('loading')}</p>
  if (product.isError || !product.data) {
    return <section className="panel"><p role="alert">{t('productLoadError')}</p><Link to="/">{t('backToCatalog')}</Link></section>
  }

  return (
    <article className="panel product-detail">
      <Link to={{ pathname: '/', search: searchParams.toString() ? `?${searchParams.toString()}` : '' }}>
        {t('backToCatalog')}
      </Link>
      <p className="eyebrow">{product.data.category}</p>
      <h1>{product.data.name}</h1>
      <p>{product.data.description}</p>
      <dl>
        <div><dt>{t('sku')}</dt><dd>{product.data.sku}</dd></div>
        <div><dt>{t('price')}</dt><dd>${product.data.price.toFixed(2)}</dd></div>
        <div><dt>{t('weight')}</dt><dd>{product.data.weightKg ?? t('notSpecified')}</dd></div>
        <div><dt>{t('availability')}</dt><dd><StockBadge stock={product.data.stock} /></dd></div>
      </dl>
      <button type="button" disabled={product.data.stock <= 0} onClick={() => addProduct(product.data!)}>
        {product.data.stock > 0 ? t('addToCart') : t('outOfStock')}
      </button>
    </article>
  )
}
