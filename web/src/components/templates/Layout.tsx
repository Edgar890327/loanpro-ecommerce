import { Link, Outlet, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../../shared/auth/auth-context'
import { useCart } from '../../features/cart/state/useCart'

export function Layout() {
  const { i18n, t } = useTranslation()
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const isAdmin = user?.role === 'Admin'
  const { items } = useCart()

  return (
    <div className="app-shell">
      <header className="site-header">
        <a className="skip-link" href="#main-content">{t('skipToContent')}</a>
        <Link className="brand" to="/" aria-label={t('storeHome')}>
          Store
        </Link>
        <nav aria-label={t('mainNavigation')}>
          <Link to="/">{t('home')}</Link>
          <Link to="/cart">{t('cart')} ({items.reduce((count, item) => count + item.quantity, 0)})</Link>
          {isAdmin && <Link to="/admin/products">{t('products')}</Link>}
          {isAdmin && <Link to="/admin/settings">{t('settingsTitle')}</Link>}
          {isAdmin && <Link to="/admin/orders">{t('orderHistory')}</Link>}
          {isAdmin && <Link to="/admin/error-logs">{t('errorLogs')}</Link>}
        </nav>
        <div className="account-menu" role="group" aria-label={t('accountMenu')}>
          {user
            ? (
              <>
                <span className="account-name">{user.fullName}</span>
                {user.role === 'Customer' && <Link to="/account/orders">{t('myOrders')}</Link>}
                <button
                  className="text-button"
                  type="button"
                  onClick={() => void logout().finally(() => navigate('/'))}
                >
                  {t('logout')}
                </button>
              </>
            )
            : (
              <>
                <Link to="/login">{t('login')}</Link>
                <Link to="/register">{t('register')}</Link>
              </>
            )}
        </div>
        <div className="language-switcher" role="group" aria-label={t('language')}>
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
      <main id="main-content" className="page-content" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  )
}
