import { Link, useNavigate } from 'react-router-dom'
import { useApi } from '../../api/hooks'
import type { Overview } from '../../api/types'
import { ChevronRightIcon } from '../../components/Icons'
import {
  Button,
  Card,
  EmptyState,
  ErrorBox,
  PageTitle,
  ProgressBar,
  SkeletonCard,
  StatTile,
} from '../../components/ui'
import { AvailabilityBadge } from '../HomePage'

export function AdminHomePage() {
  const navigate = useNavigate()
  const { data, loading, error, reload } = useApi<Overview>('/api/admin/stats/overview')

  if (loading) return <SkeletonCard lines={4} />
  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  const total = Math.max(1, data.participantCount)

  return (
    <div className="space-y-4">
      <PageTitle subtitle={data.seasonName ?? 'Nenhuma temporada ativa'}>Visão geral</PageTitle>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <StatTile label="Participantes" value={data.participantCount} tone="brand" />
        <StatTile label="Administradores" value={data.adminCount} />
        <StatTile label="Rodadas publicadas" value={data.weeks.length} />
        <StatTile
          label="Participação média"
          value={
            data.weeks.length === 0
              ? '—'
              : `${Math.round(
                  (data.weeks.reduce((sum, week) => sum + week.attempts, 0) /
                    (data.weeks.length * total)) *
                    100,
                )}%`
          }
        />
      </div>

      {!data.seasonId ? (
        <EmptyState
          title="Nenhuma temporada ativa"
          description="A temporada agrupa as rodadas e define o ranking premiado. É o primeiro passo."
          action={
            <Button size="lg" onClick={() => navigate('/admin/temporadas')}>
              Criar temporada
            </Button>
          }
        />
      ) : data.weeks.length === 0 ? (
        <EmptyState
          title="Nenhuma rodada publicada nesta temporada"
          description="Crie o rascunho da primeira semana, cadastre a lição e as perguntas."
          action={
            <Button size="lg" onClick={() => navigate('/admin/rodadas')}>
              Criar rodada
            </Button>
          }
        />
      ) : (
        <Card padded={false}>
          <h2 className="border-b border-slate-100 px-4 py-3 text-sm font-semibold text-slate-700 sm:px-5">
            Participação por semana
          </h2>

          <ul className="divide-y divide-slate-100">
            {data.weeks.map((week) => (
              <li key={week.roundId}>
                <Link
                  to={`/admin/rodadas/${week.roundId}/estatisticas`}
                  className="block px-4 py-3 transition hover:bg-slate-50 sm:px-5"
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="min-w-0 truncate text-sm font-semibold text-slate-800">
                      S{week.weekNumber}. {week.title}
                    </span>
                    <span className="flex shrink-0 items-center gap-2">
                      <AvailabilityBadge availability={week.availability} />
                      <ChevronRightIcon className="h-4 w-4 text-slate-400" />
                    </span>
                  </div>

                  <div className="mt-2 flex items-center gap-3">
                    <ProgressBar
                      value={week.attempts}
                      max={total}
                      label={`Participação na semana ${week.weekNumber}`}
                      size="sm"
                    />
                    <span className="nums shrink-0 text-xs font-semibold text-slate-600">
                      {week.attempts}/{data.participantCount}
                    </span>
                  </div>

                  <p className="nums mt-1 text-xs text-slate-500">
                    Média de {week.averagePoints} pontos
                  </p>
                </Link>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}
