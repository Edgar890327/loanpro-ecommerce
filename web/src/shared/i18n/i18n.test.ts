import { describe, expect, it } from 'vitest'
import { resources } from './i18n'

const sourceFiles = import.meta.glob('../../**/*.{ts,tsx}', {
  eager: true,
  query: '?raw',
  import: 'default',
}) as Record<string, string>

const applicationSource = Object.entries(sourceFiles)
  .filter(([path]) => !path.includes('.test.') && !path.endsWith('/shared/i18n/i18n.ts'))
  .map(([, source]) => source)
  .join('\n')

describe('UI translations', () => {
  it('contains the same translation keys in both supported languages', () => {
    expect(Object.keys(resources.es.translation).sort()).toEqual(
      Object.keys(resources['en-US'].translation).sort(),
    )
  })

  it('uses every translation key in the application', () => {
    const availableKeys = Object.keys(resources['en-US'].translation)
    const availableKeySet = new Set(availableKeys)
    const usedKeys = new Set<string>()
    const stringKeys = /['"]([A-Za-z][A-Za-z0-9_]*)['"]/g
    for (const match of applicationSource.matchAll(stringKeys)) {
      if (availableKeySet.has(match[1])) usedKeys.add(match[1])
    }
    const staticTranslationCalls = /\b(?:translate|t)\(\s*['"]([A-Za-z][A-Za-z0-9_]*)['"]/g
    const unknownKeys = [...applicationSource.matchAll(staticTranslationCalls)]
      .map((match) => match[1])
      .filter((key) => !availableKeySet.has(key))
    expect(unknownKeys).toEqual([])
    if (applicationSource.includes('`orderState')) {
      availableKeys.filter((key) => key.startsWith('orderState')).forEach((key) => usedKeys.add(key))
    }
    if (applicationSource.includes('`settingName_')) {
      availableKeys.filter((key) => key.startsWith('settingName_')).forEach((key) => usedKeys.add(key))
    }
    if (applicationSource.includes('`logLevel_')) {
      availableKeys.filter((key) => key.startsWith('logLevel_')).forEach((key) => usedKeys.add(key))
    }
    if (applicationSource.includes('`paymentField_')) {
      availableKeys.filter((key) => key.startsWith('paymentField_')).forEach((key) => usedKeys.add(key))
    }
    if (applicationSource.includes('`paymentMode_')) {
      availableKeys.filter((key) => key.startsWith('paymentMode_')).forEach((key) => usedKeys.add(key))
    }
    if (applicationSource.includes('`serverError_')) {
      availableKeys.filter((key) => key.startsWith('serverError_')).forEach((key) => usedKeys.add(key))
    }

    expect(availableKeys.filter((key) => !usedKeys.has(key))).toEqual([])
    expect([...usedKeys].filter((key) => !availableKeys.includes(key))).toEqual([])
  })
})
