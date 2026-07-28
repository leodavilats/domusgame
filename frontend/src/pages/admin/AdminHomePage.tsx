import { Link } from 'react-router-dom'
import { useApi } from '../../api/hooks'
import type { Overview } from '../../api/types'
import { Card, EmptyState, ErrorBox, PageTitle, Spinner } from '../../components/ui'
import { AvailabilityBadge } from '../HomePage'

export function AdminHomePage() {
  const { data, loading, error, reload } = useApi<Overview>('/api/admin/stats/overview')

  if (loading) return <Spinner />
  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  const maxAttempts = Math.max(1, data.participantCount)

  return (
    <div className="space-y-4">
      <PageTitle subtitle={data.seasonName ?? 'Nenhuma temporada ativa'}>Visao geral</PageTitle>

      <div className="grid grid-cols-2 gap-2">
        <Tile label="Participantes" value={data.participantCount} />
        <Tile label="Administradores" value={data.adminCount} />
      </div>

      {!data.seasonId ? (
        <EmptyState
          title="Nenhuma temporada ativa"
          description="A temporada agrupa as rodadas e define o ranking premiado. E o primeiro passo."
          action={
            <Link
              to="/admin/temporadas"
              className="inline-flex min-h-11 items-center rounded-xl bg-brand-600 px-4 text-sm font-semibold text-white"
            >
              Criar temporada
            </Link>
          }
        />
      ) : data.weeks.length === 0 ? (
        <EmptyState
          title="Nenhuma rodada publicada nesta temporada"
          description="Crie o rascunho da primeira semana, cadastre a licao e as perguntas."
          action={
            <Link
              to="/admin/rodadas"
              className="inline-flex min-h-11 items-center rounded-xl bg-brand-600 px-4 text-sm font-semibold text-white"
            >
              Criar rodada
            </Link>
          }
        />
      ) : (
        <Card>
          <h2 className="mb-3 text-sm font-semibold text-slate-700">Participacao por semana</h2>

          <ul className="space-y-3">
            {data.weeks.map((week) => (
              <li key={week.roundId}>
                <div className="flex items-center justify-between gap-2 text-sm">
                  <Link
                    to={`/admin/rodadas/${week.roundId}/estatisticas`}
                    className="min-w-0 truncate font-medium text-slate-800"
                  >
                    S{week.weekNumber}. {week.title}
                  </Link>
                  <div className="flex shrink-0 items-center gap-2">
                    <AvailabilityBadge availability={week.availability} />
                    <span className="tabular-nums text-slate-600">
                      {week.attempts}/{data.participantCount}
                    </span>
                  </div>
                </div>

                <div className="mt-1 h-2 overflow-hidden rounded-full bg-slate-200">
                  <div
                    className="h-full rounded-full bg-brand-500"
                    style={{ width: `${Math.min(100, (week.attempts / maxAttempts) * 100)}%` }}
                  />
                </div>

                <p className="mt-1 text-xs text-slate-500">Media de {week.averagePoints} pontos</p>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}

function Tile({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-3 text-center shadow-sm">
      <p className="text-xl font-bold text-slate-900">{value}</p>
      <p className="text-xs text-slate-500">{label}</p>
    </div>
  )
}
