export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }

  get isUnauthorized() {
    return this.status === 401
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  let response: Response

  try {
    response = await fetch(path, {
      method,
      credentials: 'same-origin',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'Sem conexao com o servidor. Verifique sua internet.')
  }

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()

  if (!response.ok) {
    let message = 'Não foi possivel completar a operação.'

    if (text) {
      try {
        const problem = JSON.parse(text) as ProblemDetails
        message = problem.detail || problem.title || message
      } catch {
        message = text
      }
    }

    if (response.status === 401) message = 'Sua sessão expirou. Entre novamente.'
    if (response.status === 429) message = 'Muitas tentativas seguidas. Aguarde um minuto.'

    throw new ApiError(response.status, message)
  }

  return text ? (JSON.parse(text) as T) : (undefined as T)
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  del: <T>(path: string, body?: unknown) => request<T>('DELETE', path, body),
}
