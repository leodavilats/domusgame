import { useState } from 'react'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminSeason } from '../../api/types'
import { Badge, Button, Card, ErrorBox, Field, Input, PageTitle, Spinner } from '../../components/ui'
import { formatDate } from '../../lib/format'

function today(offsetDays: number) {
  const date = new Date()
  date.setDate(date.getDate() + offsetDays)
  return date.toISOString().slice(0, 10)
}

export function AdminSeasonsPage() {
  const seasons = useApi<AdminSeason[]>('/api/admin/seasons')

  const [name, setName] = useState('')
  const [startsOn, setStartsOn] = useState(today(0))
  const [endsOn, setEndsOn] = useState(today(90))

  const create = useMutation(async () => {
    await api.post('/api/admin/seasons', { name, startsOn, endsOn })
    setName('')
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

  return (
    <div className="space-y-4">
      <PageTitle subtitle="A temporada ativa define o ranking premiado">Temporadas</PageTitle>

      <Card>
        <h2 className="mb-3 text-sm font-semibold text-slate-700">Nova temporada</h2>

        <form
          className="space-y-3"
          onSubmit={(event) => {
            event.preventDefault()
            void create.run()
          }}
        >
          {create.error ? <ErrorBox message={create.error} /> : null}

          <Field label="Nome">
            <Input required maxLength={80} value={name} onChange={(event) => setName(event.target.value)} />
          </Field>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Início">
              <Input type="date" required value={startsOn} onChange={(event) => setStartsOn(event.target.value)} />
            </Field>
            <Field label="Fim">
              <Input type="date" required value={endsOn} onChange={(event) => setEndsOn(event.target.value)} />
            </Field>
          </div>

          <Button type="submit" loading={create.loading}>
            Criar temporada
          </Button>
        </form>
      </Card>

      {seasons.loading && <Spinner />}
      {seasons.error && <ErrorBox message={seasons.error} onRetry={seasons.reload} />}
      {activate.error && <ErrorBox message={activate.error} />}
      {finish.error && <ErrorBox message={finish.error} />}

      {(seasons.data ?? []).map((season) => (
        <Card key={season.id}>
          <div className="flex items-start justify-between gap-3">
            <div>
              <h3 className="text-base font-semibold text-slate-900">{season.name}</h3>
              <p className="text-xs text-slate-500">
                {formatDate(season.startsOn)} a {formatDate(season.endsOn)} · {season.publishedRoundCount} de{' '}
                {season.roundCount} rodadas publicadas
              </p>
            </div>

            <Badge tone={season.status === 'Active' ? 'success' : season.status === 'Finished' ? 'neutral' : 'info'}>
              {season.status === 'Active' ? 'Ativa' : season.status === 'Finished' ? 'Encerrada' : 'Rascunho'}
            </Badge>
          </div>

          {season.podium.length > 0 && (
            <ol className="mt-3 space-y-1 rounded-xl bg-amber-50 p-3 text-sm text-amber-900">
              {season.podium.map((entry) => (
                <li key={entry.position}>
                  {entry.position}º — <strong>{entry.displayName}</strong> ({entry.totalPoints} pts)
                </li>
              ))}
            </ol>
          )}

          {season.status !== 'Finished' && (
            <div className="mt-3 flex flex-wrap gap-2">
              {season.status !== 'Active' && (
                <Button variant="secondary" onClick={() => void activate.run(season.id)}>
                  Tornar ativa
                </Button>
              )}
              <Button
                variant="danger"
                onClick={() => {
                  if (window.confirm('Encerrar a temporada e congelar o pódio dos 3 primeiros?')) {
                    void finish.run(season.id)
                  }
                }}
              >
                Encerrar
              </Button>
              <a
                href={`/api/admin/seasons/${season.id}/export`}
                role="button"
                className="inline-flex min-h-11 items-center rounded-xl border border-slate-300 px-4 text-sm font-semibold text-slate-700"
              >
                Exportar CSV
              </a>
            </div>
          )}
        </Card>
      ))}
    </div>
  )
}
