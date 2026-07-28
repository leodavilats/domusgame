import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, api } from './client'

interface ApiState<T> {
  data: T | null
  loading: boolean
  error: string | null
}

/**
 * Busca dados de uma rota GET. Substitui uma biblioteca de data-fetching:
 * o app tem poucas telas e nenhuma precisa de cache compartilhado.
 */
export function useApi<T>(path: string | null, deps: unknown[] = []): ApiState<T> & { reload: () => void } {
  const [state, setState] = useState<ApiState<T>>({ data: null, loading: path !== null, error: null })
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

    setState((previous) => ({ ...previous, loading: true, error: null }))

    api
      .get<T>(path)
      .then((data) => {
        if (mounted.current) setState({ data, loading: false, error: null })
      })
      .catch((error: unknown) => {
        if (!mounted.current) return
        const message = error instanceof ApiError ? error.message : 'Erro inesperado.'
        setState({ data: null, loading: false, error: message })
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [path, nonce, ...deps])

  const reload = useCallback(() => setNonce((value) => value + 1), [])

  return { ...state, reload }
}

/** Executa uma acao de escrita, expondo estado de carregamento e erro para a tela. */
export function useMutation<TArgs extends unknown[], TResult>(
  action: (...args: TArgs) => Promise<TResult>,
) {
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = useCallback(
    async (...args: TArgs): Promise<TResult | null> => {
      setLoading(true)
      setError(null)

      try {
        return await action(...args)
      } catch (caught: unknown) {
        setError(caught instanceof ApiError ? caught.message : 'Erro inesperado.')
        return null
      } finally {
        setLoading(false)
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  )

  return { run, loading, error, setError }
}
