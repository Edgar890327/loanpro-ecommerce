import { apiRequest } from '../../../shared/api/api'
import type { PaymentCapture, PaymentCreation } from './paymentTypes'

export function createPayment(orderId: string) {
  return apiRequest<PaymentCreation>(`/api/orders/${orderId}/payment`, {
    method: 'POST',
  })
}

export function capturePayment(orderId: string, externalReference: string) {
  return apiRequest<PaymentCapture>(`/api/orders/${orderId}/payment/capture`, {
    method: 'POST',
    body: JSON.stringify({ externalReference }),
  })
}
