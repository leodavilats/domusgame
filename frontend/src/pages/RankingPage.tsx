import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useApi } from '../api/hooks'
import type { Ranking, RankingEntry, RoundListItem } from '../api/types'
import {
  Avatar,
  Card,
  EmptyState,
  ErrorBox,
  Field,
  PageTitle,
  SegmentedControl,
  Select,
  SkeletonCard,
} from '../components/ui'
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

  const path =
    tab === 'season'
      ? '/api/rankings/season'
      : selectedRoundId
        ? `/api/rankings/round/${selectedRoundId}`
        : null

  const ranking = useApi<Ranking>(path)

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Pontuação acumulada e resultado das semanas encerradas">Ranking</PageTitle>

      <SegmentedControl<Tab>
        label="Escopo do ranking"
        value={tab}
        onChange={setTab}
        options={[
          { value: 'season', label: 'Temporada' },
          { value: 'round', label: 'Por semana' },
        ]}
      />

      {tab === 'round' && closedRounds.length > 0 && (
        <Field label="Semana">
          <Select
            value={selectedRoundId ?? ''}
            onChange={(event) => setParams({ rodada: event.target.value })}
          >
            {closedRounds.map((item) => (
              <option key={item.round.id} value={item.round.id}>
                Semana {item.round.weekNumber} — {item.round.title}
              </option>
            ))}
          </Select>
        </Field>
      )}

      {tab === 'round' && !selectedRoundId && !rounds.loading && (
        <EmptyState
          title="Nenhuma semana encerrada"
          description="O ranking da semana aparece quando a rodada fecha — assim ninguém vê a pontuação dos outros durante a semana."
        />
      )}

      {ranking.loading && <SkeletonCard lines={5} />}
      {ranking.error && <ErrorBox message={ranking.error} onRetry={ranking.reload} />}

      {!ranking.loading && !ranking.error && ranking.data && <RankingList ranking={ranking.data} />}
    </div>
  )
}

function RankingList({ ranking }: { ranking: Ranking }) {
  if (ranking.entries.length === 0) {
    return <EmptyState title="Ainda não há pontuação registrada" />
  }

  const [first, second, third, ...rest] = ranking.entries
  const podium = [second, first, third].filter(Boolean)
  const meOutsideList = ranking.me && !ranking.entries.some((entry) => entry.isMe) ? ranking.me : null

  return (
    <div className="space-y-3">
      {first && (
        <Card elevated className="animate-rise">
          <ul className="flex items-end justify-center gap-2 sm:gap-4">
            {podium.map((entry) => (
              <PodiumSpot key={entry.participantId} entry={entry} />
            ))}
          </ul>
        </Card>
      )}

      {rest.length > 0 && (
        <Card padded={false}>
          <ul className="divide-y divide-slate-100">
            {rest.map((entry) => (
              <Row key={entry.participantId} entry={entry} />
            ))}
          </ul>
        </Card>
      )}

      {meOutsideList && (
        <Card className="border-brand-300 bg-brand-50">
          <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-brand-700">
            Sua posição
          </p>
          <ul>
            <Row entry={meOutsideList} />
          </ul>
        </Card>
      )}
    </div>
  )
}

function PodiumSpot({ entry }: { entry: RankingEntry }) {
  const medals: Record<number, { emoji: string; height: string; size: number }> = {
    1: { emoji: '🥇', height: 'h-20', size: 64 },
    2: { emoji: '🥈', height: 'h-14', size: 48 },
    3: { emoji: '🥉', height: 'h-11', size: 48 },
  }

  const medal = medals[entry.position] ?? medals[3]

  return (
    <li className="flex min-w-0 flex-1 flex-col items-center">
      <span aria-hidden="true" className="text-xl">
        {medal.emoji}
      </span>

      <Avatar name={entry.displayName} url={entry.avatarUrl} size={medal.size} ring />

      <p className="mt-1.5 w-full truncate text-center text-xs font-semibold text-slate-800">
        {entry.displayName}
        {entry.isMe ? ' (você)' : ''}
      </p>

      <p className="nums text-sm font-bold text-brand-700">{entry.totalPoints}</p>

      <div
        className={`mt-1.5 w-full rounded-t-xl ${medal.height} ${
          entry.isMe ? 'bg-brand-200' : 'bg-slate-100'
        }`}
        aria-hidden="true"
      />
    </li>
  )
}

function Row({ entry }: { entry: RankingEntry }) {
  return (
    <li
      className={`flex items-center gap-3 px-3 py-2.5 ${
        entry.isMe ? 'bg-brand-50/70 ring-1 ring-inset ring-brand-200' : ''
      }`}
    >
      <span className="nums w-7 shrink-0 text-center text-sm font-bold text-slate-400">
        {entry.position}
      </span>

      <Avatar name={entry.displayName} url={entry.avatarUrl} size={36} />

      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold text-slate-900">
          {entry.displayName}
          {entry.isMe ? ' (você)' : ''}
        </p>
        <p className="nums text-xs text-slate-500">
          {entry.roundsPlayed} rodada(s) · {formatDuration(entry.totalTimeMs)}
        </p>
      </div>

      <span className="nums shrink-0 text-base font-bold text-brand-700">{entry.totalPoints}</span>
    </li>
  )
}
