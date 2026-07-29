import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ApiError, api } from '../api/client'
import type { AttemptQuestion, AttemptState, RoundDetail, SubmitAnswerResponse } from '../api/types'
import { ClockIcon } from '../components/Icons'
import { Button, Callout, Card, ErrorBox, ProgressBar, Spinner } from '../components/ui'

type Stage = 'loading' | 'rules' | 'question' | 'error'

const letters = ['A', 'B', 'C', 'D', 'E']

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

  // Atalhos de teclado: A-E (ou 1-5) escolhe, Enter confirma. Quem responde no notebook
  // deixa de precisar do mouse com o cronometro correndo.
  useEffect(() => {
    if (stage !== 'question' || !question) return

    const options = question.options

    function onKey(event: KeyboardEvent) {
      if (event.metaKey || event.ctrlKey || event.altKey) return

      const index = letters.indexOf(event.key.toUpperCase())
      const numeric = Number.parseInt(event.key, 10) - 1
      const target = index >= 0 ? index : Number.isNaN(numeric) ? -1 : numeric

      if (target >= 0 && target < options.length) {
        event.preventDefault()
        setSelected(options[target].id)
        return
      }

      if (event.key === 'Enter' && selected && !submitting.current) {
        event.preventDefault()
        void submit(selected)
      }
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [stage, question, selected, submit])

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
  const urgent = remaining <= 10
  const last = question.order === total

  return (
    <div className="flex min-h-[calc(100dvh-2rem)] flex-col gap-4">
      <div className="sticky top-0 z-10 -mx-4 space-y-2 bg-canvas/95 px-4 pb-3 pt-1 backdrop-blur">
        <div className="flex items-center justify-between gap-3">
          <span className="text-sm font-semibold text-slate-600">
            Pergunta <span className="nums text-slate-900">{question.order}</span> de{' '}
            <span className="nums">{total}</span>
          </span>

          <span
            role="timer"
            aria-live="off"
            className={`nums inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-bold transition-colors ${
              urgent ? 'bg-red-100 text-red-700 animate-urgent' : 'bg-slate-200/80 text-slate-700'
            }`}
          >
            <ClockIcon className="h-4 w-4" />
            {remaining}s
          </span>
        </div>

        <ProgressBar
          value={question.order - 1}
          max={total}
          label="Progresso no desafio"
          size="sm"
        />

        <ProgressBar
          value={remaining}
          max={question.timeLimitSeconds}
          label="Tempo restante nesta pergunta"
          tone={urgent ? 'danger' : 'neutral'}
          size="sm"
        />
      </div>

      <Card elevated className="flex-1 animate-rise">
        <h1 className="text-lg font-semibold leading-snug text-slate-900">{question.text}</h1>

        {question.mediaType === 'Image' && question.mediaUrl && (
          <img
            src={question.mediaUrl}
            alt=""
            className="mt-4 max-h-64 w-full rounded-xl bg-slate-50 object-contain"
            loading="lazy"
          />
        )}

        {question.mediaType === 'Audio' && question.mediaUrl && (
          <audio controls src={question.mediaUrl} className="mt-4 w-full">
            Seu navegador não suporta áudio.
          </audio>
        )}

        <div
          role="radiogroup"
          aria-label="Alternativas"
          className="mt-5 space-y-2.5"
        >
          {question.options.map((option, index) => {
            const active = selected === option.id

            return (
              <button
                key={option.id}
                type="button"
                role="radio"
                aria-checked={active}
                onClick={() => setSelected(option.id)}
                disabled={sending}
                className={`flex min-h-14 w-full items-center gap-3 rounded-xl border px-3 py-3 text-left transition ${
                  active
                    ? 'border-brand-500 bg-brand-50 ring-2 ring-brand-500/20'
                    : 'border-slate-200 bg-surface hover:border-slate-300 hover:bg-slate-50'
                } disabled:opacity-60`}
              >
                <span
                  aria-hidden="true"
                  className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-sm font-bold transition ${
                    active ? 'bg-brand-600 text-white' : 'bg-slate-100 text-slate-500'
                  }`}
                >
                  {letters[index] ?? index + 1}
                </span>

                <span
                  className={`text-sm leading-relaxed ${
                    active ? 'font-semibold text-brand-900' : 'text-slate-700'
                  }`}
                >
                  {option.text}
                </span>
              </button>
            )
          })}
        </div>

        <p className="mt-4 hidden text-xs text-slate-400 md:block">
          Atalhos: {letters.slice(0, question.options.length).join(', ')} para escolher · Enter para
          confirmar
        </p>
      </Card>

      {error ? <ErrorBox message={error} /> : null}

      <div className="sticky bottom-3 space-y-2 pb-[env(safe-area-inset-bottom)]">
        <Button
          size="lg"
          full
          loading={sending}
          disabled={!selected}
          onClick={() => void submit(selected)}
          className="shadow-float"
        >
          {sending ? 'Enviando...' : last ? 'Responder e finalizar' : 'Confirmar e avançar'}
        </Button>

        <p className="text-center text-xs text-slate-500">
          {selected ? 'Não dá para voltar depois de confirmar.' : 'Escolha uma alternativa para continuar.'}
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

  const rules = [
    { icon: '1', text: <>Você tem <strong>uma única tentativa</strong> nesta rodada.</> },
    {
      icon: '2',
      text: (
        <>
          São <strong>{summary?.questionCount ?? '—'} perguntas</strong>, uma de cada vez, sem voltar.
        </>
      ),
    },
    {
      icon: '3',
      text: (
        <>
          Cada pergunta tem <strong>{summary?.questionTimeLimitSeconds ?? '—'} segundos</strong>.
          Responder rápido vale até <strong>{summary?.maxSpeedBonus ?? 0} pontos extras</strong>.
        </>
      ),
    },
    { icon: '4', text: <>Se o tempo acabar, a pergunta vale zero.</> },
    { icon: '5', text: <>O tempo é contado no servidor — precisa de internet estável.</> },
    { icon: '6', text: <>O gabarito aparece quando a rodada encerrar.</> },
  ]

  return (
    <div className="mx-auto max-w-lg space-y-4">
      <Card elevated className="animate-rise">
        <p className="text-xs font-semibold uppercase tracking-wide text-brand-600">
          Semana {summary?.weekNumber ?? '—'}
        </p>
        <h1 className="mt-1 text-xl font-bold tracking-tight text-slate-900">
          {summary?.title ?? 'Desafio da semana'}
        </h1>

        <ul className="mt-5 space-y-3">
          {rules.map((rule) => (
            <li key={rule.icon} className="flex gap-3 text-sm leading-relaxed text-slate-700">
              <span
                aria-hidden="true"
                className="nums mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-slate-100 text-xs font-bold text-slate-500"
              >
                {rule.icon}
              </span>
              <span>{rule.text}</span>
            </li>
          ))}
        </ul>
      </Card>

      {error ? <ErrorBox message={error} /> : null}

      <Callout tone="warning">
        Ao começar, o cronômetro da primeira pergunta já está correndo.
      </Callout>

      <div className="space-y-2">
        <Button size="lg" full onClick={onStart}>
          Estou pronto, começar
        </Button>

        <Button size="lg" full variant="secondary" onClick={onCancel}>
          Ainda não — voltar
        </Button>
      </div>
    </div>
  )
}
