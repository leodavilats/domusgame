import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '../../api/hooks'
import type { Ranking, RoundStats } from '../../api/types'
import { ArrowLeftIcon } from '../../components/Icons'
import {
  Badge,
  Callout,
  Card,
  ErrorBox,
  PageTitle,
  ProgressBar,
  SkeletonCard,
  StatTile,
} from '../../components/ui'
import { formatPercent } from '../../lib/format'

export function AdminRoundStatsPage() {
  const { roundId } = useParams<{ roundId: string }>()
  const navigate = useNavigate()
  const stats = useApi<RoundStats>(`/api/admin/rounds/${roundId}/stats`)
  const ranking = useApi<Ranking>(`/api/rankings/round/${roundId}`)

  if (stats.loading) return <SkeletonCard lines={4} />
  if (stats.error) return <ErrorBox message={stats.error} onRetry={stats.reload} />
  if (!stats.data) return null

  const data = stats.data
  const hardest = [...data.questions].sort((a, b) => a.accuracy - b.accuracy)

  return (
    <div className="space-y-4">
      <button
        type="button"
        onClick={() => navigate('/admin/rodadas')}
        className="-ml-2 inline-flex min-h-10 items-center gap-1.5 rounded-xl px-2 text-sm font-semibold text-slate-500 transition hover:bg-slate-100 hover:text-slate-700"
      >
        <ArrowLeftIcon className="h-5 w-5" />
        Rodadas
      </button>

      <PageTitle subtitle={`Semana ${data.round.weekNumber}`}>{data.round.title}</PageTitle>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <StatTile
          label="Participação"
          value={formatPercent(data.participationRate)}
          hint={`${data.attemptCount} de ${data.participantCount}`}
          tone="brand"
        />
        <StatTile label="Concluíram" value={data.finishedCount} hint="tentativas finalizadas" />
        <StatTile label="Média" value={data.averagePoints} hint={`mediana ${data.medianPoints}`} />
        <StatTile label="Tempo médio" value={`${data.averageSecondsPerQuestion}s`} hint="por pergunta" />
      </div>

      <Card>
        <h2 className="mb-1 text-sm font-semibold text-slate-700">Perguntas por índice de acerto</h2>
        <p className="mb-4 text-xs text-slate-500">
          Da mais difícil para a mais fácil — as primeiras valem revisar na reunião.
        </p>

        <ul className="space-y-4">
          {hardest.map((question) => (
            <li key={question.questionId}>
              <div className="flex items-start justify-between gap-3">
                <span className="min-w-0 text-sm text-slate-700">
                  <span className="nums text-slate-400">{question.order}.</span> {question.text}
                </span>
                <Badge tone={question.accuracy < 0.5 ? 'danger' : question.accuracy < 0.8 ? 'warning' : 'success'}>
                  {formatPercent(question.accuracy)}
                </Badge>
              </div>

              <div className="mt-2">
                <ProgressBar
                  value={question.accuracy * 100}
                  label={`Acertos na pergunta ${question.order}`}
                  tone={question.accuracy < 0.5 ? 'danger' : 'success'}
                  size="sm"
                />
              </div>

              <p className="nums mt-1 text-xs text-slate-500">
                {question.correct} de {question.answers} respostas · {question.averageSeconds}s em média
              </p>
            </li>
          ))}
        </ul>
      </Card>

      <Card>
        <h2 className="mb-3 text-sm font-semibold text-slate-700">
          Ainda não responderam ({data.missing.length})
        </h2>

        {data.missing.length === 0 ? (
          <Callout tone="success">Todo mundo participou. 🎉</Callout>
        ) : (
          <ul className="flex flex-wrap gap-1.5">
            {data.missing.map((name) => (
              <li key={name}>
                <Badge tone="warning">{name}</Badge>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card padded={false}>
        <h2 className="border-b border-slate-100 px-4 py-3 text-sm font-semibold text-slate-700 sm:px-5">
          Ranking da semana
        </h2>

        {ranking.loading && (
          <div className="p-4">
            <SkeletonCard lines={3} />
          </div>
        )}

        {ranking.error && (
          <div className="p-4">
            <Callout tone="neutral">
              O ranking da semana fica visível quando a rodada encerra.
            </Callout>
          </div>
        )}

        {ranking.data && (
          <ol className="divide-y divide-slate-100">
            {ranking.data.entries.map((entry) => (
              <li key={entry.participantId} className="flex items-center gap-3 px-4 py-2.5 sm:px-5">
                <span className="nums w-6 shrink-0 text-center text-sm font-bold text-slate-400">
                  {entry.position}
                </span>
                <span className="min-w-0 flex-1 truncate text-sm text-slate-700">
                  {entry.displayName}
                </span>
                <span className="nums shrink-0 text-sm font-bold text-slate-900">
                  {entry.totalPoints}
                </span>
              </li>
            ))}
          </ol>
        )}
      </Card>
    </div>
  )
}
