import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { Ranking, RankingEntry, RoundListItem } from '../api/types'
import { Avatar, Card, EmptyState, ErrorBox, PageTitle, Spinner } from '../components/ui'
import { formatDuration } from '../lib/format'

type Tab = 'season' | 'round'

export function RankingPage() {
  const [params, setParams] = useSearchParams()
  const roundFromUrl = params.get('rodada')

  const [tab, setTab] = useState<Tab>(roundFromUrl ? 'round' : 'season')
  const rounds = useApi<RoundListItem[]>('/api/rounds')

  const closedRounds = useMemo(
    () => (rounds.data ?? []).filter((item) => item.round.availability === 'Closed'),
    [rounds.data],
  )

  const selectedRoundId = roundFromUrl ?? closedRounds[0]?.round.id ?? null

  const path = tab === 'season' ? '/api/rankings/season' : selectedRoundId ? `/api/rankings/round/${selectedRoundId}` : null
  const ranking = useApi<Ranking>(path)

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Pontuacao acumulada e resultado das semanas encerradas">Ranking</PageTitle>

      <div className="flex gap-2">
        <TabButton active={tab === 'season'} onClick={() => setTab('season')}>
          Temporada
        </TabButton>
        <TabButton active={tab === 'round'} onClick={() => setTab('round')}>
          Semana
        </TabButton>
      </div>

      {tab === 'round' && (
        <select
          className="w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm"
          value={selectedRoundId ?? ''}
          onChange={(event) => setParams({ rodada: event.target.value })}
        >
          {closedRounds.length === 0 && <option value="">Nenhuma semana encerrada ainda</option>}
          {closedRounds.map((item) => (
            <option key={item.round.id} value={item.round.id}>
              Semana {item.round.weekNumber} — {item.round.title}
            </option>
          ))}
        </select>
      )}

      {ranking.loading && <Spinner />}
      {ranking.error && <ErrorBox message={ranking.error} onRetry={ranking.reload} />}

      {!ranking.loading && !ranking.error && ranking.data && (
        <RankingList ranking={ranking.data} />
      )}

      {tab === 'round' && !selectedRoundId && (
        <EmptyState
          title="Nenhuma semana encerrada"
          description="O ranking da semana aparece quando a rodada fecha."
        />
      )}
    </div>
  )
}

function RankingList({ ranking }: { ranking: Ranking }) {
  if (ranking.entries.length === 0) {
    return <EmptyState title="Ainda nao ha pontuacao registrada" />
  }

  const podium = ranking.entries.slice(0, 3)
  const rest = ranking.entries.slice(3)
  const meOutsideList = ranking.me && !ranking.entries.some((entry) => entry.isMe) ? ranking.me : null

  return (
    <div className="space-y-3">
      <Card>
        <ul className="space-y-2">
          {podium.map((entry) => (
            <Row key={entry.participantId} entry={entry} highlightPodium />
          ))}
        </ul>
      </Card>

      {rest.length > 0 && (
        <Card>
          <ul className="space-y-2">
            {rest.map((entry) => (
              <Row key={entry.participantId} entry={entry} />
            ))}
          </ul>
        </Card>
      )}

      {meOutsideList && (
        <Card className="border-brand-300 bg-brand-50">
          <p className="mb-2 text-xs font-semibold uppercase text-brand-700">Sua posicao</p>
          <ul>
            <Row entry={meOutsideList} />
          </ul>
        </Card>
      )}
    </div>
  )
}

function Row({ entry, highlightPodium = false }: { entry: RankingEntry; highlightPodium?: boolean }) {
  const medals = ['🥇', '🥈', '🥉']

  return (
    <li
      className={`flex items-center gap-3 rounded-xl px-2 py-2 ${
        entry.isMe ? 'bg-brand-50 ring-1 ring-brand-200' : ''
      }`}
    >
      <span className="w-8 text-center text-sm font-bold text-slate-500">
        {highlightPodium && entry.position <= 3 ? medals[entry.position - 1] : `${entry.position}o`}
      </span>

      <Avatar name={entry.displayName} url={entry.avatarUrl} size={36} />

      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold text-slate-900">
          {entry.displayName}
          {entry.isMe ? ' (voce)' : ''}
        </p>
        <p className="text-xs text-slate-500">
          {entry.roundsPlayed} rodada(s) · {formatDuration(entry.totalTimeMs)}
        </p>
      </div>

      <span className="text-base font-bold text-brand-700">{entry.totalPoints}</span>
    </li>
  )
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`min-h-11 flex-1 rounded-xl px-4 text-sm font-semibold ${
        active ? 'bg-brand-600 text-white' : 'border border-slate-300 bg-white text-slate-600'
      }`}
    >
      {children}
    </button>
  )
}
