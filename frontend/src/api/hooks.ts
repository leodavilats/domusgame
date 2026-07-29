import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, api } from './client'

interface ApiState<T> {
  data: T | null
  loading: boolean
  error: string | null
}

/**
 * Cache em memória por rota, com estratégia *stale-while-revalidate*: ao voltar para uma tela
 * já visitada, o dado antigo aparece na hora e a atualização acontece por trás.
 *
 * Sem isso, trocar de aba mostra um spinner em cada navegação — o que, num celular com
 * conexão ruim, faz o app parecer travado mesmo estando correto.
 *
 * Vive no módulo de propósito: é cache de sessão, não estado persistido. Um reload limpa.
 */
const cache = new Map<string, unknown>()

/** Invalida rotas cujo caminho contenha o trecho informado (após uma escrita, por exemplo). */
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
    // Com dado em cache não há espera: a tela pinta e revalida em silêncio.
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

        // Havendo dado em cache, mantemos a tela útil e não trocamos conteúdo por erro.
        setState((previous) => ({
          data: previous.data,
          loading: false,
          error: previous.data === null ? message : null,
        }))
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [path, nonce, ...deps])

  const reload = useCallback(() => {
    if (path !== null) cache.delete(path)
    setNonce((value) => value + 1)
  }, [path])

  return { ...state, reload }
}

/** Executa uma ação de escrita, expondo estado de carregamento e erro para a tela. */
export function useMutation<TArgs extends unknown[], TResult>(
  action: (...args: TArgs) => Promise<TResult>,
) {
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // `action` é recriada a cada render e fecha sobre o estado atual do formulário.
  // Guardamos sempre a versão mais recente numa ref: assim `run` mantém identidade estável
  // (não invalida memos de quem o recebe) sem nunca executar um closure velho.
  //
  // Um useCallback com lista de dependências vazia aqui congelaria a primeira renderização
  // e enviaria os valores iniciais dos campos - vazios - em todo formulário do app.
  const latestAction = useRef(action)
  latestAction.current = action

  const run = useCallback(async (...args: TArgs): Promise<TResult | null> => {
    setLoading(true)
    setError(null)

    try {
      const result = await latestAction.current(...args)

      // Uma escrita torna qualquer leitura em cache suspeita.
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
