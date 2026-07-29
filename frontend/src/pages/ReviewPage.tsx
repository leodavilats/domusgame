import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { AnswerOutcome, ReviewQuestion, RoundReview } from '../api/types'
import { CheckIcon } from '../components/Icons'
import {
  Badge,
  Button,
  Card,
  ErrorBox,
  PageTitle,
  SkeletonCard,
  StatTile,
} from '../components/ui'
import { formatDuration } from '../lib/format'

const outcomes: Record<AnswerOutcome, { label: string; tone: 'success' | 'danger' | 'warning' | 'neutral' }> = {
  Correct: { label: 'Acertou', tone: 'success' },
  Incorrect: { label: 'Errou', tone: 'danger' },
  Blank: { label: 'Em branco', tone: 'warning' },
  TimedOut: { label: 'Tempo esgotado', tone: 'warning' },
  Pending: { label: 'Não respondida', tone: 'neutral' },
}

const letters = ['A', 'B', 'C', 'D', 'E']

export function ReviewPage() {
  const { roundId } = useParams<{ roundId: string }>()
  const navigate = useNavigate()
  const { data, loading, error, reload } = useApi<RoundReview>(`/api/rounds/${roundId}/review`)

  if (loading) {
    return (
      <div className="space-y-4">
        <SkeletonCard lines={2} />
        <SkeletonCard lines={5} />
      </div>
    )
  }

  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  return (
    <div className="space-y-4">
      <PageTitle subtitle={`Semana ${data.round.weekNumber} · ${data.lesson.scriptureReference}`}>
        {data.round.title}
      </PageTitle>

      <Card>
        <dl className="grid grid-cols-3 gap-2">
          <StatTile label="Pontos" value={`${data.totalPoints}/${data.maxPoints}`} tone="brand" />
          <StatTile label="Acertos" value={`${data.correctCount}/${data.questions.length}`} />
          <StatTile label="Tempo" value={formatDuration(data.totalTimeMs)} />
        </dl>
      </Card>

      <ol className="space-y-3">
        {data.questions.map((question) => (
          <li key={question.questionId}>
            <QuestionReview question={question} />
          </li>
        ))}
      </ol>

      <div className="flex gap-2">
        <Button variant="secondary" full onClick={() => navigate(`/ranking?rodada=${data.round.id}`)}>
          Ranking da semana
        </Button>
        <Button variant="ghost" full onClick={() => navigate('/')}>
          Início
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
        <h2 className="text-base font-semibold leading-snug text-slate-900">
          <span className="nums text-slate-400">{question.order}.</span> {question.text}
        </h2>
        <Badge tone={outcome.tone}>{outcome.label}</Badge>
      </div>

      {question.mediaType === 'Image' && question.mediaUrl && (
        <img
          src={question.mediaUrl}
          alt=""
          className="mt-3 max-h-56 w-full rounded-xl bg-slate-50 object-contain"
          loading="lazy"
        />
      )}

      {question.mediaType === 'Audio' && question.mediaUrl && (
        <audio controls src={question.mediaUrl} className="mt-3 w-full" />
      )}

      <ul className="mt-4 space-y-2">
        {question.options.map((option, index) => {
          const chosen = option.id === question.selectedOptionId

          const style = option.isCorrect
            ? 'border-emerald-300 bg-emerald-50'
            : chosen
              ? 'border-red-300 bg-red-50'
              : 'border-slate-200 bg-surface'

          const letterStyle = option.isCorrect
            ? 'bg-emerald-600 text-white'
            : chosen
              ? 'bg-red-600 text-white'
              : 'bg-slate-100 text-slate-500'

          return (
            <li
              key={option.id}
              className={`flex items-center gap-3 rounded-xl border px-3 py-2.5 ${style}`}
            >
              <span
                aria-hidden="true"
                className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-lg text-xs font-bold ${letterStyle}`}
              >
                {option.isCorrect ? <CheckIcon className="h-4 w-4" /> : (letters[index] ?? index + 1)}
              </span>

              <span
                className={`flex-1 text-sm leading-relaxed ${
                  option.isCorrect
                    ? 'font-semibold text-emerald-900'
                    : chosen
                      ? 'text-red-900'
                      : 'text-slate-600'
                }`}
              >
                {option.text}
              </span>

              {chosen && (
                <span
                  className={`shrink-0 text-[11px] font-semibold uppercase tracking-wide ${
                    option.isCorrect ? 'text-emerald-700' : 'text-red-700'
                  }`}
                >
                  sua resposta
                </span>
              )}
            </li>
          )
        })}
      </ul>

      {question.explanation ? (
        <div className="mt-3 rounded-xl border border-slate-200 bg-slate-50 p-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Por quê</p>
          <p className="mt-1 text-sm leading-relaxed text-slate-700">{question.explanation}</p>
        </div>
      ) : null}

      <p className="nums mt-3 text-xs text-slate-500">
        {question.points} ponto(s) · {formatDuration(question.elapsedMs)}
      </p>
    </Card>
  )
}
