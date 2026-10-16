import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import App from './App'
import i18n from './i18n'

describe('application shell', () => {
  afterEach(() => {
    void i18n.changeLanguage('en-US')
  })

  it('renders the layout and changes the active language', async () => {
    render(<App />)

    expect(screen.getByRole('link', { name: 'Store home' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Welcome' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'English' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )

    fireEvent.click(screen.getByRole('button', { name: 'Español' }))

    expect(await screen.findByRole('button', { name: 'Español' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })
})
