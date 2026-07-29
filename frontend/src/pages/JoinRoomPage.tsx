import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { useApi, useMutation } from '../api/hooks'
import type { MyRoom } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { CheckIcon, KeyIcon } from '../components/Icons'
import { Button, Callout, Card, ErrorBox, Field, Input, PageTitle, Spinner } from '../components/ui'
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
    <div className="mx-auto max-w-md space-y-4">
      <PageTitle subtitle="Cada GC tem o seu código. Sem ele, a plataforma fica vazia.">
        Entrar na sala
      </PageTitle>

      {rooms.loading && <Spinner />}
      {rooms.error && <ErrorBox message={rooms.error} onRetry={rooms.reload} />}

      {mine && (
        <Card elevated className="animate-rise border-emerald-200">
          <div className="flex items-start gap-3">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-emerald-100 text-emerald-700">
              <CheckIcon className="h-6 w-6" />
            </span>
            <div className="min-w-0">
              <p className="font-semibold text-slate-900">Você está na sala {mine.name}</p>
              <p className="mt-0.5 text-sm text-slate-500">
                {pluralize(mine.memberCount, 'pessoa', 'pessoas')} · você entrou em{' '}
                {formatDate(mine.joinedAt)}
              </p>
            </div>
          </div>

          <Button size="lg" full className="mt-4" onClick={() => navigate('/')}>
            Ir para o desafio
          </Button>
        </Card>
      )}

      {!mine && !rooms.loading && (
        <Card elevated className="animate-rise">
          <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-brand-50 text-brand-600">
            <KeyIcon />
          </div>

          <form
            className="space-y-4"
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
                autoComplete="off"
                spellCheck={false}
                placeholder="DOMUS2026"
                className="text-center text-xl font-bold uppercase tracking-[0.2em]"
                value={inviteCode}
                onChange={(event) => setInviteCode(event.target.value.toUpperCase())}
              />
            </Field>

            <Button type="submit" size="lg" full loading={join.loading} disabled={inviteCode.trim().length < 4}>
              Entrar na sala
            </Button>
          </form>

          <div className="mt-4">
            <Callout tone="neutral">
              <span className="text-xs">
                Sua conta já está criada — entrar na sala só liga você ao conteúdo do GC. Você pode
                fazer isso a qualquer momento.
              </span>
            </Callout>
          </div>
        </Card>
      )}
    </div>
  )
}
