import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ApiError, api } from '../api/client'
import type { AttemptQuestion, AttemptState, RoundDetail, SubmitAnswerResponse } from '../api/types'
import { Button, Card, ErrorBox, Spinner } from '../components/ui'

type Stage = 'loading' | 'rules' | 'question' | 'error'

export function QuizPage() {
  const { roundId } = useParams<{ roundId: string }>()
  const navigate = useNavigate()

  const [stage, setStage] = useState<Stage>('loading')
  const [round, setRound] = useState<RoundDetail | null>(null)
  const [attemptId, setAttemptId] = useState<string | null>(null)
  const [question, setQuestion] = useState<AttemptQuestion | null>(null)
  const [selected, setSelected] = useState<string | null>(null)
  const [remaining, setRemaining] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [sending, setSending] = useState(false)

  const clockOffset = useRef(0)
  const submitting = useRef(false)

  const applyState = useCallback(
    (state: AttemptState) => {
      setAttemptId(state.attemptId)

      if (state.status === 'Completed' || !state.currentQuestion) {
        navigate(`/tentativas/${state.attemptId}/resultado`, { replace: true })
        return
      }

      applyQuestion(state.currentQuestion)
    },
    [navigate],
  )

  function applyQuestion(next: AttemptQuestion) {
    clockOffset.current = new Date(next.serverNow).getTime() - Date.now()
    setQuestion(next)
    setSelected(null)
    submitting.current = false
    setSending(false)
    setStage('question')
  }

  useEffect(() => {
    if (!roundId) return

    let cancelled = false

    async function load() {
      try {
        const detail = await api.get<RoundDetail>(`/api/rounds/${roundId}`)
        if (cancelled) return
        setRound(detail)

        try {
          const state = await api.get<AttemptState>(`/api/rounds/${roundId}/attempts/current`)
          if (cancelled) return
          applyState(state)
        } catch (attemptError) {
          if (cancelled) return

          if (attemptError instanceof ApiError && attemptError.status === 404) {
            setStage('rules')
            return
          }

          throw attemptError
        }
      } catch (caught) {
        if (cancelled) return
        setError(caught instanceof ApiError ? caught.message : 'Erro inesperado.')
        setStage('error')
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [roundId, applyState])

  const submit = useCallback(
    async (optionId: string | null) => {
      if (!attemptId || !question || submitting.current) return

      submitting.current = true
      setSending(true)

      try {
        const response = await api.post<SubmitAnswerResponse>(`/api/attempts/${attemptId}/answers`, {
          questionId: question.id,
          selectedOptionId: optionId,
        })

        if (response.attemptFinished || !response.nextQuestion) {
          navigate(`/tentativas/${attemptId}/resultado`, { replace: true })
          return
        }

        applyQuestion(response.nextQuestion)
      } catch (caught) {
        submitting.current = false
        setSending(false)
        setError(caught instanceof ApiError ? caught.message : 'Erro ao enviar a resposta.')
      }
    },
    [attemptId, question, navigate],
  )

  useEffect(() => {
    if (stage !== 'question' || !question) return

    const deadline = new Date(question.deadlineAt).getTime()

    function tick() {
      const serverNow = Date.now() + clockOffset.current
      const secondsLeft = Math.max(0, Math.ceil((deadline - serverNow) / 1000))
      setRemaining(secondsLeft)

      if (secondsLeft === 0 && !submitting.current) {
        void submit(null)
      }
    }

    tick()
    const timer = window.setInterval(tick, 250)
    return () => window.clearInterval(timer)
  }, [stage, question, submit])

  async function startAttempt() {
    if (!roundId) return

    setError(null)
    setStage('loading')

    try {
      applyState(await api.post<AttemptState>(`/api/rounds/${roundId}/attempts`))
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Não foi possível iniciar.')
      setStage('rules')
    }
  }

  if (stage === 'loading') return <Spinner label="Preparando o desafio..." />

  if (stage === 'error') {
    return (
      <div className="space-y-3">
        <ErrorBox message={error ?? 'Erro inesperado.'} />
        <Button variant="secondary" onClick={() => navigate('/')}>
          Voltar ao início
        </Button>
      </div>
    )
  }

  if (stage === 'rules') {
    return (
      <RulesCard
        round={round}
        onStart={startAttempt}
        onCancel={() => navigate('/', { replace: true })}
        error={error}
      />
    )
  }

  if (!question) return null

  const total = question.totalQuestions
  const progress = Math.round(((question.order - 1) / total) * 100)
  const timeRatio = Math.min(100, (remaining / question.timeLimitSeconds) * 100)
  const urgent = remaining <= 10

  return (
    <div className="flex min-h-[80dvh] flex-col gap-4">
      <div className="space-y-2">
        <div className="flex items-center justify-between text-sm">
          <span className="font-semibold text-slate-700">
            Pergunta {question.order} de {total}
          </span>
          <span
            className={`rounded-full px-3 py-1 font-bold tabular-nums ${
              urgent ? 'bg-red-100 text-red-700' : 'bg-slate-200 text-slate-700'
            }`}
            role="timer"
            aria-live="off"
          >
            {remaining}s
          </span>
        </div>

        <div
          className="h-1.5 overflow-hidden rounded-full bg-slate-200"
          role="progressbar"
          aria-label="Progresso no desafio"
          aria-valuenow={question.order - 1}
          aria-valuemin={0}
          aria-valuemax={total}
        >
          <div className="h-full rounded-full bg-brand-500 transition-all" style={{ width: `${progress}%` }} />
        </div>

        <div className="h-1.5 overflow-hidden rounded-full bg-slate-200">
          <div
            className={`h-full rounded-full ${urgent ? 'bg-red-500' : 'bg-slate-400'}`}
            style={{ width: `${timeRatio}%` }}
          />
        </div>
      </div>

      <Card className="flex-1">
        <h2 className="text-lg font-semibold text-slate-900">{question.text}</h2>

        {question.mediaType === 'Image' && question.mediaUrl && (
          <img
            src={question.mediaUrl}
            alt=""
            className="mt-3 max-h-64 w-full rounded-xl object-contain"
            loading="lazy"
          />
        )}

        {question.mediaType === 'Audio' && question.mediaUrl && (
          <audio controls src={question.mediaUrl} className="mt-3 w-full">
            Seu navegador não suporta audio.
          </audio>
        )}

        <ul className="mt-4 space-y-2">
          {question.options.map((option) => {
            const active = selected === option.id
            return (
              <li key={option.id}>
                <button
                  type="button"
                  onClick={() => setSelected(option.id)}
                  disabled={sending}
                  className={`min-h-14 w-full rounded-xl border px-4 py-3 text-left text-sm transition ${
                    active
                      ? 'border-brand-500 bg-brand-50 font-semibold text-brand-800'
                      : 'border-slate-300 bg-white text-slate-700'
                  }`}
                >
                  {option.text}
                </button>
              </li>
            )
          })}
        </ul>
      </Card>

      {error ? <ErrorBox message={error} /> : null}

      <div className="sticky bottom-4">
        <Button full loading={sending} disabled={!selected} onClick={() => void submit(selected)}>
          {question.order === total ? 'Responder e finalizar' : 'Confirmar e avancar'}
        </Button>
        <p className="mt-2 text-center text-xs text-slate-500">
          Não da para voltar depois de confirmar.
        </p>
      </div>
    </div>
  )
}

function RulesCard({
  round,
  onStart,
  onCancel,
  error,
}: {
  round: RoundDetail | null
  onStart: () => void
  onCancel: () => void
  error: string | null
}) {
  const summary = round?.round

  return (
    <div className="space-y-4">
      <Card>
        <h1 className="text-xl font-bold text-slate-900">
          {summary ? `Semana ${summary.weekNumber}: ${summary.title}` : 'Desafio da semana'}
        </h1>

        <ul className="mt-4 space-y-2 text-sm text-slate-700">
          <li>• Você tem <strong>uma única tentativa</strong> nesta rodada.</li>
          <li>
            • Sao <strong>{summary?.questionCount ?? '-'} perguntas</strong>, uma de cada vez, sem voltar.
          </li>
          <li>
            • Cada pergunta tem <strong>{summary?.questionTimeLimitSeconds ?? '-'} segundos</strong>. Responder
            rapido vale até <strong>{summary?.maxSpeedBonus ?? 0} pontos extras</strong>.
          </li>
          <li>• Se o tempo acabar, a pergunta vale zero.</li>
          <li>• Precisa de internet estavel: o tempo e contado no servidor.</li>
          <li>• O gabarito aparece quando a rodada encerrar.</li>
        </ul>
      </Card>

      {error ? <ErrorBox message={error} /> : null}

      <div className="space-y-2">
        <Button full onClick={onStart}>
          Estou pronto, começar
        </Button>

        <Button full variant="secondary" onClick={onCancel}>
          Ainda não — voltar
        </Button>
      </div>
    </div>
  )
}
