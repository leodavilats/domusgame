import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { Dashboard } from '../api/types'
import { Badge, Button, Card, EmptyState, ErrorBox, PageTitle, Spinner } from '../components/ui'
import { formatCountdown, formatDateTime, formatWeekday, pluralize } from '../lib/format'
import { useSession } from '../auth/SessionContext'

export function HomePage() {
  const { me } = useSession()
  const navigate = useNavigate()
  const { data, loading, error, reload } = useApi<Dashboard>('/api/dashboard')
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  if (loading) return <Spinner />
  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  const { round, myAttempt, actions, stats } = data

  if (!data.room) {
    return (
      <div className="space-y-4">
        <PageTitle subtitle="Falta um passo para você começar">
          Olá, {me?.displayName.split(' ')[0]}!
        </PageTitle>

        <EmptyState
          title="Você ainda não está em uma sala"
          description="Os desafios, o ranking e as pessoas ficam dentro da sala do seu GC. Use o código que o líder compartilhou para entrar."
          action={<Button onClick={() => navigate('/sala')}>Tenho um código</Button>}
        />
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <PageTitle subtitle={data.season ? data.season.name : 'Nenhuma temporada em andamento'}>
        Olá, {me?.displayName.split(' ')[0]}!
      </PageTitle>

      <div className="grid grid-cols-3 gap-2">
        <StatTile label="Pontos" value={stats.seasonPoints} />
        <StatTile label="Posição" value={stats.position ? `${stats.position}º` : '-'} />
        <StatTile label="Sequência" value={stats.streak} />
      </div>

      {!data.season ? (
        <EmptyState
          title="Nenhuma temporada em andamento"
          description="Assim que o lider abrir uma nova temporada, o desafio aparece aqui."
        />
      ) : !round ? (
        <EmptyState title="A próxima rodada está sendo preparada" description="Volte em breve." />
      ) : (
        <Card>
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                Semana {round.weekNumber}
              </p>
              <h2 className="text-lg font-bold text-slate-900">{round.title}</h2>
              {data.lessonReference ? (
                <p className="mt-0.5 text-sm text-slate-500">{data.lessonReference}</p>
              ) : null}
            </div>
            <AvailabilityBadge availability={round.availability} />
          </div>

          <div className="mt-4 space-y-3">
            {round.availability === 'Scheduled' && (
              <p className="text-sm text-slate-600">
                Abre {formatWeekday(round.opensAt)} — faltam{' '}
                <strong>{formatCountdown(round.opensAt, now)}</strong>
              </p>
            )}

            {round.availability === 'Open' && (
              <p className="text-sm text-slate-600">
                Fecha {formatWeekday(round.closesAt)} — resta{' '}
                <strong>{formatCountdown(round.closesAt, now)}</strong>
              </p>
            )}

            {round.availability === 'Closed' && (
              <p className="text-sm text-slate-600">Encerrada em {formatDateTime(round.closesAt)}</p>
            )}

            <p className="text-sm text-slate-500">
              {pluralize(round.questionCount, 'pergunta', 'perguntas')} · {round.questionTimeLimitSeconds}s por
              pergunta · até {round.maxPoints} pontos
            </p>

            {myAttempt?.status === 'Completed' && myAttempt.totalPoints != null && (
              <div className="rounded-xl bg-brand-50 p-3 text-sm text-brand-800">
                Você fez <strong>{myAttempt.totalPoints} pontos</strong>
                {myAttempt.correctCount != null && ` (${myAttempt.correctCount}/${myAttempt.questionCount} acertos)`}
                {round.availability === 'Open' && '. O ranking sai quando a rodada encerrar.'}
                {myAttempt.position ? ` · ${myAttempt.position}º lugar na semana` : ''}
              </div>
            )}

            <div className="flex flex-wrap gap-2 pt-1">
              {actions.canStart && (
                <Button onClick={() => navigate(`/rodadas/${round.id}/quiz`)}>Responder o desafio</Button>
              )}
              {actions.canResume && (
                <Button onClick={() => navigate(`/rodadas/${round.id}/quiz`)}>
                  Continuar ({myAttempt?.answeredCount ?? 0}/{round.questionCount})
                </Button>
              )}
              {actions.canReview && (
                <Button variant="secondary" onClick={() => navigate(`/rodadas/${round.id}/revisao`)}>
                  Ver gabarito
                </Button>
              )}
              {myAttempt && (
                <Button
                  variant="ghost"
                  onClick={() => navigate(`/tentativas/${myAttempt.attemptId}/resultado`)}
                >
                  Meu resultado
                </Button>
              )}
              {data.lessonTitle && (
                <Link
                  to={`/rodadas/${round.id}/licao`}
                  className="inline-flex min-h-11 items-center px-2 text-sm font-semibold text-brand-600"
                >
                  Ler a lição
                </Link>
              )}
            </div>
          </div>
        </Card>
      )}

      {data.nextRoundOpensAt && round?.availability !== 'Scheduled' && (
        <Card className="bg-slate-50">
          <p className="text-sm text-slate-600">
            Próxima rodada abre {formatWeekday(data.nextRoundOpensAt)} (
            {formatCountdown(data.nextRoundOpensAt, now)}).
          </p>
        </Card>
      )}
    </div>
  )
}

function StatTile({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-3 text-center shadow-sm">
      <p className="text-lg font-bold text-slate-900">{value}</p>
      <p className="text-xs text-slate-500">{label}</p>
    </div>
  )
}

export function AvailabilityBadge({ availability }: { availability: string }) {
  const map: Record<string, { label: string; tone: 'neutral' | 'success' | 'warning' | 'info' }> = {
    Draft: { label: 'Rascunho', tone: 'neutral' },
    Scheduled: { label: 'Agendada', tone: 'info' },
    Open: { label: 'Aberta', tone: 'success' },
    Closed: { label: 'Encerrada', tone: 'neutral' },
  }

  const item = map[availability] ?? map.Draft
  return <Badge tone={item.tone}>{item.label}</Badge>
}
