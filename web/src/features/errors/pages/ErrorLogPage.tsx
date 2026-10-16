import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiRequest, translateApiError } from '../../../shared/api/api'
import { useAuth } from '../../../shared/auth/auth-context'
import { useNotifications } from '../../../shared/notifications/useNotifications'
import { formatDate } from '../../../shared/i18n/format'
import { LoadingSkeleton } from '../../../components/atoms/LoadingSkeleton'
import type { ErrorLog, ErrorLogPageData } from '../types/ErrorLog'

const pageSize = 20

export function ErrorLogPage() {
  const { t, i18n } = useTranslation()
  const { token } = useAuth()
  const { notify } = useNotifications()
  const [fromDateInput, setFromDateInput] = useState('')
  const [toDateInput, setToDateInput] = useState('')
  const [levelInput, setLevelInput] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [level, setLevel] = useState('')
  const [correlationInput, setCorrelationInput] = useState('')
  const [correlationId, setCorrelationId] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<ErrorLog | null>(null)

  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  })
  if (fromDate) query.set('fromUtc', new Date(`${fromDate}T00:00:00`).toISOString())
  if (toDate) query.set('toUtc', new Date(`${toDate}T23:59:59.999`).toISOString())
  if (level) query.set('level', level)
  if (correlationId) query.set('correlationId', correlationId.trim())
  const logs = useQuery({
    queryKey: ['error-logs', fromDate, toDate, level, correlationId, page],
    queryFn: () => apiRequest<ErrorLogPageData>(`/api/admin/error-logs?${query}`, token),
  })
  const pages = Math.max(1, Math.ceil((logs.data?.totalCount ?? 0) / pageSize))

  function applyFilters(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPage(1)
    setFromDate(fromDateInput)
    setToDate(toDateInput)
    setLevel(levelInput)
    setCorrelationId(correlationInput)
  }

  async function copyCorrelationId(value: string) {
    try {
      await navigator.clipboard.writeText(value)
      notify(t('correlationIdCopied'))
    } catch {
      notify(t('correlationIdCopyFailed'), 'error')
    }
  }

  return (
    <section className="panel">
      <h1>{t('errorLogs')}</h1>
      <form className="error-log-filters" onSubmit={applyFilters}>
        <label>
          {t('errorLogDateFrom')}
          <input type="date" value={fromDateInput} onChange={(event) => setFromDateInput(event.target.value)} />
        </label>
        <label>
          {t('errorLogDateTo')}
          <input type="date" value={toDateInput} onChange={(event) => setToDateInput(event.target.value)} />
        </label>
        <label>
          {t('errorLevel')}
          <select value={levelInput} onChange={(event) => setLevelInput(event.target.value)}>
            <option value="">{t('allLevels')}</option>
            <option value="Error">{t('logLevel_Error')}</option>
            <option value="Warning">{t('logLevel_Warning')}</option>
            <option value="Critical">{t('logLevel_Critical')}</option>
          </select>
        </label>
        <label>
          {t('correlationId')}
          <input value={correlationInput} onChange={(event) => setCorrelationInput(event.target.value)} />
        </label>
        <button type="submit" className="secondary-button">{t('applyFilters')}</button>
      </form>
      {logs.isLoading && <LoadingSkeleton rows={5} />}
      {logs.isError && <p role="alert">{translateApiError(logs.error, t, 'errorLogLoadFailed')}</p>}
      {logs.data?.items.length === 0 && <p role="status">{t('errorLogEmpty')}</p>}
      {logs.data && logs.data.items.length > 0 && (
        <>
          <div className="table-scroll">
            <table>
              <caption className="sr-only">{t('errorLogs')}</caption>
              <thead>
                <tr>
                  <th scope="col">{t('errorLogTimestamp')}</th>
                  <th scope="col">{t('errorLevel')}</th>
                  <th scope="col">{t('correlationId')}</th>
                  <th scope="col">{t('errorLogStatus')}</th>
                  <th scope="col">{t('actions')}</th>
                </tr>
              </thead>
              <tbody>
                {logs.data.items.map((log) => (
                  <tr key={log.id}>
                    <td>{formatDate(log.timestampUtc, i18n.language)}</td>
                    <td>{t(`logLevel_${log.level}`, { defaultValue: log.level })}</td>
                    <td className="correlation-id">{log.correlationId}</td>
                    <td>{log.statusCode}</td>
                    <td>
                      <button type="button" className="text-button" onClick={() => setSelected(log)}>
                        {t('viewDetails')}
                      </button>
                      <button type="button" className="text-button" onClick={() => void copyCorrelationId(log.correlationId)}>
                        {t('copyCorrelationId')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <nav className="pagination" aria-label={t('pagination')}>
            <button type="button" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>
              {t('previous')}
            </button>
            <span aria-current="page">{t('page', { page, pages })}</span>
            <button type="button" disabled={page >= pages} onClick={() => setPage((value) => value + 1)}>
              {t('next')}
            </button>
          </nav>
        </>
      )}
      {selected && (
        <section className="error-log-detail-panel" aria-labelledby="error-log-detail-title">
            <div className="page-heading">
              <h2 id="error-log-detail-title">{t('errorLogDetails')}</h2>
              <button type="button" className="secondary-button" onClick={() => setSelected(null)}>
                {t('closeDetails')}
              </button>
            </div>
            <dl className="error-log-details">
              <div><dt>{t('errorLogTimestamp')}</dt><dd>{formatDate(selected.timestampUtc, i18n.language)}</dd></div>
              <div><dt>{t('errorLevel')}</dt><dd>{t(`logLevel_${selected.level}`, { defaultValue: selected.level })}</dd></div>
              <div><dt>{t('correlationId')}</dt><dd>{selected.correlationId}</dd></div>
              <div><dt>{t('errorLogMethod')}</dt><dd>{selected.httpMethod}</dd></div>
              <div><dt>{t('errorLogPath')}</dt><dd>{selected.path}</dd></div>
              <div><dt>{t('errorLogStatus')}</dt><dd>{selected.statusCode}</dd></div>
              <div><dt>{t('errorLogMessage')}</dt><dd>{selected.message}</dd></div>
              <div><dt>{t('errorLogException')}</dt><dd>{selected.exceptionType}</dd></div>
            </dl>
            <h3>{t('errorLogStackTrace')}</h3>
            <pre className="stack-trace">{selected.stackTrace || t('notSpecified')}</pre>
        </section>
      )}
    </section>
  )
}
