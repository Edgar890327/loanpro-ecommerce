import { useCallback, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { NotificationContext } from './notification-context'
import type { NotificationKind } from './notification-context'

type Notice = { id: number; message: string; kind: NotificationKind }

export function NotificationProvider({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const [notices, setNotices] = useState<Notice[]>([])
  const notify = useCallback((message: string, kind: Notice['kind'] = 'success') => {
    const id = Date.now() + Math.random()
    setNotices((current) => [...current, { id, message, kind }])
    window.setTimeout(() => {
      setNotices((current) => current.filter((notice) => notice.id !== id))
    }, 5000)
  }, [])
  const context = useMemo(() => ({ notify }), [notify])

  return (
    <NotificationContext.Provider value={context}>
      {children}
      <div className="notification-region" aria-label={t('notifications')} aria-live="polite" aria-atomic="false">
        {notices.map((notice) => (
          <div key={notice.id} className={`notification notification-${notice.kind}`} role={notice.kind === 'error' ? 'alert' : 'status'}>
            <span>{notice.message}</span>
            <button type="button" aria-label={t('dismissNotification')} onClick={() => {
              setNotices((current) => current.filter((item) => item.id !== notice.id))
            }}>×</button>
          </div>
        ))}
      </div>
    </NotificationContext.Provider>
  )
}
