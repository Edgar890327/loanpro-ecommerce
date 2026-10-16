import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiRequest } from '../../../shared/api/api'
import { useAuth } from '../../../shared/auth/auth-context'
import type { StoreSettings, SettingSchema } from '../types/StoreSettings'

type Draft = {
  publicConfiguration: Record<string, string>
  secrets: Record<string, string>
}

export function SettingsPage() {
  const { t } = useTranslation()
  const { token } = useAuth()
  const settings = useQuery({
    queryKey: ['store-settings'],
    queryFn: () => apiRequest<StoreSettings>('/api/admin/settings', token),
  })

  if (settings.isLoading) return <p role="status">{t('loading')}</p>
  if (settings.isError) return <section className="panel"><p role="alert">{t('settingsLoadFailed')}</p></section>
  if (!settings.data) return <section className="panel"><p role="alert">{t('settingsLoadFailed')}</p></section>

  return (
    <div className="settings-page">
      <h1>{t('settingsTitle')}</h1>
      <section className="panel">
        <h2>{t('paymentSettingsTitle')}</h2>
        {settings.data.paymentProviders.map((setting) => (
          <SettingsEditor
            key={`payment-${setting.key}`}
            setting={setting}
            endpoint={`/api/admin/payment-providers/${setting.key}`}
            kind="payment"
          />
        ))}
      </section>
      <section className="panel">
        <h2>{t('shippingSettingsTitle')}</h2>
        {settings.data.shippingStrategies.map((setting) => (
          <SettingsEditor
            key={`shipping-${setting.key}`}
            setting={setting}
            endpoint={`/api/admin/settings/shipping/${setting.key}`}
            kind="shipping"
          />
        ))}
      </section>
    </div>
  )
}

function SettingsEditor({
  setting,
  endpoint,
  kind,
}: {
  setting: SettingSchema
  endpoint: string
  kind: 'payment' | 'shipping'
}) {
  const { t } = useTranslation()
  const { token } = useAuth()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<Draft>({
    publicConfiguration: setting.publicConfiguration,
    secrets: {},
  })
  const [enabled, setEnabled] = useState(setting.isEnabled)
  const save = useMutation({
    mutationFn: (activate: boolean) => apiRequest<SettingSchema>(endpoint, token, {
      method: 'PUT',
      body: JSON.stringify({ ...draft, enabled, activate }),
    }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['store-settings'] })
    },
  })

  useEffect(() => {
    setDraft({ publicConfiguration: setting.publicConfiguration, secrets: {} })
    setEnabled(setting.isEnabled)
  }, [setting.key, setting.publicConfiguration, setting.isEnabled])

  function updateField(key: string, value: string, secret: boolean) {
    setDraft((current) => secret
      ? { ...current, secrets: { ...current.secrets, [key]: value } }
      : { ...current, publicConfiguration: { ...current.publicConfiguration, [key]: value } })
  }

  return (
    <form className="settings-editor" onSubmit={(event) => {
      event.preventDefault()
      if (window.confirm(t('confirmSaveSettings'))) save.mutate(false)
    }}>
      <h3>{t(`settingName_${setting.key}`, { defaultValue: setting.name })}{setting.isActive ? ` — ${t('active')}` : ''}</h3>
      {setting.configurationFields.map((field) => (
        <SettingField
          key={field.key}
          field={field}
          value={field.secret
            ? draft.secrets[field.key] ?? ''
            : draft.publicConfiguration[field.key] ?? ''}
          secretConfigured={Boolean(setting.maskedSecrets[field.key])}
          onChange={(value) => updateField(field.key, value, field.secret)}
        />
      ))}
      <label>
        <input
          type="checkbox"
          checked={enabled}
          disabled={setting.isActive}
          onChange={(event) => setEnabled(event.target.checked)}
        />
        {t('settingEnabled')}
      </label>
      {save.isError && <p role="alert">{t('settingsSaveFailed')}</p>}
      {save.isSuccess && <p role="status">{t('settingsSaved')}</p>}
      <div className="form-actions">
        <button type="submit" disabled={save.isPending}>{t('savePaymentSettings')}</button>
        {!setting.isActive && enabled && (
          <button
            type="button"
            disabled={save.isPending}
            onClick={() => {
              if (window.confirm(t(kind === 'payment' ? 'confirmActivateProvider' : 'confirmActivateShipping'))) {
                save.mutate(true)
              }
            }}
          >
            {t(kind === 'payment' ? 'saveAndActivateProvider' : 'saveAndActivateShipping')}
          </button>
        )}
      </div>
    </form>
  )
}

function SettingField({
  field,
  value,
  secretConfigured,
  onChange,
}: {
  field: SettingSchema['configurationFields'][number]
  value: string
  secretConfigured: boolean
  onChange: (value: string) => void
}) {
  const { t } = useTranslation()
  const translatedLabel = t(`paymentField_${field.key}`, { defaultValue: field.label })
  const label = field.secret && secretConfigured
    ? `${translatedLabel} (${t('secretNotShown')})`
    : translatedLabel
  return (
    <label>
      {label}
      {field.type === 'select'
        ? <select
            required={field.required}
            value={value}
            onChange={(event) => onChange(event.target.value)}
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
              onChange={(event) => onChange(String(event.target.checked))}
            />
          : <input
              type={field.secret ? 'password' : field.type === 'number' ? 'number' : 'text'}
              required={field.required && !field.secret && !value}
              min={field.type === 'number' ? field.minimum ?? undefined : undefined}
              max={field.type === 'number' ? field.maximum ?? undefined : undefined}
              value={value}
              autoComplete={field.secret ? 'new-password' : undefined}
              onChange={(event) => onChange(event.target.value)}
            />}
    </label>
  )
}
