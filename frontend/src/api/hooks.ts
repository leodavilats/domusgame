import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, api } from './client'

interface ApiState<T> {
  data: T | null
  loading: boolean
  error: string | null
}

const cache = new Map<string, unknown>()

export function invalidateCache(pathFragment?: string) {
  if (!pathFragment) {
    cache.clear()
    return
  }

  for (const key of [...cache.keys()]) {
    if (key.includes(pathFragment)) cache.delete(key)
  }
}

export function useApi<T>(path: string | null, deps: unknown[] = []): ApiState<T> & { reload: () => void } {
  const cached = path === null ? undefined : (cache.get(path) as T | undefined)

  const [state, setState] = useState<ApiState<T>>({
    data: cached ?? null,
    loading: path !== null && cached === undefined,
    error: null,
  })

  const [nonce, setNonce] = useState(0)
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  useEffect(() => {
    if (path === null) {
      setState({ data: null, loading: false, error: null })
      return
    }

    const known = cache.get(path) as T | undefined

    setState((previous) => ({
      data: known ?? previous.data,
      loading: known === undefined,
      error: null,
    }))

    api
      .get<T>(path)
      .then((data) => {
        cache.set(path, data)
        if (mounted.current) setState({ data, loading: false, error: null })
      })
      .catch((error: unknown) => {
        if (!mounted.current) return

        const message = error instanceof ApiError ? error.message : 'Erro inesperado.'

        setState((previous) => ({
          data: previous.data,
          loading: false,
          error: previous.data === null ? message : null,
        }))
      })
  }, [path, nonce, ...deps])

  const reload = useCallback(() => {
    if (path !== null) cache.delete(path)
    setNonce((value) => value + 1)
  }, [path])

  return { ...state, reload }
}

export function useMutation<TArgs extends unknown[], TResult>(
  action: (...args: TArgs) => Promise<TResult>,
) {
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const latestAction = useRef(action)
  latestAction.current = action

  const run = useCallback(async (...args: TArgs): Promise<TResult | null> => {
    setLoading(true)
    setError(null)

    try {
      const result = await latestAction.current(...args)

      invalidateCache()

      return result
    } catch (caught: unknown) {
      setError(caught instanceof ApiError ? caught.message : 'Erro inesperado.')
      return null
    } finally {
      setLoading(false)
    }
  }, [])

  return { run, loading, error, setError }
}
