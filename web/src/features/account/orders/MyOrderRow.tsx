import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { CountdownLabel } from '../../../components/molecules/CountdownLabel'
import { formatCurrency, formatDate } from '../../../shared/i18n/format'
import type { MyOrderSummary } from '../types/MyOrderSummary'
import { describeOrder } from './describeOrder'

export function MyOrderRow({ order, now }: { order: MyOrderSummary, now: number }) {
  const { t, i18n } = useTranslation()
  const presentation = describeOrder(order.state, order.reservedUntilUtc, now)
  return (
    <tr>
      <th scope="row"><Link to={`/account/orders/${order.id}`}>{order.orderNumber}</Link></th>
      <td>{formatDate(order.createdAtUtc, i18n.language)}</td>
      <td>{t(`orderState${presentation.state}`)}</td>
      <td>{formatCurrency(order.total, i18n.language)}</td>
      <td>
        {presentation.canContinuePayment && (
          <>
            <Link className="secondary-button" to={`/orders/${order.id}`}>{t('continuePayment')}</Link>
            {' '}
            <CountdownLabel seconds={presentation.secondsRemaining} />
          </>
        )}
      </td>
    </tr>
  )
}
