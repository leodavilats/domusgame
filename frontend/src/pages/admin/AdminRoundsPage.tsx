import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminRound, AdminRoundListItem, AdminSeason } from '../../api/types'
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
  Select,
  SkeletonCard,
} from '../../components/ui'
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

  const rounds = useApi<AdminRoundListItem[]>(
    seasonId ? `/api/admin/rounds?seasonId=${seasonId}` : null,
    [seasonId],
  )

  const suggestion = suggestedWindow()
  const [creating, setCreating] = useState(false)
  const [weekNumber, setWeekNumber] = useState(1)
  const [title, setTitle] = useState('')
  const [opensAt, setOpensAt] = useState(suggestion.opensAt)
  const [closesAt, setClosesAt] = useState(suggestion.closesAt)
  const [pointsPerCorrectAnswer, setPoints] = useState(10)
  const [maxSpeedBonus, setBonus] = useState(5)
  const [questionTimeLimitSeconds, setTimeLimit] = useState(45)

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

  const list = rounds.data ?? []
  const showForm = creating || (!rounds.loading && seasonId !== '' && list.length === 0)

  return (
    <div className="space-y-4">
      <PageTitle
        subtitle="Cada semana é uma rodada: lição, perguntas e janela"
        actions={
          seasonId && !showForm ? (
            <Button size="sm" onClick={() => setCreating(true)}>
              Nova rodada
            </Button>
          ) : undefined
        }
      >
        Rodadas
      </PageTitle>

      {seasons.loading && <SkeletonCard lines={2} />}
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
        <EmptyState
          title="Crie uma temporada primeiro"
          description="A rodada precisa pertencer a uma temporada."
          action={<Button onClick={() => navigate('/admin/temporadas')}>Ir para Temporadas</Button>}
        />
      )}

      {showForm && (
        <Card elevated className="animate-rise">
          <SectionTitle hint="A rodada nasce como rascunho: nada aparece para o GC até você publicar.">
            Nova rodada
          </SectionTitle>

          <form
            className="space-y-4"
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
                <Field label="Título">
                  <Input
                    required
                    maxLength={120}
                    value={title}
                    onChange={(event) => setTitle(event.target.value)}
                  />
                </Field>
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
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
              <Field label="Bônus máx.">
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

            <div className="flex gap-2">
              <Button type="submit" loading={create.loading}>
                Criar e editar
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

      {rounds.loading && <SkeletonCard lines={3} />}
      {rounds.error && <ErrorBox message={rounds.error} onRetry={rounds.reload} />}
      {duplicate.error && <ErrorBox message={duplicate.error} />}

      <ul className="space-y-3">
        {list.map((item) => (
          <li key={item.round.id}>
            <Card>
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                    Semana {item.round.weekNumber}
                  </p>
                  <h3 className="truncate text-base font-semibold text-slate-900">
                    {item.round.title}
                  </h3>
                  <p className="nums mt-1 text-xs text-slate-500">
                    {formatDateTime(item.round.opensAt)} → {formatDateTime(item.round.closesAt)}
                  </p>
                  <p className="nums mt-0.5 text-xs text-slate-500">
                    {item.round.questionCount} perguntas · {item.attemptCount} participações
                  </p>
                </div>

                {item.status === 'Draft' ? (
                  <Badge tone="neutral">Rascunho</Badge>
                ) : (
                  <AvailabilityBadge availability={item.round.availability} />
                )}
              </div>

              <div className="mt-4 flex flex-wrap items-center gap-2">
                <Link
                  to={`/admin/rodadas/${item.round.id}`}
                  className="inline-flex min-h-9 items-center rounded-xl bg-brand-600 px-3 text-sm font-semibold text-white transition hover:bg-brand-700"
                >
                  {item.canEdit ? 'Editar' : 'Ver'}
                </Link>

                {item.status === 'Published' && (
                  <Link
                    to={`/admin/rodadas/${item.round.id}/estatisticas`}
                    className="inline-flex min-h-9 items-center rounded-xl bg-slate-100 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-200"
                  >
                    Estatísticas
                  </Link>
                )}

                <Button
                  size="sm"
                  variant="ghost"
                  loading={duplicate.loading}
                  onClick={() => void duplicate.run(item.round.id, item.round.weekNumber + 1)}
                >
                  Duplicar
                </Button>
              </div>
            </Card>
          </li>
        ))}
      </ul>
    </div>
  )
}
