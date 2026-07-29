import { useState } from 'react'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminParticipant, Invite, ResetPasswordResult } from '../../api/types'
import { Avatar, Badge, Button, Card, ErrorBox, Field, Input, PageTitle, Spinner } from '../../components/ui'
import { formatDate } from '../../lib/format'
import { share } from '../../lib/share'

export function AdminParticipantsPage() {
  const invite = useApi<Invite>('/api/admin/invite')
  const participants = useApi<AdminParticipant[]>('/api/admin/participants')

  const [customCode, setCustomCode] = useState('')
  const [feedback, setFeedback] = useState<string | null>(null)

  const rotate = useMutation(async () => {
    await api.post<Invite>('/api/admin/invite', { code: customCode.trim() === '' ? null : customCode.trim() })
    setCustomCode('')
    invite.reload()
  })

  const changeRole = useMutation(async (id: string, role: 'Admin' | 'Participant') => {
    await api.put(`/api/admin/participants/${id}/role`, { role })
    participants.reload()
  })

  // Sem serviço de e-mail não existe "esqueci minha senha": o admin gera uma senha
  // temporária e repassa pelo grupo. Ela aparece uma única vez.
  const [temporary, setTemporary] = useState<ResetPasswordResult | null>(null)

  const resetPassword = useMutation(async (id: string) => {
    const result = await api.post<ResetPasswordResult>(`/api/admin/participants/${id}/reset-password`)
    setTemporary(result)
    return result
  })

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Convite do GC e papéis">Pessoas</PageTitle>

      <Card>
        <h2 className="mb-3 text-sm font-semibold text-slate-700">Código de convite</h2>

        {invite.loading && <Spinner />}
        {invite.error && <ErrorBox message={invite.error} onRetry={invite.reload} />}
        {rotate.error && <ErrorBox message={rotate.error} />}

        {invite.data && (
          <>
            <p className="rounded-xl bg-slate-100 p-4 text-center text-2xl font-black tracking-widest text-slate-900">
              {invite.data.inviteCode}
            </p>
            <p className="mt-2 text-xs text-slate-500">
              {invite.data.memberCount} pessoa(s) no GC · atualizado em {formatDate(invite.data.rotatedAt)}
            </p>

            <div className="mt-3 flex flex-wrap gap-2">
              <Button
                variant="secondary"
                onClick={async () => {
                  const result = await share({
                    title: 'Convite do GC',
                    text: `Entre no desafio do ${invite.data?.gcName} com o código ${invite.data?.inviteCode}:`,
                  })
                  setFeedback(result === 'copied' ? 'Convite copiado!' : null)
                }}
              >
                Compartilhar convite
              </Button>
            </div>

            {feedback ? <p className="mt-2 text-sm text-emerald-700">{feedback}</p> : null}

            <form
              className="mt-4 space-y-2 border-t border-slate-200 pt-4"
              onSubmit={(event) => {
                event.preventDefault()
                if (window.confirm('Gerar um novo código? O atual deixa de funcionar.')) void rotate.run()
              }}
            >
              <Field label="Novo código (opcional)" hint="Deixe vazio para gerar automaticamente. 6 a 20 letras ou números.">
                <Input
                  value={customCode}
                  maxLength={20}
                  autoCapitalize="characters"
                  onChange={(event) => setCustomCode(event.target.value)}
                />
              </Field>
              <Button type="submit" variant="danger" loading={rotate.loading}>
                Trocar código
              </Button>
            </form>
          </>
        )}
      </Card>

      {temporary && (
        <Card className="border-amber-300 bg-amber-50">
          <h2 className="text-sm font-semibold text-amber-900">
            Senha temporária de {temporary.displayName}
          </h2>

          <p className="mt-3 rounded-xl bg-white p-4 text-center text-2xl font-black tracking-widest text-slate-900">
            {temporary.temporaryPassword}
          </p>

          <p className="mt-2 text-xs text-amber-900">
            Anote ou compartilhe agora: ela não será mostrada de novo. A senha antiga deixou de valer.
          </p>

          <div className="mt-3 flex flex-wrap gap-2">
            <Button
              variant="secondary"
              onClick={async () => {
                const result = await share({
                  title: 'Senha temporária',
                  text: `Sua senha temporária no ${invite.data?.gcName ?? 'GC'} é ${temporary.temporaryPassword} — troque depois de entrar.`,
                })
                setFeedback(result === 'copied' ? 'Senha copiada!' : null)
              }}
            >
              Compartilhar
            </Button>
            <Button variant="ghost" onClick={() => setTemporary(null)}>
              Já anotei
            </Button>
          </div>
        </Card>
      )}

      {resetPassword.error && <ErrorBox message={resetPassword.error} />}

      {participants.loading && <Spinner />}
      {participants.error && <ErrorBox message={participants.error} onRetry={participants.reload} />}
      {changeRole.error && <ErrorBox message={changeRole.error} />}

      {participants.data && (
        <Card>
          <h2 className="mb-3 text-sm font-semibold text-slate-700">
            Participantes ({participants.data.filter((p) => !p.isRemoved).length})
          </h2>

          <ul className="space-y-3">
            {participants.data.map((participant) => (
              <li key={participant.id} className="flex items-center gap-3">
                <Avatar name={participant.displayName} url={participant.avatarUrl} size={40} />

                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold text-slate-900">
                    {participant.displayName}{' '}
                    {participant.role === 'Admin' && <Badge tone="info">admin</Badge>}
                    {participant.isRemoved && <Badge tone="neutral">removido</Badge>}
                    {!participant.showInRanking && !participant.isRemoved && (
                      <Badge tone="neutral">fora do ranking</Badge>
                    )}
                  </p>
                  <p className="text-xs text-slate-500">
                    {participant.seasonPoints} pts · {participant.roundsPlayed} rodada(s) · entrou em{' '}
                    {formatDate(participant.joinedAt)}
                  </p>
                </div>

                {!participant.isRemoved && (
                  <div className="flex shrink-0 flex-col items-end gap-1 text-sm font-semibold">
                    <button
                      type="button"
                      className="text-brand-600"
                      onClick={() =>
                        void changeRole.run(
                          participant.id,
                          participant.role === 'Admin' ? 'Participant' : 'Admin',
                        )
                      }
                    >
                      {participant.role === 'Admin' ? 'Rebaixar' : 'Promover'}
                    </button>

                    <button
                      type="button"
                      className="text-slate-600"
                      onClick={() => {
                        if (window.confirm(`Gerar uma senha temporária para ${participant.displayName}? A senha atual deixa de valer.`)) {
                          void resetPassword.run(participant.id)
                        }
                      }}
                    >
                      Redefinir senha
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}
