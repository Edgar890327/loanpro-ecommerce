import { createServer } from 'node:http'

const orders = new Map()
let nextOrder = 1

const server = createServer(async (request, response) => {
  const chunks = []
  for await (const chunk of request) chunks.push(chunk)
  const body = Buffer.concat(chunks).toString('utf8')
  const url = new URL(request.url ?? '/', 'http://localhost')
  response.setHeader('Content-Type', 'application/json')

  if (request.method === 'GET' && url.pathname === '/health') {
    response.writeHead(200).end(JSON.stringify({ status: 'ok' }))
    return
  }
  if (request.method === 'POST' && url.pathname === '/v1/oauth2/token') {
    response.writeHead(200).end(JSON.stringify({ access_token: 'e2e-stub-token', expires_in: 300 }))
    return
  }
  if (request.method === 'POST' && url.pathname === '/v2/checkout/orders') {
    const payload = JSON.parse(body)
    const id = `E2E-PAYPAL-${nextOrder++}`
    orders.set(id, payload.purchase_units[0])
    response.writeHead(201).end(JSON.stringify({ id, status: 'CREATED' }))
    return
  }

  const match = url.pathname.match(/^\/v2\/checkout\/orders\/([^/]+)(?:\/capture)?$/)
  if (match) {
    const id = decodeURIComponent(match[1])
    const order = orders.get(id)
    if (!order) {
      response.writeHead(404).end(JSON.stringify({ name: 'RESOURCE_NOT_FOUND' }))
      return
    }
    if (url.pathname.endsWith('/capture')) {
      const capture = {
        id: `${id}-CAPTURE`,
        status: 'COMPLETED',
        amount: order.amount,
      }
      const captured = {
        reference_id: order.reference_id,
        amount: order.amount,
        payments: { captures: [capture] },
      }
      orders.set(id, captured)
      response.writeHead(201).end(JSON.stringify({
        id,
        status: 'COMPLETED',
        purchase_units: [{ ...order, payments: captured.payments }],
      }))
      return
    }
    response.writeHead(200).end(JSON.stringify({ id, status: 'COMPLETED', purchase_units: [order] }))
    return
  }

  response.writeHead(404).end(JSON.stringify({ name: 'NOT_FOUND' }))
})

server.listen(8081, '0.0.0.0')
