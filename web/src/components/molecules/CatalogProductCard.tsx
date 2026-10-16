import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { StockBadge } from '../atoms/StockBadge'

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
  const { t } = useTranslation()
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
        <strong>${product.price.toFixed(2)}</strong>
        <span>{t('stockCount', { count: product.stock })}</span>
      </div>
    </article>
  )
}
