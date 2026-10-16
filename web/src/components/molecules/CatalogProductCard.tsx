import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { StockBadge } from '../atoms/StockBadge'
import { formatCurrency } from '../../shared/i18n/format'

type CatalogProductCardProps = {
  product: {
    id: string
    name: string
    description: string
    category: string
    price: number
    stock: number
  }
}

export function CatalogProductCard({ product }: CatalogProductCardProps) {
  const { t, i18n } = useTranslation()
  const [searchParams] = useSearchParams()

  return (
    <article className="catalog-card">
      <div className="catalog-card-top">
        <StockBadge stock={product.stock} />
        <span className="catalog-category">{product.category}</span>
      </div>
      <h2>
        <Link to={{
          pathname: `/products/${product.id}`,
          search: searchParams.toString() ? `?${searchParams.toString()}` : '',
        }}>
          {product.name}
        </Link>
      </h2>
      <p>{product.description}</p>
      <div className="catalog-card-bottom">
        <strong>{formatCurrency(product.price, i18n.language)}</strong>
        <span>{t('stockCount', { count: product.stock })}</span>
      </div>
    </article>
  )
}
