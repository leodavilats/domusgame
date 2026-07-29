import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { Dashboard, MyAttemptSummary, RoundSummary } from '../api/types'
import { BookIcon, ChevronRightIcon, ClockIcon, FlameIcon, KeyIcon } from '../components/Icons'
import {
  Badge,
  Button,
  Callout,
  Card,
  EmptyState,
  ErrorBox,
  ProgressBar,
  SkeletonCard,
  StatTile,
} from '../components/ui'
import { formatCountdown, formatDateTime, formatWeekday, pluralize } from '../lib/format'
import { useSession } from '../auth/SessionContext'

export function HomePage() {
  const { me } = useSession()
  const navigate = useNavigate()
  const { data, loading, error, reload } = useApi<Dashboard>('/api/dashboard')
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  const firstName = me?.displayName.split(' ')[0]

  if (loading) {
    return (
      <div className="space-y-4">
        <Greeting name={firstName} subtitle="Carregando seu desafio..." />
        <div className="grid grid-cols-3 gap-2">
          {[0, 1, 2].map((index) => (
            <div key={index} className="h-[76px] rounded-2xl bg-slate-200/70" />
          ))}
        </div>
        <SkeletonCard lines={4} />
      </div>
    )
  }

  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  const { round, myAttempt, actions, stats } = data

  if (!data.room) {
    return (
      <div className="space-y-4">
        <Greeting name={firstName} subtitle="Falta um passo para você começar" />

        <EmptyState
          icon={<KeyIcon />}
          title="Você ainda não está em uma sala"
          description="Os desafios, o ranking e as pessoas ficam dentro da sala do seu GC. Use o código que o líder compartilhou para entrar."
          action={<Button size="lg" onClick={() => navigate('/sala')}>Tenho um código</Button>}
        />
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <Greeting
        name={firstName}
        subtitle={data.season ? data.season.name : 'Nenhuma temporada em andamento'}
      />

      <div className="grid grid-cols-3 gap-2">
        <StatTile label="Pontos" value={stats.seasonPoints} tone="brand" />
        <StatTile
          label="Posição"
          value={stats.position ? `${stats.position}º` : '—'}
          hint={stats.position ? `de ${stats.participantsCount}` : undefined}
        />
        <StatTile
          label="Sequência"
          value={
            <span className="inline-flex items-center gap-1">
              {stats.streak}
              {stats.streak > 0 ? <FlameIcon className="h-4 w-4 text-amber-500" /> : null}
            </span>
          }
          hint={stats.streak > 0 ? 'semanas seguidas' : undefined}
        />
      </div>

      {!data.season ? (
        <EmptyState
          title="Nenhuma temporada em andamento"
          description="Assim que o líder abrir uma nova temporada, o desafio aparece aqui."
        />
      ) : !round ? (
        <EmptyState
          icon={<ClockIcon />}
          title="A próxima rodada está sendo preparada"
          description="Volte em breve — você recebe o aviso no grupo quando ela abrir."
        />
      ) : (
        <RoundCard
          round={round}
          attempt={myAttempt ?? null}
          actions={actions}
          lessonTitle={data.lessonTitle}
          lessonReference={data.lessonReference}
          now={now}
        />
      )}

      {data.nextRoundOpensAt && round?.availability !== 'Scheduled' && (
        <Callout tone="neutral">
          Próxima rodada abre {formatWeekday(data.nextRoundOpensAt)} —{' '}
          <strong className="nums">{formatCountdown(data.nextRoundOpensAt, now)}</strong>.
        </Callout>
      )}
    </div>
  )
}

function Greeting({ name, subtitle }: { name?: string; subtitle: string }) {
  return (
    <header>
      <h1 className="text-2xl font-bold tracking-tight text-slate-900">
        Olá, {name ?? 'tudo bem'}!
      </h1>
      <p className="mt-0.5 text-sm text-slate-500">{subtitle}</p>
    </header>
  )
}

function RoundCard({
  round,
  attempt,
  actions,
  lessonTitle,
  lessonReference,
  now,
}: {
  round: RoundSummary
  attempt: MyAttemptSummary | null
  actions: Dashboard['actions']
  lessonTitle?: string | null
  lessonReference?: string | null
  now: number
}) {
  const navigate = useNavigate()
  const open = round.availability === 'Open'

  return (
    <Card elevated className="animate-rise overflow-hidden" padded={false}>
      <div
        className={`px-4 py-4 sm:px-5 ${
          open ? 'bg-gradient-to-br from-brand-600 to-brand-700 text-white' : 'bg-slate-50'
        }`}
      >
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p
              className={`text-xs font-semibold uppercase tracking-wide ${
                open ? 'text-brand-100' : 'text-slate-500'
              }`}
            >
              Semana {round.weekNumber}
            </p>
            <h2
              className={`mt-0.5 text-lg font-bold leading-snug ${open ? 'text-white' : 'text-slate-900'}`}
            >
              {round.title}
            </h2>
            {lessonReference ? (
              <p className={`mt-0.5 text-sm ${open ? 'text-brand-100' : 'text-slate-500'}`}>
                {lessonReference}
              </p>
            ) : null}
          </div>

          <AvailabilityBadge availability={round.availability} onDark={open} />
        </div>

        <p className={`mt-3 text-sm ${open ? 'text-brand-50' : 'text-slate-600'}`}>
          {round.availability === 'Scheduled' && (
            <>
              Abre {formatWeekday(round.opensAt)} — faltam{' '}
              <strong className="nums">{formatCountdown(round.opensAt, now)}</strong>
            </>
          )}
          {open && (
            <>
              Fecha {formatWeekday(round.closesAt)} — resta{' '}
              <strong className="nums">{formatCountdown(round.closesAt, now)}</strong>
            </>
          )}
          {round.availability === 'Closed' && <>Encerrada em {formatDateTime(round.closesAt)}</>}
        </p>
      </div>

      <div className="space-y-4 p-4 sm:p-5">
        <p className="text-sm text-slate-500">
          {pluralize(round.questionCount, 'pergunta', 'perguntas')} · {round.questionTimeLimitSeconds}s
          por pergunta · até <span className="nums">{round.maxPoints}</span> pontos
        </p>

        {attempt?.status === 'InProgress' && (
          <div>
            <div className="mb-1.5 flex items-baseline justify-between text-sm">
              <span className="font-semibold text-slate-700">Tentativa em andamento</span>
              <span className="nums text-slate-500">
                {attempt.answeredCount}/{attempt.questionCount}
              </span>
            </div>
            <ProgressBar
              value={attempt.answeredCount}
              max={attempt.questionCount}
              label="Perguntas respondidas"
            />
          </div>
        )}

        {attempt?.status === 'Completed' && attempt.totalPoints != null && (
          <Callout tone="success" title={`Você fez ${attempt.totalPoints} pontos`}>
            {attempt.correctCount != null && (
              <>
                {attempt.correctCount} de {attempt.questionCount} acertos
              </>
            )}
            {attempt.position ? ` · ${attempt.position}º lugar na semana` : ''}
            {open && '. O ranking sai quando a rodada encerrar.'}
          </Callout>
        )}

        <div className="space-y-2">
          {actions.canStart && (
            <Button size="lg" full onClick={() => navigate(`/rodadas/${round.id}/quiz`)}>
              Responder o desafio
            </Button>
          )}

          {actions.canResume && (
            <Button size="lg" full onClick={() => navigate(`/rodadas/${round.id}/quiz`)}>
              Continuar de onde parei
            </Button>
          )}

          {actions.canReview && (
            <Button
              size="lg"
              full
              variant={attempt ? 'secondary' : 'primary'}
              onClick={() => navigate(`/rodadas/${round.id}/revisao`)}
            >
              Ver gabarito e explicações
            </Button>
          )}

          <div className="flex flex-col divide-y divide-slate-100">
            {lessonTitle && (
              <QuickLink
                to={`/rodadas/${round.id}/licao`}
                icon={<BookIcon className="h-5 w-5" />}
                title="Ler a lição da semana"
                detail={lessonTitle}
              />
            )}

            {attempt && (
              <QuickLink
                to={`/tentativas/${attempt.attemptId}/resultado`}
                icon={<TrophyMini />}
                title="Meu resultado"
                detail={
                  attempt.totalPoints != null
                    ? `${attempt.totalPoints} pontos`
                    : 'Tentativa em andamento'
                }
              />
            )}
          </div>
        </div>
      </div>
    </Card>
  )
}

function QuickLink({
  to,
  icon,
  title,
  detail,
}: {
  to: string
  icon: React.ReactNode
  title: string
  detail?: string
}) {
  return (
    <Link
      to={to}
      className="-mx-2 flex min-h-14 items-center gap-3 rounded-xl px-2 transition hover:bg-slate-50"
    >
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-slate-100 text-slate-500">
        {icon}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-semibold text-slate-800">{title}</span>
        {detail ? <span className="block truncate text-xs text-slate-500">{detail}</span> : null}
      </span>
      <ChevronRightIcon className="h-5 w-5 shrink-0 text-slate-400" />
    </Link>
  )
}

function TrophyMini() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-5 w-5">
      <path d="M7 4h10v4a5 5 0 0 1-10 0z" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M12 13v4M8.5 21h7" strokeLinecap="round" />
    </svg>
  )
}

export function AvailabilityBadge({
  availability,
  onDark = false,
}: {
  availability: string
  onDark?: boolean
}) {
  const map: Record<string, { label: string; tone: 'neutral' | 'success' | 'warning' | 'info' }> = {
    Draft: { label: 'Rascunho', tone: 'neutral' },
    Scheduled: { label: 'Agendada', tone: 'info' },
    Open: { label: 'Aberta', tone: 'success' },
    Closed: { label: 'Encerrada', tone: 'neutral' },
  }

  const item = map[availability] ?? map.Draft

  if (onDark) {
    return (
      <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full bg-white/15 px-2.5 py-1 text-xs font-semibold text-white ring-1 ring-inset ring-white/25">
        <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-emerald-300" />
        {item.label}
      </span>
    )
  }

  return (
    <Badge tone={item.tone} dot={availability === 'Open'}>
      {item.label}
    </Badge>
  )
}
