import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiRequest } from '../../../shared/api/api'
import { useAuth } from '../../../shared/auth/auth-context'
import type { PaymentProviderSetting } from '../types/PaymentProviderSetting'

type Draft = {
  publicConfiguration: Record<string, string>
  secrets: Record<string, string>
}

const noSettings: PaymentProviderSetting[] = []

export function PaymentProviderSettingsPage() {
  const { t } = useTranslation()
  const { token } = useAuth()
  const queryClient = useQueryClient()
  const [selectedKey, setSelectedKey] = useState('')
  const [draft, setDraft] = useState<Draft>({ publicConfiguration: {}, secrets: {} })
  const settings = useQuery({
    queryKey: ['payment-provider-settings'],
    queryFn: () => apiRequest<PaymentProviderSetting[]>('/api/admin/payment-providers', token),
  })
  const save = useMutation({
    mutationFn: (activate: boolean) => apiRequest<PaymentProviderSetting>(
      `/api/admin/payment-providers/${selectedKey}`,
      token,
      {
        method: 'PUT',
        body: JSON.stringify({ ...draft, activate }),
      },
    ),
    onSuccess: (updated) => {
      queryClient.setQueryData<PaymentProviderSetting[]>(
        ['payment-provider-settings'],
        (current) => current?.map((item) => ({
          ...item,
          ...(item.key === updated.key ? updated : { isActive: updated.isActive ? false : item.isActive }),
        })),
      )
      setDraft({
        publicConfiguration: updated.publicConfiguration,
        secrets: {},
      })
    },
  })

  const providers = settings.data ?? noSettings
  const selected = providers.find((item) => item.key === selectedKey) ?? providers[0]

  useEffect(() => {
    if (!selectedKey && providers[0]) {
      setSelectedKey(providers[0].key)
      setDraft({ publicConfiguration: providers[0].publicConfiguration, secrets: {} })
    }
  }, [providers, selectedKey])

  function selectProvider(provider: PaymentProviderSetting) {
    setSelectedKey(provider.key)
    setDraft({ publicConfiguration: provider.publicConfiguration, secrets: {} })
    save.reset()
  }

  function updateField(key: string, value: string, secret: boolean) {
    setDraft((current) => secret
      ? { ...current, secrets: { ...current.secrets, [key]: value } }
      : { ...current, publicConfiguration: { ...current.publicConfiguration, [key]: value } })
  }

  if (settings.isLoading) return <p role="status">{t('loading')}</p>
  if (settings.isError) return <section className="panel"><p role="alert">{t('paymentSettingsLoadFailed')}</p></section>

  return (
    <section className="panel payment-settings">
      <h1>{t('paymentSettingsTitle')}</h1>
      <label>
        {t('paymentProvider')}
        <select
          value={selected?.key ?? ''}
          onChange={(event) => {
            const provider = providers.find((item) => item.key === event.target.value)
            if (provider) selectProvider(provider)
          }}
        >
          {providers.map((provider) => (
            <option key={provider.key} value={provider.key}>
              {provider.name}{provider.isActive ? ` — ${t('active')}` : ''}
            </option>
          ))}
        </select>
      </label>
      {selected && (
        <form onSubmit={(event) => { event.preventDefault(); save.mutate(false) }}>
          {selected.configurationFields.map((field) => {
            const value = field.secret
              ? draft.secrets[field.key] ?? ''
              : draft.publicConfiguration[field.key] ?? ''
            const fieldLabel = t(`paymentField_${field.key}`, { defaultValue: field.label })
            const label = field.secret ? `${fieldLabel} (${t('secretNotShown')})` : fieldLabel
            return (
              <label key={field.key}>
                {label}
                {field.type === 'select'
                  ? <select
                      required={field.required}
                      value={value}
                      onChange={(event) => updateField(field.key, event.target.value, field.secret)}
                    >
                      {(field.options ?? []).map((option) => (
                        <option key={option} value={option}>
                          {t(`paymentMode_${option}`, { defaultValue: option })}
                        </option>
                      ))}
                    </select>
                  : field.type === 'boolean'
                    ? <input
                        type="checkbox"
                        checked={value === 'true'}
                        onChange={(event) => updateField(field.key, String(event.target.checked), field.secret)}
                      />
                    : <input
                        type={field.secret ? 'password' : field.type === 'number' ? 'number' : 'text'}
                        required={field.required && !field.secret}
                        value={value}
                        autoComplete={field.secret ? 'new-password' : undefined}
                        onChange={(event) => updateField(field.key, event.target.value, field.secret)}
                      />}
              </label>
            )
          })}
          {save.isError && <p role="alert">{t('paymentSettingsSaveFailed')}</p>}
          {save.isSuccess && <p role="status">{t('paymentSettingsSaved')}</p>}
          <div className="form-actions">
            <button type="submit" disabled={save.isPending}>{t('savePaymentSettings')}</button>
            {!selected.isActive && (
              <button type="button" disabled={save.isPending} onClick={() => save.mutate(true)}>
                {t('saveAndActivateProvider')}
              </button>
            )}
          </div>
        </form>
      )}
    </section>
  )
}
