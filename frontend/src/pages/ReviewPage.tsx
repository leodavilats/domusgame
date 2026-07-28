import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { AnswerOutcome, ReviewQuestion, RoundReview } from '../api/types'
import { Badge, Button, Card, ErrorBox, PageTitle, Spinner } from '../components/ui'
import { formatDuration } from '../lib/format'

const outcomes: Record<AnswerOutcome, { label: string; tone: 'success' | 'danger' | 'warning' | 'neutral' }> = {
  Correct: { label: 'Acertou', tone: 'success' },
  Incorrect: { label: 'Errou', tone: 'danger' },
  Blank: { label: 'Em branco', tone: 'warning' },
  TimedOut: { label: 'Tempo esgotado', tone: 'warning' },
  Pending: { label: 'Não respondida', tone: 'neutral' },
}

export function ReviewPage() {
  const { roundId } = useParams<{ roundId: string }>()
  const navigate = useNavigate()
  const { data, loading, error, reload } = useApi<RoundReview>(`/api/rounds/${roundId}/review`)

  if (loading) return <Spinner />
  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  return (
    <div className="space-y-4">
      <PageTitle subtitle={`Semana ${data.round.weekNumber} · ${data.lesson.scriptureReference}`}>
        Gabarito: {data.round.title}
      </PageTitle>

      <Card>
        <p className="text-sm text-slate-600">
          Você fez <strong>{data.totalPoints}</strong> de {data.maxPoints} pontos, com{' '}
          <strong>{data.correctCount}</strong> acertos em {formatDuration(data.totalTimeMs)}.
        </p>
      </Card>

      {data.questions.map((question) => (
        <QuestionReview key={question.questionId} question={question} />
      ))}

      <div className="flex gap-2">
        <Button variant="secondary" onClick={() => navigate(`/ranking?rodada=${data.round.id}`)}>
          Ranking da semana
        </Button>
        <Button variant="ghost" onClick={() => navigate('/')}>
          Inicio
        </Button>
      </div>
    </div>
  )
}

function QuestionReview({ question }: { question: ReviewQuestion }) {
  const outcome = outcomes[question.outcome]

  return (
    <Card>
      <div className="flex items-start justify-between gap-3">
        <h2 className="text-base font-semibold text-slate-900">
          {question.order}. {question.text}
        </h2>
        <Badge tone={outcome.tone}>{outcome.label}</Badge>
      </div>

      {question.mediaType === 'Image' && question.mediaUrl && (
        <img src={question.mediaUrl} alt="" className="mt-3 max-h-56 w-full rounded-xl object-contain" />
      )}

      <ul className="mt-3 space-y-2">
        {question.options.map((option) => {
          const chosen = option.id === question.selectedOptionId

          const style = option.isCorrect
            ? 'border-emerald-400 bg-emerald-50 text-emerald-900'
            : chosen
              ? 'border-red-300 bg-red-50 text-red-900'
              : 'border-slate-200 bg-white text-slate-600'

          return (
            <li key={option.id} className={`rounded-xl border px-3 py-2 text-sm ${style}`}>
              <span>{option.text}</span>
              {option.isCorrect && <span className="ml-2 text-xs font-semibold">correta</span>}
              {chosen && !option.isCorrect && <span className="ml-2 text-xs font-semibold">sua resposta</span>}
            </li>
          )
        })}
      </ul>

      {question.explanation ? (
        <p className="mt-3 rounded-xl bg-slate-50 p-3 text-sm text-slate-600">{question.explanation}</p>
      ) : null}

      <p className="mt-2 text-xs text-slate-500">
        {question.points} ponto(s) · {formatDuration(question.elapsedMs)}
      </p>
    </Card>
  )
}
