import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, Navigate, Outlet, RouterProvider } from 'react-router-dom'
import { AuthProvider } from '../shared/auth/auth'
import { ProtectedRoute } from '../shared/auth/ProtectedRoute'
import { NotFoundPage } from './NotFoundPage'
import { Layout } from '../components/templates/Layout'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { RegisterPage } from '../features/auth/pages/RegisterPage'
import { ProductImportPage } from '../features/import/pages/ProductImportPage'
import { ProductFormPage } from '../features/products/pages/ProductFormPage'
import { ProductListPage } from '../features/products/pages/ProductListPage'
import { PublicCatalogPage } from '../features/catalog/pages/PublicCatalogPage'
import { PublicProductDetailPage } from '../features/catalog/pages/PublicProductDetailPage'
import { CartPage } from '../features/cart/pages/CartPage'
import { CheckoutPage } from '../features/cart/pages/CheckoutPage'
import { OrderStatusPage } from '../features/cart/pages/OrderStatusPage'
import { SettingsPage } from '../features/settings/pages/SettingsPage'
import { CartProvider } from '../features/cart/state/CartProvider'
import { GlobalErrorBoundary } from './GlobalErrorBoundary'
import { NotificationProvider } from '../shared/notifications/NotificationProvider'
import { OrderHistoryPage } from '../features/orders/pages/OrderHistoryPage'
import { ErrorLogPage } from '../features/errors/pages/ErrorLogPage'

const queryClient = new QueryClient()

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <PublicCatalogPage /> },
      { path: '/products/:id', element: <PublicProductDetailPage /> },
      { path: '/cart', element: <CartPage /> },
      { path: '/checkout', element: <ProtectedRoute role="Customer"><CheckoutPage /></ProtectedRoute> },
      { path: '/orders/:id', element: <ProtectedRoute role="Customer"><OrderStatusPage /></ProtectedRoute> },
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },
      {
        path: '/admin',
        element: <ProtectedRoute role="Admin"><Outlet /></ProtectedRoute>,
        children: [
          { index: true, element: <Navigate to="products" replace /> },
          { path: 'products', element: <ProductListPage /> },
          { path: 'products/import', element: <ProductImportPage /> },
          { path: 'products/new', element: <ProductFormPage /> },
          { path: 'products/:id/edit', element: <ProductFormPage /> },
          { path: 'payments', element: <SettingsPage /> },
          { path: 'settings', element: <SettingsPage /> },
          { path: 'orders', element: <OrderHistoryPage /> },
          { path: 'error-logs', element: <ErrorLogPage /> },
        ],
      },
      {
        path: '*',
        element: <NotFoundPage />,
      },
    ],
  },
])

export default function App() {
  return (
    <AuthProvider>
      <CartProvider>
        <QueryClientProvider client={queryClient}>
          <NotificationProvider>
            <GlobalErrorBoundary>
              <RouterProvider router={router} />
            </GlobalErrorBoundary>
          </NotificationProvider>
        </QueryClientProvider>
      </CartProvider>
    </AuthProvider>
  )
}
