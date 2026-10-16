import { useTranslation } from 'react-i18next'

export function LoadingSkeleton({ rows = 3 }: { rows?: number }) {
  const { t } = useTranslation()
  return (
    <div className="loading-skeleton" role="status" aria-label={t('loading')}>
      {Array.from({ length: rows }, (_, index) => (
        <span key={index} className="skeleton-line" />
      ))}
    </div>
  )
}
