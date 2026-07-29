import { useState } from 'react'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminSeason } from '../../api/types'
import {
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorBox,
  Field,
  Input,
  PageTitle,
  SectionTitle,
  SkeletonCard,
} from '../../components/ui'
import { formatDate } from '../../lib/format'

function today(offsetDays: number) {
  const date = new Date()
  date.setDate(date.getDate() + offsetDays)
  return date.toISOString().slice(0, 10)
}

export function AdminSeasonsPage() {
  const seasons = useApi<AdminSeason[]>('/api/admin/seasons')

  const [creating, setCreating] = useState(false)
  const [name, setName] = useState('')
  const [startsOn, setStartsOn] = useState(today(0))
  const [endsOn, setEndsOn] = useState(today(90))

  const create = useMutation(async () => {
    await api.post('/api/admin/seasons', { name, startsOn, endsOn })
    setName('')
    setCreating(false)
    seasons.reload()
  })

  const activate = useMutation(async (id: string) => {
    await api.post(`/api/admin/seasons/${id}/activate`)
    seasons.reload()
  })

  const finish = useMutation(async (id: string) => {
    await api.post(`/api/admin/seasons/${id}/finish`)
    seasons.reload()
  })

  const list = seasons.data ?? []

  return (
    <div className="space-y-4">
      <PageTitle
        subtitle="A temporada ativa define o ranking premiado"
        actions={
          !creating && list.length > 0 ? (
            <Button size="sm" onClick={() => setCreating(true)}>
              Nova temporada
            </Button>
          ) : undefined
        }
      >
        Temporadas
      </PageTitle>

      {(creating || (!seasons.loading && list.length === 0)) && (
        <Card elevated className="animate-rise">
          <SectionTitle>Nova temporada</SectionTitle>

          <form
            className="space-y-4"
            onSubmit={(event) => {
              event.preventDefault()
              void create.run()
            }}
          >
            {create.error ? <ErrorBox message={create.error} /> : null}

            <Field label="Nome" hint="Ex.: 1º trimestre de 2026">
              <Input
                required
                autoFocus
                maxLength={80}
                value={name}
                onChange={(event) => setName(event.target.value)}
              />
            </Field>

            <div className="grid grid-cols-2 gap-3">
              <Field label="Início">
                <Input
                  type="date"
                  required
                  value={startsOn}
                  onChange={(event) => setStartsOn(event.target.value)}
                />
              </Field>
              <Field label="Fim">
                <Input
                  type="date"
                  required
                  value={endsOn}
                  onChange={(event) => setEndsOn(event.target.value)}
                />
              </Field>
            </div>

            <div className="flex gap-2">
              <Button type="submit" loading={create.loading}>
                Criar temporada
              </Button>
              {list.length > 0 && (
                <Button type="button" variant="ghost" onClick={() => setCreating(false)}>
                  Cancelar
                </Button>
              )}
            </div>
          </form>
        </Card>
      )}

      {seasons.loading && <SkeletonCard lines={3} />}
      {seasons.error && <ErrorBox message={seasons.error} onRetry={seasons.reload} />}
      {activate.error && <ErrorBox message={activate.error} />}
      {finish.error && <ErrorBox message={finish.error} />}

      {!seasons.loading && list.length === 0 && !creating && (
        <EmptyState
          title="Nenhuma temporada criada"
          description="A temporada é o período que agrupa as rodadas e define quem sobe ao pódio."
        />
      )}

      <ul className="space-y-3">
        {list.map((season) => (
          <li key={season.id}>
            <Card>
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <h3 className="text-base font-semibold text-slate-900">{season.name}</h3>
                  <p className="nums mt-0.5 text-xs text-slate-500">
                    {formatDate(season.startsOn)} a {formatDate(season.endsOn)} ·{' '}
                    {season.publishedRoundCount} de {season.roundCount} rodadas publicadas
                  </p>
                </div>

                <Badge
                  tone={
                    season.status === 'Active'
                      ? 'success'
                      : season.status === 'Finished'
                        ? 'neutral'
                        : 'info'
                  }
                  dot={season.status === 'Active'}
                >
                  {season.status === 'Active'
                    ? 'Ativa'
                    : season.status === 'Finished'
                      ? 'Encerrada'
                      : 'Rascunho'}
                </Badge>
              </div>

              {season.podium.length > 0 && (
                <ol className="mt-3 space-y-1.5 rounded-xl border border-amber-200 bg-amber-50 p-3">
                  {season.podium.map((entry) => (
                    <li key={entry.position} className="flex items-center gap-2 text-sm text-amber-900">
                      <span aria-hidden="true">
                        {entry.position === 1 ? '🥇' : entry.position === 2 ? '🥈' : '🥉'}
                      </span>
                      <strong className="min-w-0 flex-1 truncate">{entry.displayName}</strong>
                      <span className="nums font-semibold">{entry.totalPoints} pts</span>
                    </li>
                  ))}
                </ol>
              )}

              <div className="mt-4 flex flex-wrap gap-2">
                {season.status !== 'Finished' && season.status !== 'Active' && (
                  <Button size="sm" onClick={() => void activate.run(season.id)}>
                    Tornar ativa
                  </Button>
                )}

                <a
                  href={`/api/admin/seasons/${season.id}/export`}
                  className="inline-flex min-h-9 items-center rounded-xl bg-slate-100 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-200"
                >
                  Exportar CSV
                </a>

                {season.status !== 'Finished' && (
                  <Button
                    size="sm"
                    variant="ghost"
                    className="text-red-700 hover:bg-red-50"
                    onClick={() => {
                      if (window.confirm('Encerrar a temporada e congelar o pódio dos 3 primeiros?')) {
                        void finish.run(season.id)
                      }
                    }}
                  >
                    Encerrar
                  </Button>
                )}
              </div>
            </Card>
          </li>
        ))}
      </ul>
    </div>
  )
}
