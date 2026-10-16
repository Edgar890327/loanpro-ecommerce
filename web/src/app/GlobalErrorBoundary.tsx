import { Component } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import i18n from '../shared/i18n/i18n'

type Props = { children: ReactNode }
type State = { hasError: boolean }

export class GlobalErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false }

  static getDerivedStateFromError(): State {
    return { hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled UI error', error, info.componentStack)
  }

  render() {
    if (this.state.hasError) {
      return (
        <main className="panel global-error" role="alert">
          <h1>{i18n.t('uiErrorTitle')}</h1>
          <p>{i18n.t('uiErrorBody')}</p>
          <button type="button" onClick={() => window.location.reload()}>{i18n.t('retry')}</button>
        </main>
      )
    }
    return this.props.children
  }
}
