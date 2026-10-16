import { Link, Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'

export function Layout() {
  const { i18n } = useTranslation()

  return (
    <div className="app-shell">
      <header className="site-header">
        <Link className="brand" to="/" aria-label="Store home">
          Store
        </Link>
        <nav aria-label="Main navigation">
          <Link to="/">Home</Link>
        </nav>
        <div className="language-switcher" aria-label="Language">
          <button
            type="button"
            aria-pressed={i18n.language === 'en-US'}
            onClick={() => void i18n.changeLanguage('en-US')}
          >
            English
          </button>
          <button
            type="button"
            aria-pressed={i18n.language === 'es'}
            onClick={() => void i18n.changeLanguage('es')}
          >
            Español
          </button>
        </div>
      </header>
      <main className="page-content">
        <Outlet />
      </main>
    </div>
  )
}
