import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { RoundDetail } from '../api/types'
import { ArrowLeftIcon, ClockIcon } from '../components/Icons'
import { Button, Card, EmptyState, ErrorBox, PageTitle, SkeletonCard } from '../components/ui'
import { Markdown } from '../lib/markdown'
import { formatDateTime } from '../lib/format'

export function LessonPage() {
  const { roundId } = useParams<{ roundId: string }>()
  const navigate = useNavigate()
  const { data, loading, error, reload } = useApi<RoundDetail>(`/api/rounds/${roundId}`)

  if (loading) return <SkeletonCard lines={6} />
  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  if (!data.lesson) {
    return (
      <div className="space-y-4">
        <BackLink onClick={() => navigate(-1)} />
        <EmptyState
          icon={<ClockIcon />}
          title="A lição ainda não está disponível"
          description={`Ela é liberada quando a rodada abrir, em ${formatDateTime(data.round.opensAt)}.`}
        />
      </div>
    )
  }

  const canAnswer = data.round.availability === 'Open' && !data.myAttempt

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <BackLink onClick={() => navigate(-1)} />

      <PageTitle subtitle={`Semana ${data.round.weekNumber} · ${data.lesson.scriptureReference}`}>
        {data.lesson.title}
      </PageTitle>

      <Card elevated>
        <div className="text-[15px] leading-relaxed">
          <Markdown content={data.lesson.content} />
        </div>

        {data.lesson.externalUrl ? (
          <a
            href={data.lesson.externalUrl}
            target="_blank"
            rel="noreferrer noopener"
            className="mt-5 inline-flex min-h-11 items-center gap-1.5 text-sm font-semibold text-brand-700 underline decoration-brand-300 underline-offset-4"
          >
            Material complementar
            <span aria-hidden="true">↗</span>
          </a>
        ) : null}
      </Card>

      <div className="sticky bottom-24 space-y-2 md:bottom-4">
        {canAnswer && (
          <Button size="lg" full className="shadow-float" onClick={() => navigate(`/rodadas/${data.round.id}/quiz`)}>
            Responder o desafio
          </Button>
        )}

        {data.round.availability === 'Closed' && (
          <Button
            size="lg"
            full
            variant="secondary"
            onClick={() => navigate(`/rodadas/${data.round.id}/revisao`)}
          >
            Ver gabarito
          </Button>
        )}
      </div>
    </div>
  )
}

function BackLink({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="-ml-2 inline-flex min-h-10 items-center gap-1.5 rounded-xl px-2 text-sm font-semibold text-slate-500 transition hover:bg-slate-100 hover:text-slate-700"
    >
      <ArrowLeftIcon className="h-5 w-5" />
      Voltar
    </button>
  )
}
