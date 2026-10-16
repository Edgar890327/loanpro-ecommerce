import { useTranslation } from 'react-i18next'

type StockBadgeProps = {
  stock: number
}

export function StockBadge({ stock }: StockBadgeProps) {
  const { t } = useTranslation()
  const status = stock === 0 ? 'out' : stock <= 5 ? 'low' : 'available'
  const label = status === 'out' ? t('outOfStock') : status === 'low' ? t('lowStock') : t('available')

  return <span className={`stock-badge stock-${status}`}>{label}</span>
}
