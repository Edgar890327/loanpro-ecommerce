import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, apiRequest } from '../../../shared/api/api'
import { useAuth } from '../../../shared/auth/auth-context'
import type { ProductPageData } from '../types/ProductPageData'

export function ProductListPage() {
  const { t } = useTranslation()
  const { token } = useAuth()
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const products = useQuery({
    queryKey: ['products', page],
    queryFn: () => apiRequest<ProductPageData>(`/api/admin/products?page=${page}&pageSize=10`, token),
  })
  const removeProduct = useMutation({
    mutationFn: (id: string) => apiRequest<void>(`/api/admin/products/${id}`, token, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['products'] }),
  })
  const pages = Math.max(1, Math.ceil((products.data?.totalCount ?? 0) / 10))

  return (
    <section className="panel">
      <div className="page-heading">
        <h1>{t('productsTitle')}</h1>
        <div className="page-heading-actions">
          <Link className="secondary-button" to="/admin/products/import">{t('importProducts')}</Link>
          <Link className="primary-button" to="/admin/products/new">{t('addProduct')}</Link>
        </div>
      </div>
      {products.isLoading && <p>{t('loading')}</p>}
      {products.isError && <p role="alert">{t('failedToLoad')}</p>}
      {removeProduct.isError && <p role="alert">{getMutationError(removeProduct.error, t)}</p>}
      {products.data?.items.length === 0 && <p>{t('noProducts')}</p>}
      {products.data && products.data.items.length > 0 && (
        <>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>{t('name')}</th>
                  <th>{t('sku')}</th>
                  <th>{t('category')}</th>
                  <th>{t('price')}</th>
                  <th>{t('stock')}</th>
                  <th>{t('actions')}</th>
                </tr>
              </thead>
              <tbody>
                {products.data.items.map((product) => (
                  <tr key={product.id}>
                    <td>{product.name}</td>
                    <td>{product.sku}</td>
                    <td>{product.category}</td>
                    <td>{product.price.toFixed(2)}</td>
                    <td>{product.stock}</td>
                    <td className="row-actions">
                      <Link to={`/admin/products/${product.id}/edit`}>{t('edit')}</Link>
                      <button
                        type="button"
                        className="text-button danger-text"
                        onClick={() => {
                          if (window.confirm(t('deleteConfirmation'))) removeProduct.mutate(product.id)
                        }}
                      >
                        {t('delete')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="pagination">
            <button type="button" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
              {t('previous')}
            </button>
            <span>{t('page', { page, pages })}</span>
            <button type="button" disabled={page >= pages} onClick={() => setPage((current) => current + 1)}>
              {t('next')}
            </button>
          </div>
        </>
      )}
    </section>
  )
}

function getMutationError(error: Error, t: (key: string) => string) {
  if (error instanceof ApiError && error.status === 409) return t('conflict')
  return t('requestFailed')
}
