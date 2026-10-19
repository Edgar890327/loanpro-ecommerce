const healthUrl = new URL('/health', process.env.E2E_BASE_URL)

for (let attempt = 0; attempt < 60; attempt += 1) {
  try {
    const response = await fetch(healthUrl)
    if (response.ok) process.exit(0)
  } catch {}

  await new Promise((resolve) => setTimeout(resolve, 1000))
}

throw new Error(`The application did not become healthy at ${healthUrl}`)
