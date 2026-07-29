import { useParams } from 'react-router-dom'
import { useApi } from '../../api/hooks'
import type { Ranking, RoundStats } from '../../api/types'
import { Card, EmptyState, ErrorBox, PageTitle, Spinner } from '../../components/ui'
import { formatPercent } from '../../lib/format'

export function AdminRoundStatsPage() {
  const { roundId } = useParams<{ roundId: string }>()
  const stats = useApi<RoundStats>(`/api/admin/rounds/${roundId}/stats`)
  const ranking = useApi<Ranking>(`/api/rankings/round/${roundId}`)

  if (stats.loading) return <Spinner />
  if (stats.error) return <ErrorBox message={stats.error} onRetry={stats.reload} />
  if (!stats.data) return null

  const data = stats.data

  return (
    <div className="space-y-4">
      <PageTitle subtitle={`Semana ${data.round.weekNumber}`}>{data.round.title}</PageTitle>

      <div className="grid grid-cols-2 gap-2">
        <Tile label="Participação" value={formatPercent(data.participationRate)} hint={`${data.attemptCount} de ${data.participantCount}`} />
        <Tile label="Concluíram" value={data.finishedCount} hint="tentativas finalizadas" />
        <Tile label="Média" value={data.averagePoints} hint={`mediana ${data.medianPoints}`} />
        <Tile label="Tempo médio" value={`${data.averageSecondsPerQuestion}s`} hint="por pergunta" />
      </div>

      <Card>
        <h2 className="mb-3 text-sm font-semibold text-slate-700">Perguntas mais difíceis</h2>

        <ul className="space-y-3">
          {[...data.questions]
            .sort((a, b) => a.accuracy - b.accuracy)
            .map((question) => (
              <li key={question.questionId}>
                <div className="flex items-start justify-between gap-3 text-sm">
                  <span className="min-w-0 text-slate-700">
                    {question.order}. {question.text}
                  </span>
                  <span className="shrink-0 font-semibold tabular-nums text-slate-900">
                    {formatPercent(question.accuracy)}
                  </span>
                </div>

                <div className="mt-1 h-2 overflow-hidden rounded-full bg-slate-200">
                  <div
                    className={`h-full rounded-full ${question.accuracy < 0.5 ? 'bg-red-500' : 'bg-emerald-500'}`}
                    style={{ width: `${question.accuracy * 100}%` }}
                  />
                </div>

                <p className="mt-1 text-xs text-slate-500">
                  {question.correct} de {question.answers} respostas · {question.averageSeconds}s em média
                </p>
              </li>
            ))}
        </ul>
      </Card>

      <Card>
        <h2 className="mb-2 text-sm font-semibold text-slate-700">
          Ainda não responderam ({data.missing.length})
        </h2>

        {data.missing.length === 0 ? (
          <p className="text-sm text-emerald-700">Todo mundo participou. 🎉</p>
        ) : (
          <p className="text-sm text-slate-600">{data.missing.join(', ')}</p>
        )}
      </Card>

      <Card>
        <h2 className="mb-2 text-sm font-semibold text-slate-700">Ranking da semana</h2>

        {ranking.loading && <Spinner />}
        {ranking.error && (
          <EmptyState title="Ranking indisponível" description="Ele fica visível quando a rodada encerra." />
        )}

        {ranking.data && (
          <ol className="space-y-1 text-sm">
            {ranking.data.entries.map((entry) => (
              <li key={entry.participantId} className="flex justify-between gap-3">
                <span className="min-w-0 truncate text-slate-700">
                  {entry.position}º {entry.displayName}
                </span>
                <span className="shrink-0 font-semibold text-slate-900">{entry.totalPoints}</span>
              </li>
            ))}
          </ol>
        )}
      </Card>
    </div>
  )
}

function Tile({ label, value, hint }: { label: string; value: string | number; hint?: string }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
      <p className="text-xs text-slate-500">{label}</p>
      <p className="text-xl font-bold text-slate-900">{value}</p>
      {hint ? <p className="text-xs text-slate-500">{hint}</p> : null}
    </div>
  )
}
