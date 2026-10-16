import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, Navigate, Outlet, RouterProvider } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AuthProvider, ProtectedRoute } from './auth'
import { Layout } from './Layout'
import { LoginPage } from './LoginPage'
import { ProductFormPage, ProductListPage } from './ProductPages'

const queryClient = new QueryClient()

function HomePage() {
  const { t } = useTranslation()
  return (
    <section className="welcome-panel">
      <p className="eyebrow">{t('welcomeEyebrow')}</p>
      <h1>{t('welcomeTitle')}</h1>
      <p>{t('welcomeBody')}</p>
    </section>
  )
}

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <HomePage /> },
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
