import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { apiRequest, translateApiError } from '../../../shared/api/api'
import { CatalogProductCard } from '../../../components/molecules/CatalogProductCard'
import { CatalogFilters } from '../../../components/organisms/CatalogFilters'
import { LoadingSkeleton } from '../../../components/atoms/LoadingSkeleton'
import type { CatalogPageData } from '../types/CatalogPageData'
import type { CatalogCategory } from '../types/CatalogCategory'

const pageSize = 12

function buildCatalogSearchParams(current: URLSearchParams, key: string, value: string): URLSearchParams {
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
        setSearchParams(buildCatalogSearchParams(searchParams, 'q', searchInput.trim()))
      }
    }, 300)
    return () => window.clearTimeout(timeout)
  }, [query, searchInput, searchParams, setSearchParams])

  const page = Math.max(1, parsePositiveInteger(searchParams.get('page'), 1))
  const catalogQuery = useQuery({
    queryKey: ['public-catalog', searchParams.toString()],
    queryFn: () => apiRequest<CatalogPageData>(`/api/catalog?${toApiSearchParams(searchParams, page)}`),
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

      <label className="catalog-search">
        {t('searchProducts')}
        <input
          type="search"
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          placeholder={t('searchPlaceholder')}
        />
      </label>
      <CatalogFilters
        categories={categories.data ?? []}
        category={searchParams.get('category') ?? ''}
        minPrice={searchParams.get('minPrice') ?? ''}
        maxPrice={searchParams.get('maxPrice') ?? ''}
        inStock={searchParams.get('inStock') === 'true'}
        onFilterChange={updateFilter}
      />

      {catalogQuery.isLoading && <LoadingSkeleton rows={6} />}
      {catalogQuery.isError && <p role="alert">{translateApiError(catalogQuery.error, t, 'catalogLoadError')}</p>}
      {categories.isError && <p role="alert">{translateApiError(categories.error, t, 'categoriesLoadError')}</p>}
      {catalogQuery.data?.items.length === 0 && <p role="status">{t('emptyCatalog')}</p>}
      {catalogQuery.data && catalogQuery.data.items.length > 0 && (
        <>
          <div className="catalog-grid">
            {catalogQuery.data.items.map((product) => (
              <CatalogProductCard key={product.id} product={product} />
            ))}
          </div>
          <nav className="pagination" aria-label={t('pagination')}>
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setSearchParams(buildCatalogSearchParams(searchParams, 'page', String(page - 1)))}
            >
              {t('previous')}
            </button>
            <span aria-current="page">{t('page', { page, pages })}</span>
            <button
              type="button"
              disabled={page >= pages}
              onClick={() => setSearchParams(buildCatalogSearchParams(searchParams, 'page', String(page + 1)))}
            >
              {t('next')}
            </button>
          </nav>
        </>
      )}
    </section>
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
