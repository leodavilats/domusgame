import { useState } from 'react'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminParticipant, Invite } from '../../api/types'
import {
  Avatar,
  Badge,
  Button,
  Callout,
  Card,
  ErrorBox,
  Field,
  Input,
  PageTitle,
  SectionTitle,
  SkeletonCard,
} from '../../components/ui'
import { formatDate } from '../../lib/format'
import { share } from '../../lib/share'

export function AdminParticipantsPage() {
  const invite = useApi<Invite>('/api/admin/invite')
  const participants = useApi<AdminParticipant[]>('/api/admin/participants')

  const [rotating, setRotating] = useState(false)
  const [customCode, setCustomCode] = useState('')
  const [feedback, setFeedback] = useState<string | null>(null)

  const rotate = useMutation(async () => {
    await api.post<Invite>('/api/admin/invite', {
      code: customCode.trim() === '' ? null : customCode.trim(),
    })
    setCustomCode('')
    setRotating(false)
    setFeedback('Código trocado. O anterior deixou de funcionar.')
    invite.reload()
  })

  const changeRole = useMutation(async (id: string, role: 'Admin' | 'Participant') => {
    await api.put(`/api/admin/participants/${id}/role`, { role })
    participants.reload()
  })

  const active = (participants.data ?? []).filter((person) => !person.isRemoved)
  const removed = (participants.data ?? []).filter((person) => person.isRemoved)

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Código da sala e papéis">Pessoas</PageTitle>

      <Card elevated>
        <SectionTitle hint="Quem já entrou continua na sala mesmo depois de trocar o código.">
          Código da sala
        </SectionTitle>

        {invite.loading && <SkeletonCard lines={1} />}
        {invite.error && <ErrorBox message={invite.error} onRetry={invite.reload} />}
        {rotate.error && <ErrorBox message={rotate.error} />}

        {invite.data && (
          <>
            <p className="nums select-all rounded-2xl border-2 border-dashed border-brand-200 bg-brand-50 py-5 text-center text-3xl font-black tracking-[0.2em] text-brand-800">
              {invite.data.inviteCode}
            </p>

            <p className="mt-2 text-center text-xs text-slate-500">
              {invite.data.memberCount} pessoa(s) na sala · atualizado em{' '}
              {formatDate(invite.data.rotatedAt)}
            </p>

            <div className="mt-4 flex flex-wrap gap-2">
              <Button
                full
                onClick={async () => {
                  const result = await share({
                    title: 'Convite do GC',
                    text: `Crie sua conta no desafio do ${invite.data?.roomName} e entre na sala com o código ${invite.data?.inviteCode}:`,
                  })
                  setFeedback(
                    result === 'copied'
                      ? 'Convite copiado! Cole no grupo.'
                      : result === 'failed'
                        ? 'Não foi possível compartilhar.'
                        : null,
                  )
                }}
              >
                Compartilhar convite
              </Button>
            </div>

            {feedback ? (
              <div className="mt-3">
                <Callout tone="success" live>
                  {feedback}
                </Callout>
              </div>
            ) : null}

            {!rotating ? (
              <div className="mt-4 border-t border-slate-100 pt-4">
                <Button size="sm" variant="ghost" onClick={() => setRotating(true)}>
                  Trocar o código
                </Button>
              </div>
            ) : (
              <form
                className="mt-4 space-y-3 border-t border-slate-100 pt-4"
                onSubmit={(event) => {
                  event.preventDefault()
                  if (window.confirm('Gerar um novo código? O atual deixa de funcionar.')) {
                    void rotate.run()
                  }
                }}
              >
                <Field
                  label="Novo código (opcional)"
                  hint="Deixe vazio para gerar automaticamente. 6 a 20 letras ou números."
                >
                  <Input
                    value={customCode}
                    maxLength={20}
                    autoFocus
                    autoCapitalize="characters"
                    placeholder="DOMUS2026"
                    className="uppercase tracking-widest"
                    onChange={(event) => setCustomCode(event.target.value.toUpperCase())}
                  />
                </Field>

                <div className="flex gap-2">
                  <Button type="submit" variant="danger" size="sm" loading={rotate.loading}>
                    Trocar código
                  </Button>
                  <Button type="button" size="sm" variant="ghost" onClick={() => setRotating(false)}>
                    Cancelar
                  </Button>
                </div>
              </form>
            )}
          </>
        )}
      </Card>

      {participants.loading && <SkeletonCard lines={4} />}
      {participants.error && <ErrorBox message={participants.error} onRetry={participants.reload} />}
      {changeRole.error && <ErrorBox message={changeRole.error} />}

      {participants.data && (
        <Card padded={false}>
          <h2 className="border-b border-slate-100 px-4 py-3 text-sm font-semibold text-slate-700 sm:px-5">
            Participantes ({active.length})
          </h2>

          <ul className="divide-y divide-slate-100">
            {[...active, ...removed].map((participant) => (
              <li key={participant.id} className="flex items-center gap-3 px-4 py-3 sm:px-5">
                <Avatar name={participant.displayName} url={participant.avatarUrl} size={40} />

                <div className="min-w-0 flex-1">
                  <p className="flex flex-wrap items-center gap-1.5 text-sm font-semibold text-slate-900">
                    <span className="truncate">{participant.displayName}</span>
                    {participant.role === 'Admin' && <Badge tone="info">admin</Badge>}
                    {participant.isRemoved && <Badge tone="neutral">removido</Badge>}
                  </p>
                  <p className="nums mt-0.5 text-xs text-slate-500">
                    {participant.seasonPoints} pts · {participant.roundsPlayed} rodada(s) · entrou em{' '}
                    {formatDate(participant.joinedAt)}
                  </p>
                </div>

                {!participant.isRemoved && (
                  <Button
                    size="sm"
                    variant="subtle"
                    loading={changeRole.loading}
                    onClick={() =>
                      void changeRole.run(
                        participant.id,
                        participant.role === 'Admin' ? 'Participant' : 'Admin',
                      )
                    }
                  >
                    {participant.role === 'Admin' ? 'Rebaixar' : 'Promover'}
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}
