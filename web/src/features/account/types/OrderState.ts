export const orderStates = ['Reserved', 'Paid', 'Failed', 'Expired', 'Cancelled'] as const

export type OrderState = typeof orderStates[number]
