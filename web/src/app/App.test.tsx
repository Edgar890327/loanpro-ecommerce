import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import i18n from '../shared/i18n/i18n'

describe('application shell', () => {
  afterEach(() => {
    void i18n.changeLanguage('en-US')
    vi.unstubAllGlobals()
  })

  it('renders the layout and changes the active language', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const body = String(input).includes('/categories')
        ? []
        : { items: [], page: 1, pageSize: 12, totalCount: 0 }
      return new Response(JSON.stringify(body), {
        headers: { 'Content-Type': 'application/json' },
      })
    }))
    render(<App />)

    expect(screen.getByRole('link', { name: 'Store home' })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Product catalog' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'English' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )

    fireEvent.click(screen.getByRole('button', { name: 'Español' }))

    expect(await screen.findByRole('button', { name: 'Español' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    expect(await screen.findByRole('heading', { name: 'Catálogo de productos' })).toBeInTheDocument()
  })
})
