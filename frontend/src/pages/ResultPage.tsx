import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { AttemptResult } from '../api/types'
import { Button, Card, ErrorBox, Spinner } from '../components/ui'
import { formatDuration } from '../lib/format'
import { buildScoreMessage, share } from '../lib/share'
import { useSession } from '../auth/SessionContext'

export function ResultPage() {
  const { attemptId } = useParams<{ attemptId: string }>()
  const navigate = useNavigate()
  const { me } = useSession()
  const { data, loading, error, reload } = useApi<AttemptResult>(`/api/attempts/${attemptId}/result`)
  const [shareFeedback, setShareFeedback] = useState<string | null>(null)

  if (loading) return <Spinner />
  if (error) return <ErrorBox message={error} onRetry={reload} />
  if (!data) return null

  const percent = data.maxPoints === 0 ? 0 : Math.round((data.totalPoints / data.maxPoints) * 100)

  async function onShare() {
    if (!data) return

    const result = await share({
      title: 'Desafio do GC',
      text: buildScoreMessage(me?.gcName ?? 'GC Domus', data.round.weekNumber, data.totalPoints),
    })

    setShareFeedback(
      result === 'shared' ? null : result === 'copied' ? 'Texto copiado!' : 'Não foi possível compartilhar.',
    )
  }

  return (
    <div className="space-y-4">
      <Card className="text-center">
        <p className="text-sm font-semibold uppercase tracking-wide text-slate-500">
          Semana {data.round.weekNumber}
        </p>
        <h1 className="mt-1 text-xl font-bold text-slate-900">{data.round.title}</h1>

        <p className="mt-6 text-5xl font-black text-brand-600">{data.totalPoints}</p>
        <p className="text-sm text-slate-500">de {data.maxPoints} pontos possiveis ({percent}%)</p>

        <dl className="mt-6 grid grid-cols-3 gap-2 text-center">
          <Metric label="Acertos" value={`${data.correctCount}/${data.questionCount}`} />
          <Metric label="Tempo" value={formatDuration(data.totalTimeMs)} />
          <Metric label="Posição" value={data.position ? `${data.position}º` : '—'} />
        </dl>
      </Card>

      {!data.answersRevealed && (
        <Card className="bg-amber-50">
          <p className="text-sm text-amber-900">
            O gabarito e o ranking da semana ficam disponíveis quando a rodada encerrar. Até lá, ninguém ve
            a pontuação dos outros.
          </p>
        </Card>
      )}

      {data.status === 'InProgress' && (
        <Card className="bg-slate-50">
          <p className="text-sm text-slate-600">Sua tentativa ainda está em andamento.</p>
          <Button className="mt-2" onClick={() => navigate(`/rodadas/${data.round.id}/quiz`)}>
            Continuar respondendo
          </Button>
        </Card>
      )}

      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" onClick={() => void onShare()}>
          Compartilhar
        </Button>

        {data.answersRevealed && (
          <>
            <Button variant="secondary" onClick={() => navigate(`/rodadas/${data.round.id}/revisao`)}>
              Ver gabarito
            </Button>
            <Button variant="ghost" onClick={() => navigate('/ranking')}>
              Ver ranking
            </Button>
          </>
        )}

        <Button variant="ghost" onClick={() => navigate('/')}>
          Voltar ao início
        </Button>
      </div>

      {shareFeedback ? <p className="text-sm text-slate-600">{shareFeedback}</p> : null}
    </div>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-slate-50 p-3">
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="text-base font-bold text-slate-900">{value}</dd>
    </div>
  )
}
