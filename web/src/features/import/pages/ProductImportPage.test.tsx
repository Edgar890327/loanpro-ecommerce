import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ProductImportPage } from './ProductImportPage'
import i18n from '../../../shared/i18n/i18n'

const previewReport = {
  fileHash: 'ABCDEF1234567890',
  dryRun: true,
  reimportDetected: true,
  previousImportAtUtc: '2026-10-15T12:00:00Z',
  summary: {
    createdProducts: 2,
    stockAdded: 7,
    warnings: 1,
    rejected: 1,
    exactDuplicates: 0,
  },
  rows: [{
    rowNumber: 2,
    status: 'imported_with_warning',
    messages: ['The dollar symbol was removed from price.'],
  }, {
    rowNumber: 3,
    status: 'rejected',
    messages: ['Name is required.'],
  }],
}

function renderImportPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/admin/products/import']}>
        <Routes>
          <Route path="/admin/products/import" element={<ProductImportPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('product CSV import page', () => {
  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
    void i18n.changeLanguage('en-US')
  })

  it('previews, requires explicit reimport confirmation, applies and downloads the report', async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      const form = init?.body as FormData
      if (form.get('dryRun') === 'true') return jsonResponse(previewReport)
      return jsonResponse({ ...previewReport, dryRun: false })
    })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: vi.fn(() => 'blob:report'),
      revokeObjectURL: vi.fn(),
    })
    renderImportPage()

    const file = new File(['name,sku,description,category,price,stock,weight_kg'], 'products.csv', {
      type: 'text/csv',
    })
    fireEvent.change(screen.getByLabelText('CSV file'), { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Preview import' }))

    expect(await screen.findByText('This file has been imported before. Applying it again adds its stock again.'))
      .toBeInTheDocument()
    expect(screen.getByText('The dollar sign was removed from the price.')).toBeInTheDocument()
    expect(screen.getByText('Name is required.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply import' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Download report' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('checkbox', {
      name: 'I understand this reimport will add stock again.',
    }))
    fireEvent.click(screen.getByRole('button', { name: 'Apply import' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Import complete' })).toBeDisabled())
    expect(fetchMock).toHaveBeenCalledTimes(2)
    const [, request] = fetchMock.mock.calls[1]
    const body = request?.body as FormData
    expect(body.get('dryRun')).toBe('false')
    expect(body.get('confirmReimport')).toBe('true')
    expect(body.get('file')).toBeInstanceOf(File)
    expect(new Headers(request?.headers).has('Content-Type')).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Download report' }))
    expect(URL.createObjectURL).toHaveBeenCalledOnce()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:report')
  })

  it('allows previewing a new file after a successful import', async () => {
    const fetchMock = vi.fn(async () => jsonResponse({
      ...previewReport,
      dryRun: true,
      reimportDetected: false,
      previousImportAtUtc: null,
    }))
    vi.stubGlobal('fetch', fetchMock)
    renderImportPage()

    fireEvent.change(screen.getByLabelText('CSV file'), {
      target: { files: [new File(['csv'], 'next.csv', { type: 'text/csv' })] },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Preview import' }))

    expect(await screen.findByText('Import preview')).toBeInTheDocument()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply import' })).toBeEnabled()
  })
})
