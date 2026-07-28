import { Link } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { RoundListItem } from '../api/types'
import { Card, EmptyState, ErrorBox, PageTitle, Spinner } from '../components/ui'
import { formatDate, formatDuration } from '../lib/format'
import { AvailabilityBadge } from './HomePage'

export function HistoryPage() {
  const { data, loading, error, reload } = useApi<RoundListItem[]>('/api/rounds')

  if (loading) return <Spinner />
  if (error) return <ErrorBox message={error} onRetry={reload} />

  const items = data ?? []

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Todas as rodadas publicadas da temporada">Historico</PageTitle>

      {items.length === 0 && <EmptyState title="Nenhuma rodada publicada ainda" />}

      {items.map(({ round, myAttempt }) => (
        <Card key={round.id}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                Semana {round.weekNumber}
              </p>
              <h2 className="truncate text-base font-semibold text-slate-900">{round.title}</h2>
              <p className="mt-0.5 text-xs text-slate-500">
                {formatDate(round.opensAt)} a {formatDate(round.closesAt)}
              </p>
            </div>
            <AvailabilityBadge availability={round.availability} />
          </div>

          <div className="mt-3 flex items-center justify-between gap-3">
            {myAttempt?.totalPoints != null ? (
              <p className="text-sm text-slate-700">
                <strong>{myAttempt.totalPoints}</strong> pts · {myAttempt.correctCount}/{myAttempt.questionCount}{' '}
                acertos · {formatDuration(myAttempt.totalTimeMs ?? 0)}
              </p>
            ) : myAttempt ? (
              <p className="text-sm text-amber-700">
                Em andamento ({myAttempt.answeredCount}/{myAttempt.questionCount})
              </p>
            ) : (
              <p className="text-sm text-slate-500">
                {round.availability === 'Closed' ? 'Não participou' : 'Ainda não respondida'}
              </p>
            )}

            <div className="flex shrink-0 gap-3 text-sm font-semibold text-brand-600">
              {round.availability !== 'Scheduled' && (
                <Link to={`/rodadas/${round.id}/licao`}>Licao</Link>
              )}
              {round.availability === 'Closed' && (
                <Link to={`/rodadas/${round.id}/revisao`}>Gabarito</Link>
              )}
              {round.availability === 'Open' && !myAttempt && (
                <Link to={`/rodadas/${round.id}/quiz`}>Responder</Link>
              )}
            </div>
          </div>
        </Card>
      ))}
    </div>
  )
}
