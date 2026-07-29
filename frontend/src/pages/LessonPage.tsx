import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { RoundDetail } from '../api/types'
import { Button, Card, EmptyState, ErrorBox, PageTitle, Spinner } from '../components/ui'
import { Markdown } from '../lib/markdown'
import { formatDateTime } from '../lib/format'

export function LessonPage() {
  const { roundId } = useParams<{ roundId: string }>()
  const navigate = useNavigate()
  const { data, loading, error, reload } = useApi<RoundDetail>(`/api/rounds/${roundId}`)

  if (loading) return <Spinner />
  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  if (!data.lesson) {
    return (
      <EmptyState
        title="A lição ainda não esta disponível"
        description={`Ela é liberada quando a rodada abrir, em ${formatDateTime(data.round.opensAt)}.`}
      />
    )
  }

  return (
    <div className="space-y-4">
      <PageTitle subtitle={`Semana ${data.round.weekNumber} · ${data.lesson.scriptureReference}`}>
        {data.lesson.title}
      </PageTitle>

      <Card>
        <Markdown content={data.lesson.content} />

        {data.lesson.externalUrl ? (
          <a
            href={data.lesson.externalUrl}
            target="_blank"
            rel="noreferrer noopener"
            className="mt-4 inline-block text-sm font-semibold text-brand-600 underline"
          >
            Material complementar
          </a>
        ) : null}
      </Card>

      <div className="flex flex-wrap gap-2">
        {data.round.availability === 'Open' && !data.myAttempt && (
          <Button onClick={() => navigate(`/rodadas/${data.round.id}/quiz`)}>Responder o desafio</Button>
        )}
        {data.round.availability === 'Closed' && (
          <Button variant="secondary" onClick={() => navigate(`/rodadas/${data.round.id}/revisao`)}>
            Ver gabarito
          </Button>
        )}
        <Button variant="ghost" onClick={() => navigate('/')}>
          Voltar
        </Button>
      </div>
    </div>
  )
}
