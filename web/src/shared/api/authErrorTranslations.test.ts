import { describe, expect, it } from 'vitest'
import { ApiError, translateApiError } from './api'
import i18n from '../i18n/i18n'

const authErrorCodes = ['INVALID_CREDENTIALS', 'EMAIL_TAKEN', 'RATE_LIMITED', 'FORBIDDEN', 'SESSION_EXPIRED']

describe('auth error translations', () => {
  it.each(['en-US', 'es'])('translates every auth error code in %s', async (language) => {
    await i18n.changeLanguage(language)
    const fallback = i18n.t('requestFailed')

    for (const code of authErrorCodes) {
      const message = translateApiError(new ApiError(400, 'detail', 'title', code), i18n.t.bind(i18n))
      expect(message).not.toBe(`serverError_${code}`)
      expect(message).not.toBe(fallback)
      expect(message.length).toBeGreaterThan(0)
    }
  })

  it('keeps the Spanish and English messages different', async () => {
    const translate = (language: string) => i18n.getFixedT(language)(`serverError_INVALID_CREDENTIALS`)

    expect(translate('es')).not.toBe(translate('en-US'))
  })
})
