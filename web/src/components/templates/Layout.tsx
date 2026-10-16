import { Link, Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../../shared/auth/auth-context'
import { useCart } from '../../features/cart/state/useCart'

export function Layout() {
  const { i18n, t } = useTranslation()
  const { token, signOut } = useAuth()
  const { items } = useCart()

  return (
    <div className="app-shell">
      <header className="site-header">
        <Link className="brand" to="/" aria-label={t('storeHome')}>
          Store
        </Link>
        <nav aria-label={t('mainNavigation')}>
          <Link to="/">{t('home')}</Link>
          <Link to="/cart">{t('cart')} ({items.reduce((count, item) => count + item.quantity, 0)})</Link>
          {token && <Link to="/admin/products">{t('products')}</Link>}
          {token && <Link to="/admin/payments">{t('paymentSettingsTitle')}</Link>}
        </nav>
        {token
          ? <button className="text-button" type="button" onClick={signOut}>{t('logout')}</button>
          : <Link to="/login">{t('login')}</Link>}
        <div className="language-switcher" aria-label={t('language')}>
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
