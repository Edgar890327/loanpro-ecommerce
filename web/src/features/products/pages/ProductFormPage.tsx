import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, apiRequest } from '../../../shared/api/api'
import { useAuth } from '../../../shared/auth/auth-context'
import { validateProductForm } from '../../../shared/validation/productValidation'
import { NumberField } from '../../../components/molecules/NumberField'
import { TextField } from '../../../components/molecules/TextField'
import type { Product } from '../types/Product'
import type { ProductInput } from '../types/ProductInput'

type ProductErrors = Partial<Record<'name' | 'sku' | 'category' | 'price' | 'stock' | 'weightKg', string>>

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
          : error.title === 'Stock is reserved'
            ? t('reservedStockConflict')
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
