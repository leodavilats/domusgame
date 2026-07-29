import { Link } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { RoundListItem } from '../api/types'
import { CalendarIcon, ChevronRightIcon } from '../components/Icons'
import { Badge, Card, EmptyState, ErrorBox, PageTitle, SkeletonCard } from '../components/ui'
import { formatDate, formatDuration } from '../lib/format'
import { AvailabilityBadge } from './HomePage'

export function HistoryPage() {
  const { data, loading, error, reload } = useApi<RoundListItem[]>('/api/rounds')

  if (loading) {
    return (
      <div className="space-y-3">
        <SkeletonCard lines={2} />
        <SkeletonCard lines={2} />
      </div>
    )
  }

  if (error) return <ErrorBox message={error} onRetry={reload} />

  const items = data ?? []
  const played = items.filter((item) => item.myAttempt?.totalPoints != null).length
  const closed = items.filter((item) => item.round.availability === 'Closed').length

  return (
    <div className="space-y-4">
      <PageTitle
        subtitle={
          closed > 0
            ? `Você respondeu ${played} de ${closed} rodadas encerradas`
            : 'Todas as rodadas publicadas da temporada'
        }
      >
        Histórico
      </PageTitle>

      {items.length === 0 && (
        <EmptyState
          icon={<CalendarIcon />}
          title="Nenhuma rodada publicada ainda"
          description="Quando o líder publicar a primeira rodada da temporada, ela aparece aqui."
        />
      )}

      <ul className="space-y-3">
        {items.map(({ round, myAttempt }) => {
          const target =
            round.availability === 'Closed'
              ? `/rodadas/${round.id}/revisao`
              : round.availability === 'Open' && !myAttempt
                ? `/rodadas/${round.id}/quiz`
                : `/rodadas/${round.id}/licao`

          const targetLabel =
            round.availability === 'Closed'
              ? 'Ver gabarito'
              : round.availability === 'Open' && !myAttempt
                ? 'Responder agora'
                : round.availability === 'Scheduled'
                  ? 'Ver detalhes'
                  : 'Ler a lição'

          return (
            <li key={round.id}>
              <Card padded={false} className="overflow-hidden">
                <Link
                  to={target}
                  className="block p-4 transition hover:bg-slate-50 sm:p-5"
                  aria-label={`Semana ${round.weekNumber}: ${round.title} — ${targetLabel}`}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                        Semana {round.weekNumber}
                      </p>
                      <h2 className="truncate text-base font-semibold text-slate-900">{round.title}</h2>
                      <p className="nums mt-0.5 text-xs text-slate-500">
                        {formatDate(round.opensAt)} a {formatDate(round.closesAt)}
                      </p>
                    </div>
                    <AvailabilityBadge availability={round.availability} />
                  </div>

                  <div className="mt-3 flex items-center justify-between gap-3">
                    {myAttempt?.totalPoints != null ? (
                      <p className="nums flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-slate-700">
                        <strong className="text-brand-700">{myAttempt.totalPoints} pts</strong>
                        <span className="text-slate-400">·</span>
                        <span>
                          {myAttempt.correctCount}/{myAttempt.questionCount} acertos
                        </span>
                        <span className="text-slate-400">·</span>
                        <span>{formatDuration(myAttempt.totalTimeMs ?? 0)}</span>
                        {myAttempt.position ? <Badge tone="info">{myAttempt.position}º</Badge> : null}
                      </p>
                    ) : myAttempt ? (
                      <Badge tone="warning">
                        Em andamento ({myAttempt.answeredCount}/{myAttempt.questionCount})
                      </Badge>
                    ) : (
                      <p className="text-sm text-slate-500">
                        {round.availability === 'Closed' ? 'Não participou' : 'Ainda não respondida'}
                      </p>
                    )}

                    <span className="flex shrink-0 items-center gap-1 text-sm font-semibold text-brand-700">
                      {targetLabel}
                      <ChevronRightIcon className="h-4 w-4" />
                    </span>
                  </div>
                </Link>
              </Card>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
