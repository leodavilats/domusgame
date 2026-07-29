import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { useApi, useMutation } from '../api/hooks'
import type { MyRoom } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Button, Card, ErrorBox, Field, Input, PageTitle, Spinner } from '../components/ui'
import { formatDate, pluralize } from '../lib/format'

export function JoinRoomPage() {
  const navigate = useNavigate()
  const { refresh } = useSession()
  const rooms = useApi<MyRoom[]>('/api/rooms/mine')

  const [inviteCode, setInviteCode] = useState('')

  const join = useMutation(async () => {
    const room = await api.post<MyRoom>('/api/rooms/join', { inviteCode })
    await refresh()
    navigate('/', { replace: true })
    return room
  })

  const mine = rooms.data?.[0] ?? null

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Cada GC tem o seu código. Sem ele, a plataforma fica vazia.">
        Entrar na sala
      </PageTitle>

      {rooms.loading && <Spinner />}
      {rooms.error && <ErrorBox message={rooms.error} onRetry={rooms.reload} />}

      {mine && (
        <Card className="border-emerald-200 bg-emerald-50">
          <p className="text-sm font-semibold text-emerald-900">Você está na sala {mine.name}</p>
          <p className="mt-1 text-sm text-emerald-800">
            {pluralize(mine.memberCount, 'pessoa', 'pessoas')} · você entrou em {formatDate(mine.joinedAt)}
          </p>
          <div className="mt-3">
            <Button variant="secondary" onClick={() => navigate('/')}>
              Ir para o desafio
            </Button>
          </div>
        </Card>
      )}

      {!mine && !rooms.loading && (
        <Card>
          <form
            className="space-y-3"
            onSubmit={(event) => {
              event.preventDefault()
              void join.run()
            }}
          >
            {join.error ? <ErrorBox message={join.error} /> : null}

            <Field label="Código da sala" hint="Peça o código ao líder do seu GC.">
              <Input
                required
                autoFocus
                maxLength={20}
                autoCapitalize="characters"
                placeholder="DOMUS2026"
                className="text-center text-lg font-bold uppercase tracking-widest"
                value={inviteCode}
                onChange={(event) => setInviteCode(event.target.value)}
              />
            </Field>

            <Button type="submit" full loading={join.loading}>
              Entrar na sala
            </Button>
          </form>
        </Card>
      )}
    </div>
  )
}
