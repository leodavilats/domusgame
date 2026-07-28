import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminRound, AdminRoundListItem, AdminSeason } from '../../api/types'
import { Badge, Button, Card, EmptyState, ErrorBox, Field, Input, PageTitle, Select, Spinner } from '../../components/ui'
import { formatDateTime, fromLocalInput, suggestedWindow } from '../../lib/format'
import { AvailabilityBadge } from '../HomePage'

export function AdminRoundsPage() {
  const navigate = useNavigate()
  const seasons = useApi<AdminSeason[]>('/api/admin/seasons')
  const [seasonId, setSeasonId] = useState<string>('')

  useEffect(() => {
    if (seasonId || !seasons.data) return
    const active = seasons.data.find((season) => season.status === 'Active') ?? seasons.data[0]
    if (active) setSeasonId(active.id)
  }, [seasons.data, seasonId])

  const rounds = useApi<AdminRoundListItem[]>(seasonId ? `/api/admin/rounds?seasonId=${seasonId}` : null, [seasonId])

  const suggestion = suggestedWindow()
  const [weekNumber, setWeekNumber] = useState(1)
  const [title, setTitle] = useState('')
  const [opensAt, setOpensAt] = useState(suggestion.opensAt)
  const [closesAt, setClosesAt] = useState(suggestion.closesAt)
  const [pointsPerCorrectAnswer, setPoints] = useState(10)
  const [maxSpeedBonus, setBonus] = useState(5)
  const [questionTimeLimitSeconds, setTimeLimit] = useState(45)

  // Sugere a proxima semana livre.
  useEffect(() => {
    if (!rounds.data || rounds.data.length === 0) return
    setWeekNumber(Math.max(...rounds.data.map((item) => item.round.weekNumber)) + 1)
  }, [rounds.data])

  const create = useMutation(async () => {
    const created = await api.post<AdminRound>('/api/admin/rounds', {
      seasonId,
      weekNumber,
      title,
      opensAt: fromLocalInput(opensAt),
      closesAt: fromLocalInput(closesAt),
      pointsPerCorrectAnswer,
      maxSpeedBonus,
      questionTimeLimitSeconds,
    })

    navigate(`/admin/rodadas/${created.round.id}`)
    return created
  })

  const duplicate = useMutation(async (roundId: string, nextWeek: number) => {
    const window = suggestedWindow()

    const created = await api.post<AdminRound>(`/api/admin/rounds/${roundId}/duplicate`, {
      weekNumber: nextWeek,
      opensAt: fromLocalInput(window.opensAt),
      closesAt: fromLocalInput(window.closesAt),
    })

    navigate(`/admin/rodadas/${created.round.id}`)
    return created
  })

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Cada semana e uma rodada: licao, perguntas e janela">Rodadas</PageTitle>

      {seasons.loading && <Spinner />}
      {seasons.error && <ErrorBox message={seasons.error} onRetry={seasons.reload} />}

      {seasons.data && seasons.data.length > 0 && (
        <Field label="Temporada">
          <Select value={seasonId} onChange={(event) => setSeasonId(event.target.value)}>
            {seasons.data.map((season) => (
              <option key={season.id} value={season.id}>
                {season.name} {season.status === 'Active' ? '(ativa)' : ''}
              </option>
            ))}
          </Select>
        </Field>
      )}

      {seasons.data && seasons.data.length === 0 && (
        <EmptyState title="Crie uma temporada primeiro" description="Va em Temporadas e crie a primeira." />
      )}

      {seasonId && (
        <Card>
          <h2 className="mb-3 text-sm font-semibold text-slate-700">Nova rodada (rascunho)</h2>

          <form
            className="space-y-3"
            onSubmit={(event) => {
              event.preventDefault()
              void create.run()
            }}
          >
            {create.error ? <ErrorBox message={create.error} /> : null}

            <div className="grid grid-cols-3 gap-3">
              <Field label="Semana">
                <Input
                  type="number"
                  min={1}
                  required
                  value={weekNumber}
                  onChange={(event) => setWeekNumber(Number(event.target.value))}
                />
              </Field>
              <div className="col-span-2">
                <Field label="Titulo">
                  <Input required maxLength={120} value={title} onChange={(event) => setTitle(event.target.value)} />
                </Field>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <Field label="Abre em">
                <Input
                  type="datetime-local"
                  required
                  value={opensAt}
                  onChange={(event) => setOpensAt(event.target.value)}
                />
              </Field>
              <Field label="Fecha em">
                <Input
                  type="datetime-local"
                  required
                  value={closesAt}
                  onChange={(event) => setClosesAt(event.target.value)}
                />
              </Field>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <Field label="Pts/acerto">
                <Input
                  type="number"
                  min={1}
                  max={100}
                  value={pointsPerCorrectAnswer}
                  onChange={(event) => setPoints(Number(event.target.value))}
                />
              </Field>
              <Field label="Bonus max.">
                <Input
                  type="number"
                  min={0}
                  max={50}
                  value={maxSpeedBonus}
                  onChange={(event) => setBonus(Number(event.target.value))}
                />
              </Field>
              <Field label="Seg./pergunta">
                <Input
                  type="number"
                  min={10}
                  max={300}
                  value={questionTimeLimitSeconds}
                  onChange={(event) => setTimeLimit(Number(event.target.value))}
                />
              </Field>
            </div>

            <Button type="submit" loading={create.loading}>
              Criar e editar
            </Button>
          </form>
        </Card>
      )}

      {rounds.loading && <Spinner />}
      {rounds.error && <ErrorBox message={rounds.error} onRetry={rounds.reload} />}
      {duplicate.error && <ErrorBox message={duplicate.error} />}

      {(rounds.data ?? []).map((item) => (
        <Card key={item.round.id}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                Semana {item.round.weekNumber}
              </p>
              <h3 className="truncate text-base font-semibold text-slate-900">{item.round.title}</h3>
              <p className="mt-0.5 text-xs text-slate-500">
                {formatDateTime(item.round.opensAt)} → {formatDateTime(item.round.closesAt)}
              </p>
              <p className="mt-0.5 text-xs text-slate-500">
                {item.round.questionCount} perguntas · {item.attemptCount} participacoes
              </p>
            </div>

            <div className="flex shrink-0 flex-col items-end gap-1">
              {item.status === 'Draft' ? (
                <Badge tone="neutral">Rascunho</Badge>
              ) : (
                <AvailabilityBadge availability={item.round.availability} />
              )}
            </div>
          </div>

          <div className="mt-3 flex flex-wrap gap-3 text-sm font-semibold text-brand-600">
            <Link to={`/admin/rodadas/${item.round.id}`}>{item.canEdit ? 'Editar' : 'Ver'}</Link>
            {item.status === 'Published' && (
              <Link to={`/admin/rodadas/${item.round.id}/estatisticas`}>Estatisticas</Link>
            )}
            <button
              type="button"
              className="font-semibold text-slate-600"
              onClick={() => void duplicate.run(item.round.id, item.round.weekNumber + 1)}
            >
              Duplicar
            </button>
          </div>
        </Card>
      ))}
    </div>
  )
}
