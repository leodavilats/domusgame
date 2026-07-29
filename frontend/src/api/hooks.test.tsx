import { useState } from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { invalidateCache, useApi, useMutation } from './hooks'

// Sem `globals: true`, o cleanup automatico do Testing Library nao e registrado.
afterEach(cleanup)

/**
 * Regressao: `run` chegou a ser memoizado com lista de dependencias vazia, o que congelava
 * o closure da primeira renderizacao. Na pratica, TODO formulario do app enviava os valores
 * iniciais dos campos (vazios) em vez do que a pessoa tinha digitado - e o servidor respondia
 * "E-mail ou senha invalidos" sem nenhuma pista do motivo.
 *
 * Os testes de API nao pegam isso: o defeito esta no lado do navegador.
 */
function FormularioDeTeste({ onSubmit }: { onSubmit: (value: string) => void }) {
  const [valor, setValor] = useState('')

  const mutation = useMutation(async () => {
    onSubmit(valor)
    return valor
  })

  return (
    <>
      <input aria-label="campo" value={valor} onChange={(event) => setValor(event.target.value)} />
      <button type="button" onClick={() => void mutation.run()}>
        enviar
      </button>
    </>
  )
}

describe('useMutation', () => {
  it('envia o valor atual do formulario, nao o da primeira renderizacao', async () => {
    const enviados: string[] = []
    render(<FormularioDeTeste onSubmit={(value) => enviados.push(value)} />)

    fireEvent.change(screen.getByLabelText('campo'), { target: { value: 'senha-digitada' } })
    fireEvent.click(screen.getByText('enviar'))

    await waitFor(() => expect(enviados).toEqual(['senha-digitada']))
  })

  it('mantem a identidade de run estavel entre renderizacoes', async () => {
    const identidades = new Set<unknown>()

    function Componente() {
      const [contador, setContador] = useState(0)
      const mutation = useMutation(async () => contador)
      identidades.add(mutation.run)

      return (
        <button type="button" onClick={() => setContador((value) => value + 1)}>
          incrementar
        </button>
      )
    }

    render(<Componente />)
    fireEvent.click(screen.getByText('incrementar'))
    fireEvent.click(screen.getByText('incrementar'))

    await waitFor(() => expect(identidades.size).toBe(1))
  })

  it('captura o erro e expoe a mensagem em vez de propagar', async () => {
    function Componente() {
      const mutation = useMutation(async () => {
        throw new Error('falhou')
      })

      return (
        <>
          <button type="button" onClick={() => void mutation.run()}>
            enviar
          </button>
          <span>{mutation.error ?? 'sem erro'}</span>
        </>
      )
    }

    render(<Componente />)
    fireEvent.click(screen.getByText('enviar'))

    await waitFor(() => expect(screen.getByText('Erro inesperado.')).toBeTruthy())
  })
})

/**
 * O cache existe para que voltar a uma tela ja visitada nao mostre spinner de novo.
 * Sem ele, cada troca de aba parecia travamento em conexao ruim.
 */
describe('useApi', () => {
  const originalFetch = globalThis.fetch

  afterEach(() => {
    globalThis.fetch = originalFetch
    invalidateCache()
  })

  function Tela() {
    const { data, loading } = useApi<{ valor: string }>('/api/teste')
    return <span>{loading ? 'carregando' : (data?.valor ?? 'vazio')}</span>
  }

  it('na segunda visita mostra o dado em cache sem passar por carregando', async () => {
    let chamadas = 0

    globalThis.fetch = (async () => {
      chamadas++
      return new Response(JSON.stringify({ valor: 'do servidor' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    }) as typeof fetch

    const primeira = render(<Tela />)
    expect(screen.getByText('carregando')).toBeTruthy()
    await waitFor(() => expect(screen.getByText('do servidor')).toBeTruthy())
    primeira.unmount()

    // Segunda montagem: o valor precisa aparecer de imediato, sem estado de carregando.
    render(<Tela />)
    expect(screen.getByText('do servidor')).toBeTruthy()

    // E ainda assim revalida por trás.
    await waitFor(() => expect(chamadas).toBe(2))
  })
})
