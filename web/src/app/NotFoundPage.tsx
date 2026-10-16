import { useTranslation } from 'react-i18next'

export function NotFoundPage() {
  const { t } = useTranslation()
  return <section className="welcome-panel"><h1>{t('notFound')}</h1></section>
}
