import { useState } from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { invalidateCache, useApi, useMutation } from './hooks'

afterEach(cleanup)

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

    render(<Tela />)
    expect(screen.getByText('do servidor')).toBeTruthy()

    await waitFor(() => expect(chamadas).toBe(2))
  })
})
