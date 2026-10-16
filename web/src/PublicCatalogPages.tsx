import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { apiRequest } from './api'

type CatalogProduct = {
  id: string
  name: string
  sku: string
  description: string
  category: string
  price: number
  stock: number
  weightKg: number | null
}

type CatalogPageData = {
  items: CatalogProduct[]
  page: number
  pageSize: number
  totalCount: number
}

type CatalogCategory = {
  id: string
  name: string
  productCount: number
}

const pageSize = 12

function buildCatalogSearchParams(
  current: URLSearchParams,
  key: string,
  value: string,
): URLSearchParams {
  const next = new URLSearchParams(current)
  if (value) next.set(key, value)
  else next.delete(key)
  if (key !== 'page') next.delete('page')
  return next
}

export function PublicCatalogPage() {
  const { t } = useTranslation()
  const [searchParams, setSearchParams] = useSearchParams()
  const query = searchParams.get('q') ?? ''
  const [searchInput, setSearchInput] = useState(query)

  useEffect(() => {
    setSearchInput(query)
  }, [query])

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      if (searchInput.trim() !== query) {
        setSearchParams(
          buildCatalogSearchParams(searchParams, 'q', searchInput.trim()),
        )
      }
    }, 300)
    return () => window.clearTimeout(timeout)
  }, [query, searchInput, searchParams, setSearchParams])

  const page = Math.max(1, parsePositiveInteger(searchParams.get('page'), 1))
  const catalogQuery = useQuery({
    queryKey: ['public-catalog', searchParams.toString()],
    queryFn: () => apiRequest<CatalogPageData>(
      `/api/catalog?${toApiSearchParams(searchParams, page)}`,
    ),
  })
  const categories = useQuery({
    queryKey: ['public-catalog-categories'],
    queryFn: () => apiRequest<CatalogCategory[]>('/api/catalog/categories'),
    staleTime: 60_000,
  })
  const pages = Math.max(1, Math.ceil((catalogQuery.data?.totalCount ?? 0) / pageSize))

  function updateFilter(key: string, value: string) {
    setSearchParams(buildCatalogSearchParams(searchParams, key, value))
  }

  return (
    <section className="catalog-page">
      <div className="catalog-heading">
        <div>
          <p className="eyebrow">{t('storefront')}</p>
          <h1>{t('catalogTitle')}</h1>
        </div>
        <label className="catalog-sort">
          {t('sortBy')}
          <select
            aria-label={t('sortBy')}
            value={searchParams.get('sort') ?? 'relevance'}
            onChange={(event) => updateFilter('sort', event.target.value === 'relevance' ? '' : event.target.value)}
          >
            <option value="relevance">{t('relevance')}</option>
            <option value="price">{t('priceLowHigh')}</option>
            <option value="name">{t('nameAZ')}</option>
          </select>
        </label>
      </div>

      <div className="catalog-toolbar">
        <label className="catalog-search">
          {t('searchProducts')}
          <input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder={t('searchPlaceholder')}
          />
        </label>
        <label>
          {t('category')}
          <select
            aria-label={t('category')}
            value={searchParams.get('category') ?? ''}
            onChange={(event) => updateFilter('category', event.target.value)}
          >
            <option value="">{t('allCategories')}</option>
            {categories.data?.map((category) => (
              <option key={category.id} value={category.name}>
                {category.name} ({category.productCount})
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('minPrice')}
          <input
            type="number"
            min="0"
            step="0.01"
            aria-label={t('minPrice')}
            value={searchParams.get('minPrice') ?? ''}
            onChange={(event) => updateFilter('minPrice', event.target.value)}
          />
        </label>
        <label>
          {t('maxPrice')}
          <input
            type="number"
            min="0"
            step="0.01"
            aria-label={t('maxPrice')}
            value={searchParams.get('maxPrice') ?? ''}
            onChange={(event) => updateFilter('maxPrice', event.target.value)}
          />
        </label>
        <label className="stock-filter">
          <input
            type="checkbox"
            checked={searchParams.get('inStock') === 'true'}
            onChange={(event) => updateFilter('inStock', event.target.checked ? 'true' : '')}
          />
          {t('inStockOnly')}
        </label>
      </div>

      {catalogQuery.isLoading && <p role="status">{t('loading')}</p>}
      {catalogQuery.isError && <p role="alert">{t('catalogLoadError')}</p>}
      {categories.isError && <p role="alert">{t('categoriesLoadError')}</p>}
      {catalogQuery.data?.items.length === 0 && <p role="status">{t('emptyCatalog')}</p>}
      {catalogQuery.data && catalogQuery.data.items.length > 0 && (
        <>
          <div className="catalog-grid">
            {catalogQuery.data.items.map((product) => (
              <article className="catalog-card" key={product.id}>
                <div className="catalog-card-top">
                  <span className={stockClass(product.stock)}>
                    {stockLabel(product.stock, t)}
                  </span>
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
            ))}
          </div>
          <div className="pagination">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setSearchParams(buildCatalogSearchParams(searchParams, 'page', String(page - 1)))}
            >
              {t('previous')}
            </button>
            <span>{t('page', { page, pages })}</span>
            <button
              type="button"
              disabled={page >= pages}
              onClick={() => setSearchParams(buildCatalogSearchParams(searchParams, 'page', String(page + 1)))}
            >
              {t('next')}
            </button>
          </div>
        </>
      )}
    </section>
  )
}

export function PublicProductDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams()
  const [searchParams] = useSearchParams()
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
        <div><dt>{t('availability')}</dt><dd>{stockLabel(product.data.stock, t)}</dd></div>
      </dl>
    </article>
  )
}

function toApiSearchParams(params: URLSearchParams, page: number) {
  const apiParams = new URLSearchParams(params)
  apiParams.set('page', String(page))
  apiParams.set('pageSize', String(pageSize))
  return apiParams.toString()
}

function parsePositiveInteger(value: string | null, fallback: number) {
  if (!value) return fallback
  const parsed = Number(value)
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : fallback
}

function stockLabel(stock: number, t: (key: string) => string) {
  if (stock === 0) return t('outOfStock')
  if (stock <= 5) return t('lowStock')
  return t('available')
}

function stockClass(stock: number) {
  if (stock === 0) return 'stock-badge stock-out'
  if (stock <= 5) return 'stock-badge stock-low'
  return 'stock-badge stock-available'
}
