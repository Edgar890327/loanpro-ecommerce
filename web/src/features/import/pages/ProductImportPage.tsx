import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, apiRequest, translateApiError } from '../../../shared/api/api'
import { ImportReportView } from '../../../components/organisms/ImportReportView'
import type { ImportReport } from '../types/ImportReport'
import { formatDate } from '../../../shared/i18n/format'

type ImportAction = {
  file: File
  dryRun: boolean
  confirmReimport: boolean
}

export function ProductImportPage() {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [report, setReport] = useState<ImportReport | null>(null)
  const [confirmReimport, setConfirmReimport] = useState(false)
  const [confirmationRequired, setConfirmationRequired] = useState(false)
  const [applied, setApplied] = useState(false)
  const [requestError, setRequestError] = useState('')

  const importFile = useMutation({
    mutationFn: async (action: ImportAction) => {
      const data = new FormData()
      data.append('file', action.file)
      data.append('dryRun', String(action.dryRun))
      data.append('confirmReimport', String(action.confirmReimport))
      return apiRequest<ImportReport>('/api/admin/products/import', {
        method: 'POST',
        body: data,
      })
    },
    onSuccess: async (result, action) => {
      setReport(result)
      setConfirmationRequired(result.reimportDetected)
      setApplied(!action.dryRun)
      setRequestError('')
      if (!action.dryRun) {
        await queryClient.invalidateQueries({ queryKey: ['products'] })
        await queryClient.invalidateQueries({ queryKey: ['public-catalog'] })
      }
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        setConfirmationRequired(true)
        setRequestError(t('importReimportRace'))
        return
      }

      setRequestError(translateApiError(error, t, 'importRequestFailed'))
    },
  })

  function selectFile(selected: File | null) {
    setFile(selected)
    setReport(null)
    setConfirmReimport(false)
    setConfirmationRequired(false)
    setApplied(false)
    setRequestError('')
  }

  function downloadReport() {
    if (!report) return
    const content = new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(content)
    const link = document.createElement('a')
    link.href = url
    link.download = `product-import-${report.fileHash.slice(0, 12)}.json`
    link.click()
    URL.revokeObjectURL(url)
  }

  const canApply = Boolean(report?.dryRun && !applied && file && (
    !confirmationRequired || confirmReimport
  ))

  return (
    <section className="panel import-panel">
      <div className="page-heading">
        <h1>{t('importProductsTitle')}</h1>
        <Link to="/admin/products">{t('backToProducts')}</Link>
      </div>
      <p>{t('importProductsDescription')}</p>
      <label className="import-file">
        {t('importCsvFile')}
        <input
          type="file"
          accept=".csv,text/csv"
          onChange={(event) => selectFile(event.target.files?.[0] ?? null)}
        />
      </label>
      {file && <p className="import-file-name">{t('selectedImportFile', { name: file.name })}</p>}
      <div className="form-actions">
        <button
          type="button"
          className="secondary-button"
          disabled={!file || importFile.isPending}
          onClick={() => file && importFile.mutate({
            file,
            dryRun: true,
            confirmReimport: false,
          })}
        >
          {importFile.isPending ? t('loading') : t('previewImport')}
        </button>
        {report && (
          <button
            type="button"
            className="primary-button"
            disabled={!canApply || importFile.isPending}
            onClick={() => file && importFile.mutate({
              file,
              dryRun: false,
              confirmReimport,
            })}
          >
            {applied ? t('importComplete') : t('confirmImport')}
          </button>
        )}
        {report && (
          <button type="button" className="secondary-button" onClick={downloadReport}>
            {t('downloadImportReport')}
          </button>
        )}
      </div>
      {confirmationRequired && (
        <div className="import-warning" role="alert">
          <p>{t('importReimportWarning', {
            date: report?.previousImportAtUtc
              ? ` ${formatDate(report.previousImportAtUtc, i18n.language)}`
              : '',
          })}</p>
          <label className="import-confirm">
            <input
              type="checkbox"
              checked={confirmReimport}
              onChange={(event) => setConfirmReimport(event.target.checked)}
            />
            {t('confirmReimportCheckbox')}
          </label>
        </div>
      )}
      {requestError && <p role="alert" className="form-error">{requestError}</p>}
      {importFile.isError && !requestError && (
        <p role="alert" className="form-error">{t('importRequestFailed')}</p>
      )}
      {report && <ImportReportView report={report} />}
    </section>
  )
}
