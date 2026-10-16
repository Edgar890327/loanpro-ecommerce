import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, apiRequest } from './api'
import { useAuth } from './auth-context'
import { validateProductForm } from './productValidation'

type Product = {
  id: string
  name: string
  sku: string
  description: string
  category: string
  price: number
  stock: number
  weightKg: number | null
  isDeleted: boolean
  createdAtUtc: string
  updatedAtUtc: string
  version: number
}

type ProductInput = Omit<Product, 'id' | 'isDeleted' | 'createdAtUtc' | 'updatedAtUtc' | 'version'> & {
  version?: number
}

type ProductPage = {
  items: Product[]
  page: number
  pageSize: number
  totalCount: number
}

type ProductErrors = Partial<Record<'name' | 'sku' | 'category' | 'price' | 'stock' | 'weightKg', string>>

export function ProductListPage() {
  const { t } = useTranslation()
  const { token } = useAuth()
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const products = useQuery({
    queryKey: ['products', page],
    queryFn: () => apiRequest<ProductPage>(`/api/admin/products?page=${page}&pageSize=10`, token),
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

export function ProductFormPage() {
  const { t } = useTranslation()
  const { token } = useAuth()
  const navigate = useNavigate()
  const { id } = useParams()
  const queryClient = useQueryClient()
  const existing = useQuery({
    queryKey: ['product', id],
    queryFn: () => apiRequest<Product>(`/api/admin/products/${id}`, token),
    enabled: Boolean(id),
  })
  const [values, setValues] = useState<ProductInput>({
    name: '',
    sku: '',
    description: '',
    category: '',
    price: 0,
    stock: 0,
    weightKg: null,
  })
  const [errors, setErrors] = useState<ProductErrors>({})
  const [formError, setFormError] = useState('')

  useEffect(() => {
    if (existing.data) {
      setValues({
        name: existing.data.name,
        sku: existing.data.sku,
        description: existing.data.description,
        category: existing.data.category,
        price: existing.data.price,
        stock: existing.data.stock,
        weightKg: existing.data.weightKg,
        version: existing.data.version,
      })
    }
  }, [existing.data])

  const saveProduct = useMutation({
    mutationFn: (input: ProductInput) => apiRequest<Product>(
      id ? `/api/admin/products/${id}` : '/api/admin/products',
      token,
      { method: id ? 'PUT' : 'POST', body: JSON.stringify(input) },
    ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['products'] })
      navigate('/admin/products')
    },
    onError: (error: Error) => {
      setFormError(error instanceof ApiError && error.status === 409
        ? error.detail?.includes('SKU')
          ? t('duplicateSku')
          : t('conflict')
        : t('requestFailed'))
    },
  })

  function updateField<K extends keyof ProductInput>(field: K, value: ProductInput[K]) {
    setValues((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined }))
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const validation = validateProductForm(values, {
      required: t('required'),
      positivePrice: t('positivePrice'),
      nonNegative: t('nonNegative'),
      invalidNumber: t('invalidNumber'),
      tooLong: t('tooLong'),
    })
    setErrors(validation)
    setFormError('')
    if (Object.keys(validation).length === 0) saveProduct.mutate(values)
  }

  if (id && existing.isLoading) return <p>{t('loading')}</p>
  if (id && existing.isError) return <p role="alert">{t('failedToLoad')}</p>

  return (
    <section className="panel form-panel">
      <h1>{id ? t('editProduct') : t('createProduct')}</h1>
      <form onSubmit={submit} noValidate>
        <TextField label={t('name')} value={values.name} maxLength={200} error={errors.name}
          onChange={(value) => updateField('name', value)} />
        <TextField label={t('sku')} value={values.sku} maxLength={64} error={errors.sku}
          onChange={(value) => updateField('sku', value)} />
        <TextField label={t('category')} value={values.category} maxLength={100} error={errors.category}
          onChange={(value) => updateField('category', value)} />
        <TextField label={t('description')} value={values.description} multiline
          onChange={(value) => updateField('description', value)} />
        <NumberField label={t('price')} value={values.price} error={errors.price}
          onChange={(value) => updateField('price', value === '' ? Number.NaN : value)} />
        <NumberField label={t('stock')} value={values.stock} error={errors.stock} integer
          onChange={(value) => updateField('stock', value === '' ? Number.NaN : value)} />
        <NumberField label={t('weight')} value={values.weightKg ?? ''} error={errors.weightKg} nullable precision={3}
          onChange={(value) => updateField('weightKg', value === '' ? null : value)} />
        {formError && <p role="alert" className="form-error">{formError}</p>}
        <div className="form-actions">
          <button type="submit" className="primary-button" disabled={saveProduct.isPending}>
            {t('save')}
          </button>
          <button type="button" className="secondary-button" onClick={() => navigate('/admin/products')}>
            {t('cancel')}
          </button>
        </div>
      </form>
    </section>
  )
}

function TextField({
  label,
  value,
  error,
  maxLength,
  multiline = false,
  onChange,
}: {
  label: string
  value: string
  error?: string
  maxLength?: number
  multiline?: boolean
  onChange: (value: string) => void
}) {
  return (
    <label>
      {label}
      {multiline
        ? <textarea value={value} maxLength={2000} onChange={(event) => onChange(event.target.value)} />
        : <input value={value} maxLength={maxLength} onChange={(event) => onChange(event.target.value)} />}
      {error && <span role="alert" className="field-error">{error}</span>}
    </label>
  )
}

function NumberField({
  label,
  value,
  error,
  integer = false,
  nullable = false,
  precision = 2,
  onChange,
}: {
  label: string
  value: number | ''
  error?: string
  integer?: boolean
  nullable?: boolean
  precision?: number
  onChange: (value: number | '') => void
}) {
  return (
    <label>
      {label}
      <input
        type="number"
        min="0"
        step={integer ? '1' : precision === 3 ? '0.001' : '0.01'}
        value={value}
        onChange={(event) => {
          if (nullable && event.target.value === '') onChange('')
          else onChange(event.target.value === '' ? Number.NaN : Number(event.target.value))
        }}
      />
      {error && <span role="alert" className="field-error">{error}</span>}
    </label>
  )
}

function getMutationError(error: Error, t: (key: string) => string) {
  if (error instanceof ApiError && error.status === 409) return t('conflict')
  return t('requestFailed')
}
