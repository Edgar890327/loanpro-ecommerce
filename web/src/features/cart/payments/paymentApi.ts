import { apiRequest } from '../../../shared/api/api'
import type { PaymentCapture, PaymentCreation } from './paymentTypes'

export function createPayment(orderId: string, capability: string) {
  return apiRequest<PaymentCreation>(`/api/orders/${orderId}/payment`, {
    method: 'POST',
    headers: { 'Idempotency-Key': capability },
  })
}

export function capturePayment(orderId: string, capability: string, externalReference: string) {
  return apiRequest<PaymentCapture>(`/api/orders/${orderId}/payment/capture`, {
    method: 'POST',
    headers: { 'Idempotency-Key': capability },
    body: JSON.stringify({ externalReference }),
  })
}
