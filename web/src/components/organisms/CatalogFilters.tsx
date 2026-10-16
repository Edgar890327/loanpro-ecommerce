import { useTranslation } from 'react-i18next'

type CatalogFiltersProps = {
  categories: { id: string; name: string; productCount: number }[]
  category: string
  minPrice: string
  maxPrice: string
  inStock: boolean
  onFilterChange: (key: string, value: string) => void
}

export function CatalogFilters({
  categories,
  category,
  minPrice,
  maxPrice,
  inStock,
  onFilterChange,
}: CatalogFiltersProps) {
  const { t } = useTranslation()

  return (
    <div className="catalog-toolbar">
      <label>
        {t('category')}
        <select
          aria-label={t('category')}
          value={category}
          onChange={(event) => onFilterChange('category', event.target.value)}
        >
          <option value="">{t('allCategories')}</option>
          {categories.map((item) => (
            <option key={item.id} value={item.name}>
              {item.name} ({item.productCount})
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
          value={minPrice}
          onChange={(event) => onFilterChange('minPrice', event.target.value)}
        />
      </label>
      <label>
        {t('maxPrice')}
        <input
          type="number"
          min="0"
          step="0.01"
          aria-label={t('maxPrice')}
          value={maxPrice}
          onChange={(event) => onFilterChange('maxPrice', event.target.value)}
        />
      </label>
      <label className="stock-filter">
        <input
          type="checkbox"
          checked={inStock}
          onChange={(event) => onFilterChange('inStock', event.target.checked ? 'true' : '')}
        />
        {t('inStockOnly')}
      </label>
    </div>
  )
}
