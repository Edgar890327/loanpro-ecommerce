import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, Navigate, Outlet, RouterProvider } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AuthProvider, ProtectedRoute } from './auth'
import { Layout } from './Layout'
import { LoginPage } from './LoginPage'
import { ProductFormPage, ProductListPage } from './ProductPages'
import { PublicCatalogPage, PublicProductDetailPage } from './PublicCatalogPages'

const queryClient = new QueryClient()

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <PublicCatalogPage /> },
      { path: '/products/:id', element: <PublicProductDetailPage /> },
      { path: '/login', element: <LoginPage /> },
      {
        path: '/admin',
        element: <ProtectedRoute><Outlet /></ProtectedRoute>,
        children: [
          { index: true, element: <Navigate to="products" replace /> },
          { path: 'products', element: <ProductListPage /> },
          { path: 'products/new', element: <ProductFormPage /> },
          { path: 'products/:id/edit', element: <ProductFormPage /> },
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
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </AuthProvider>
  )
}

function NotFoundPage() {
  const { t } = useTranslation()
  return <section className="welcome-panel"><h1>{t('notFound')}</h1></section>
}
