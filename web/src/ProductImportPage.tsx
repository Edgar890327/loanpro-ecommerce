import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, apiRequest } from './api'
import { useAuth } from './auth-context'

type ImportSummary = {
  createdProducts: number
  stockAdded: number
  warnings: number
  rejected: number
  exactDuplicates: number
}

type ImportRow = {
  rowNumber: number
  status: 'imported' | 'imported_with_warning' | 'rejected'
  messages: string[]
}

type ImportReport = {
  fileHash: string
  dryRun: boolean
  reimportDetected: boolean
  previousImportAtUtc: string | null
  summary: ImportSummary
  rows: ImportRow[]
}

type ImportAction = {
  file: File
  dryRun: boolean
  confirmReimport: boolean
}

export function ProductImportPage() {
  const { t } = useTranslation()
  const { token } = useAuth()
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
      return apiRequest<ImportReport>('/api/admin/products/import', token, {
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

      setRequestError(t('importRequestFailed'))
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
              ? ` ${new Date(report.previousImportAtUtc).toLocaleString()}`
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

function ImportReportView({ report }: { report: ImportReport }) {
  const { t } = useTranslation()
  const { summary } = report
  return (
    <div className="import-report">
      <h2>{report.dryRun ? t('importPreviewTitle') : t('importResultTitle')}</h2>
      <p className="import-hash">{t('importFileHash', { hash: report.fileHash })}</p>
      {report.reimportDetected && <p className="import-reimport-mark">{t('importAlreadySeen')}</p>}
      <dl className="import-summary">
        <div><dt>{t('importCreated')}</dt><dd>{summary.createdProducts}</dd></div>
        <div><dt>{t('importStockAdded')}</dt><dd>{summary.stockAdded}</dd></div>
        <div><dt>{t('importWarnings')}</dt><dd>{summary.warnings}</dd></div>
        <div><dt>{t('importRejected')}</dt><dd>{summary.rejected}</dd></div>
        <div><dt>{t('importExactDuplicates')}</dt><dd>{summary.exactDuplicates}</dd></div>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>{t('importRowNumber')}</th>
              <th>{t('importRowStatus')}</th>
              <th>{t('importRowMessages')}</th>
            </tr>
          </thead>
          <tbody>
            {report.rows.map((row) => (
              <tr key={row.rowNumber}>
                <td>{row.rowNumber}</td>
                <td>{t(importStatusKey(row.status))}</td>
                <td>{row.messages.map((message) => translateImportMessage(message, t)).join(' ')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function importStatusKey(status: ImportRow['status']) {
  switch (status) {
    case 'imported':
      return 'importStatusImported'
    case 'imported_with_warning':
      return 'importStatusWarning'
    case 'rejected':
      return 'importStatusRejected'
  }
}

function translateImportMessage(message: string, t: (key: string, options?: Record<string, unknown>) => string) {
  const rowMatch = message.match(/(?:duplicate of row |row |file row )(\d+)/i)
  const row = rowMatch?.[1]

  if (message.startsWith('Exact duplicate of row ')) return t('importMessageExactDuplicate', { row })
  if (message.startsWith('Duplicate SKU;')) return t('importMessageDuplicate', { row })
  if (message.startsWith('Name differs from row ')) return t('importMessageNameFromFile', { row })
  if (message.startsWith('Description differs from row ')) return t('importMessageDescriptionFromFile', { row })
  if (message.startsWith('Category differs from row ')) return t('importMessageCategoryFromFile', { row })
  if (message.startsWith('Price differs from row ')) return t('importMessagePriceFromFile', { row })
  if (message.startsWith('Weight differs from row ')) return t('importMessageWeightFromFile', { row })
  if (message.startsWith('Name differs from existing product')) return t('importMessageNameExisting', { row })
  if (message.startsWith('Description differs from existing product')) return t('importMessageDescriptionExisting', { row })
  if (message.startsWith('Price differs from existing product')) return t('importMessagePriceExisting', { row })

  const exactMessages: Record<string, string> = {
    'The dollar symbol was removed from price.': 'importMessageDollar',
    'Weight is empty; it was imported as null.': 'importMessageEmptyWeight',
    'Category is empty; Uncategorized was used.': 'importMessageEmptyCategory',
    'Name is required.': 'importMessageNameRequired',
    'SKU is required.': 'importMessageSkuRequired',
    'Price must be a valid number.': 'importMessagePriceNumber',
    'Price must be greater than zero with at most two decimal places.': 'importMessagePriceRange',
    'Stock must be a whole number.': 'importMessageStockInteger',
    'Stock cannot be negative.': 'importMessageStockNegative',
    'Weight must be a valid number.': 'importMessageWeightNumber',
    'Weight must be non-negative and fit the supported precision.': 'importMessageWeightRange',
    'The SKU belongs to a logically deleted product.': 'importMessageDeletedSku',
    'The resulting stock would exceed the supported maximum.': 'importMessageStockOverflow',
    'Combined stock for this SKU exceeds the supported maximum.': 'importMessageStockOverflow',
  }
  if (exactMessages[message]) return t(exactMessages[message])

  const lengthMessage = message.match(/^(Name|SKU|Description|Category) must be at most (\d+) characters\.$/)
  if (lengthMessage) {
    const key: Record<string, string> = {
      Name: 'importMessageNameLength',
      SKU: 'importMessageSkuLength',
      Description: 'importMessageDescriptionLength',
      Category: 'importMessageCategoryLength',
    }
    return t(key[lengthMessage[1]], { maximum: lengthMessage[2] })
  }

  return t('importMessageOther', { message })
}
