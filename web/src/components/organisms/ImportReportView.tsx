import { useTranslation } from 'react-i18next'
import type { ImportReport } from '../../features/import/types/ImportReport'

export function ImportReportView({ report }: { report: ImportReport }) {
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

function importStatusKey(status: ImportReport['rows'][number]['status']) {
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
