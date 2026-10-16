import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { Layout } from './Layout'

const queryClient = new QueryClient()
const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      {
        path: '/',
        element: (
          <section className="welcome-panel">
            <p className="eyebrow">E-commerce starter</p>
            <h1>Welcome</h1>
            <p>The storefront foundation is ready.</p>
          </section>
        ),
      },
      {
        path: '*',
        element: (
          <section className="welcome-panel">
            <h1>Page not found</h1>
          </section>
        ),
      },
    ],
  },
])

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}
