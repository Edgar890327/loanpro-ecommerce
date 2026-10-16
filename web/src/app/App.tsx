import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, Navigate, Outlet, RouterProvider } from 'react-router-dom'
import { AuthProvider } from '../shared/auth/auth'
import { ProtectedRoute } from '../shared/auth/ProtectedRoute'
import { NotFoundPage } from './NotFoundPage'
import { Layout } from '../components/templates/Layout'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { ProductImportPage } from '../features/import/pages/ProductImportPage'
import { ProductFormPage } from '../features/products/pages/ProductFormPage'
import { ProductListPage } from '../features/products/pages/ProductListPage'
import { PublicCatalogPage } from '../features/catalog/pages/PublicCatalogPage'
import { PublicProductDetailPage } from '../features/catalog/pages/PublicProductDetailPage'
import { CartPage } from '../features/cart/pages/CartPage'
import { CheckoutPage } from '../features/cart/pages/CheckoutPage'
import { OrderStatusPage } from '../features/cart/pages/OrderStatusPage'
import { PaymentProviderSettingsPage } from '../features/payments/pages/PaymentProviderSettingsPage'
import { CartProvider } from '../features/cart/state/CartProvider'

const queryClient = new QueryClient()

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <PublicCatalogPage /> },
      { path: '/products/:id', element: <PublicProductDetailPage /> },
      { path: '/cart', element: <CartPage /> },
      { path: '/checkout', element: <CheckoutPage /> },
      { path: '/orders/:id', element: <OrderStatusPage /> },
      { path: '/login', element: <LoginPage /> },
      {
        path: '/admin',
        element: <ProtectedRoute><Outlet /></ProtectedRoute>,
        children: [
          { index: true, element: <Navigate to="products" replace /> },
          { path: 'products', element: <ProductListPage /> },
          { path: 'products/import', element: <ProductImportPage /> },
          { path: 'products/new', element: <ProductFormPage /> },
          { path: 'products/:id/edit', element: <ProductFormPage /> },
          { path: 'payments', element: <PaymentProviderSettingsPage /> },
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
          <RouterProvider router={router} />
        </QueryClientProvider>
      </CartProvider>
    </AuthProvider>
  )
}
