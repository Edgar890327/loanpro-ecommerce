import { createContext } from 'react'

export type NotificationKind = 'success' | 'error'
export type NotificationContextValue = {
  notify: (message: string, kind?: NotificationKind) => void
}

export const NotificationContext = createContext<NotificationContextValue | null>(null)
