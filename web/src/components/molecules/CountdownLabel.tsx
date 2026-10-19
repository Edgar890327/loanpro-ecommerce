import { useTranslation } from 'react-i18next'
import { formatNumber } from '../../shared/i18n/format'

export function CountdownLabel({ seconds }: { seconds: number }) {
  const { t, i18n } = useTranslation()
  return (
    <span role="timer">
      {t('reservationCountdown', {
        minutes: formatNumber(Math.floor(seconds / 60), i18n.language, 0),
        seconds: String(seconds % 60).padStart(2, '0'),
      })}
    </span>
  )
}
