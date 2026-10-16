export type ImportReport = {
  fileHash: string
  dryRun: boolean
  reimportDetected: boolean
  previousImportAtUtc: string | null
  summary: {
    createdProducts: number
    stockAdded: number
    warnings: number
    rejected: number
    exactDuplicates: number
  }
  rows: {
    rowNumber: number
    status: 'imported' | 'imported_with_warning' | 'rejected'
    messages: string[]
  }[]
}
