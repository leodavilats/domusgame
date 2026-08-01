import { useEffect, useState } from 'react'
import confetti from 'canvas-confetti'
import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { AttemptResult } from '../api/types'
import { Button, Callout, Card, ErrorBox, Spinner, StatTile } from '../components/ui'
import { formatDuration } from '../lib/format'
import { buildScoreMessage, share } from '../lib/share'
import { useSession } from '../auth/SessionContext'

export function ResultPage() {
  const { attemptId } = useParams<{ attemptId: string }>()
  const navigate = useNavigate()
  const { me } = useSession()
  const { data, loading, error, reload } = useApi<AttemptResult>(`/api/attempts/${attemptId}/result`)
  const [shareFeedback, setShareFeedback] = useState<string | null>(null)

  if (loading) return <Spinner label="Calculando seu resultado..." />
  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  const percent = data.maxPoints === 0 ? 0 : Math.round((data.totalPoints / data.maxPoints) * 100)
  const accuracy = data.questionCount === 0 ? 0 : data.correctCount / data.questionCount

  const headline =
    accuracy === 1
      ? 'Perfeito!'
      : accuracy >= 0.7
        ? 'Muito bem!'
        : accuracy >= 0.4
          ? 'Bom esforço!'
          : 'Semana de aprender!'

  useEffect(() => {
    if (accuracy !== 1) return
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

    void confetti({ particleCount: 140, spread: 80, origin: { y: 0.6 } })
  }, [accuracy])

  async function onShare() {
    if (!data) return

    const result = await share({
      title: 'Desafio do GC',
      text: buildScoreMessage(me?.room?.name ?? 'GC Domus', data.round.weekNumber, data.totalPoints),
    })

    setShareFeedback(
      result === 'shared' ? null : result === 'copied' ? 'Texto copiado!' : 'Não foi possível compartilhar.',
    )
  }

  return (
    <div className="mx-auto max-w-lg space-y-4">
      <Card elevated padded={false} className="animate-rise overflow-hidden">
        <div className="bg-gradient-to-br from-brand-600 to-brand-800 px-5 pb-6 pt-5 text-center text-white">
          <p className="text-xs font-semibold uppercase tracking-wide text-brand-100">
            Semana {data.round.weekNumber} · {data.round.title}
          </p>

          <p className="mt-4 text-sm font-semibold text-brand-100">{headline}</p>

          <div className="mt-1 flex items-baseline justify-center gap-1.5">
            <span className="nums text-6xl font-black leading-none">{data.totalPoints}</span>
            <span className="nums text-lg font-semibold text-brand-200">/{data.maxPoints}</span>
          </div>

          <div className="mx-auto mt-4 h-2 w-40 overflow-hidden rounded-full bg-white/25">
            <div
              className="h-full rounded-full bg-white transition-[width] duration-700 ease-out"
              style={{ width: `${percent}%` }}
            />
          </div>
          <p className="nums mt-1.5 text-xs text-brand-100">{percent}% dos pontos possíveis</p>
        </div>

        <dl className="grid grid-cols-3 gap-2 p-4">
          <StatTile label="Acertos" value={`${data.correctCount}/${data.questionCount}`} />
          <StatTile label="Tempo" value={formatDuration(data.totalTimeMs)} />
          <StatTile label="Posição" value={data.position ? `${data.position}º` : '—'} />
        </dl>
      </Card>

      {data.status === 'InProgress' && (
        <Callout tone="warning" title="Sua tentativa ainda está em andamento">
          <Button
            size="sm"
            className="mt-2"
            onClick={() => navigate(`/rodadas/${data.round.id}/quiz`)}
          >
            Continuar respondendo
          </Button>
        </Callout>
      )}

      {!data.answersRevealed && (
        <Callout tone="info">
          O gabarito e o ranking da semana ficam disponíveis quando a rodada encerrar. Até lá, ninguém
          vê a pontuação dos outros.
        </Callout>
      )}

      <div className="space-y-2">
        {data.answersRevealed && (
          <Button size="lg" full onClick={() => navigate(`/rodadas/${data.round.id}/revisao`)}>
            Ver gabarito e explicações
          </Button>
        )}

        <div className="flex gap-2">
          <Button variant="secondary" full onClick={() => void onShare()}>
            Compartilhar
          </Button>

          {data.answersRevealed ? (
            <Button variant="secondary" full onClick={() => navigate(`/ranking?rodada=${data.round.id}`)}>
              Ranking
            </Button>
          ) : (
            <Button variant="secondary" full onClick={() => navigate('/')}>
              Início
            </Button>
          )}
        </div>

        {data.answersRevealed && (
          <Button variant="ghost" full onClick={() => navigate('/')}>
            Voltar ao início
          </Button>
        )}
      </div>

      {shareFeedback ? <Callout tone="success" live>{shareFeedback}</Callout> : null}
    </div>
  )
}
