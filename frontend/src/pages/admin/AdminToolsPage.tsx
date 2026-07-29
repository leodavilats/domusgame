import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminRoundListItem, AuditEntry, ToolActionResult, ToolsDiagnostics } from '../../api/types'
import { useSession } from '../../auth/SessionContext'
import { Badge, Button, Card, ErrorBox, Field, Input, PageTitle, Select, Spinner } from '../../components/ui'
import { formatDateTime } from '../../lib/format'
import { AvailabilityBadge } from '../HomePage'

const CONFIRMATION = 'LIMPAR'

export function AdminToolsPage() {
  const { me, refresh } = useSession()
  const diagnostics = useApi<ToolsDiagnostics>('/api/admin/tools/diagnostics')
  const audit = useApi<AuditEntry[]>('/api/admin/tools/audit')
  const rounds = useApi<AdminRoundListItem[]>('/api/admin/rounds')

  const [message, setMessage] = useState<string | null>(null)
  const [roundId, setRoundId] = useState('')
  const [simulateCount, setSimulateCount] = useState(6)
  const [scope, setScope] = useState<'attempts' | 'content' | 'all'>('attempts')
  const [confirmation, setConfirmation] = useState('')

  function reloadAll() {
    diagnostics.reload()
    audit.reload()
    rounds.reload()
  }

  const run = useMutation(async (path: string, body?: unknown) => {
    const result = await api.post<ToolActionResult>(path, body)
    setMessage(result.message)
    reloadAll()
    return result
  })

  const removeMyAttempt = useMutation(async (id: string) => {
    const result = await api.del<ToolActionResult>(`/api/admin/tools/rounds/${id}/my-attempt`)
    setMessage(result.message)
    reloadAll()
    return result
  })

  const leaveRoom = useMutation(async () => {
    const result = await api.del<ToolActionResult>('/api/admin/tools/my-room')
    setMessage(result.message)

    await refresh()
    diagnostics.reload()
    audit.reload()

    return result
  })

  const reset = useMutation(async () => {
    const result = await api.post<{
      scope: string
      seasonsRemoved: number
      roundsRemoved: number
      attemptsRemoved: number
      participantsRemoved: number
    }>('/api/admin/tools/reset', { scope, confirmation })

    setMessage(
      `Limpeza (${result.scope}): ${result.attemptsRemoved} tentativa(s), ` +
        `${result.roundsRemoved} rodada(s), ${result.seasonsRemoved} temporada(s), ` +
        `${result.participantsRemoved} pessoa(s).`,
    )
    setConfirmation('')
    reloadAll()
    return result
  })

  if (diagnostics.loading) return <Spinner />
  if (diagnostics.error) return <ErrorBox message={diagnostics.error} onRetry={diagnostics.reload} />
  if (!diagnostics.data) return null

  const info = diagnostics.data
  const selectedRound = (rounds.data ?? []).find((item) => item.round.id === roundId)

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Atalhos para testar o fluxo sem esperar o relógio">Ferramentas</PageTitle>

      {message ? (
        <Card className="border-emerald-300 bg-emerald-50">
          <p className="text-sm text-emerald-900">{message}</p>
        </Card>
      ) : null}

      {run.error ? <ErrorBox message={run.error} /> : null}
      {reset.error ? <ErrorBox message={reset.error} /> : null}
      {removeMyAttempt.error ? <ErrorBox message={removeMyAttempt.error} /> : null}
      {leaveRoom.error ? <ErrorBox message={leaveRoom.error} /> : null}

      <Card>
        <div className="mb-3 flex items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-slate-700">Diagnóstico</h2>
          <Badge tone={info.enabled ? 'success' : 'neutral'}>
            {info.enabled ? 'ferramentas ativas' : 'ferramentas desligadas'}
          </Badge>
        </div>

        <dl className="grid grid-cols-2 gap-2 text-sm">
          <Info label="Ambiente" value={info.environment} />
          <Info label="Hora do servidor" value={formatDateTime(info.serverNowUtc)} />
          <Info label="Fuso de exibição" value={info.timeZoneHint} />
          <Info label="Temporada ativa" value={info.activeSeasonName ?? '—'} />
          <Info label="Migration" value={info.appliedMigration ?? '—'} />
          <Info label="Hora deste aparelho" value={formatDateTime(new Date())} />
        </dl>

        <div className="mt-3 grid grid-cols-3 gap-2 sm:grid-cols-6">
          <Count label="Temporadas" value={info.seasons} />
          <Count label="Rodadas" value={info.rounds} />
          <Count label="Perguntas" value={info.questions} />
          <Count label="Pessoas" value={info.participants} />
          <Count label="Tentativas" value={info.attempts} />
          <Count label="Respostas" value={info.answers} />
        </div>

        {!info.enabled && (
          <p className="mt-3 rounded-xl bg-amber-50 p-3 text-sm text-amber-900">
            As ações abaixo estão bloqueadas. Para liberá-las, defina{' '}
            <code className="font-mono">DevTools__Enabled=true</code> nas variáveis de ambiente e
            reinicie. Desligue de novo antes de abrir o app para o GC.
          </p>
        )}
      </Card>

      <Card>
        <h2 className="text-sm font-semibold text-slate-700">Temporada de teste</h2>
        <p className="mt-1 text-xs text-slate-500">
          Cria uma temporada com três rodadas de <strong>um dia</strong> — encerrada, aberta e
          agendada — com 5 perguntas fáceis cada, cobrindo as variações: sem mídia, com imagem, com
          áudio, com 2 e com 5 alternativas. A mídia é servida pelo próprio app.
        </p>

        <Button
          className="mt-3"
          disabled={!info.enabled}
          loading={run.loading}
          onClick={() => void run.run('/api/admin/tools/demo-season')}
        >
          Criar temporada de teste
        </Button>
      </Card>

      <Card>
        <h2 className="text-sm font-semibold text-slate-700">Ações em uma rodada</h2>

        {rounds.loading && <Spinner />}

        <div className="mt-3 space-y-3">
          <Field label="Rodada">
            <Select value={roundId} onChange={(event) => setRoundId(event.target.value)}>
              <option value="">Selecione…</option>
              {(rounds.data ?? []).map((item) => (
                <option key={item.round.id} value={item.round.id}>
                  S{item.round.weekNumber} — {item.round.title}
                </option>
              ))}
            </Select>
          </Field>

          {selectedRound && (
            <p className="flex items-center gap-2 text-xs text-slate-500">
              <AvailabilityBadge availability={selectedRound.round.availability} />
              {formatDateTime(selectedRound.round.opensAt)} →{' '}
              {formatDateTime(selectedRound.round.closesAt)} · {selectedRound.attemptCount}{' '}
              participação(ões)
            </p>
          )}

          <div className="flex flex-wrap gap-2">
            <Button
              variant="secondary"
              disabled={!info.enabled || !roundId}
              onClick={() => void run.run(`/api/admin/tools/rounds/${roundId}/open-now`)}
            >
              Abrir agora
            </Button>

            <Button
              variant="secondary"
              disabled={!info.enabled || !roundId}
              onClick={() => void run.run(`/api/admin/tools/rounds/${roundId}/close-now`)}
            >
              Encerrar agora
            </Button>

            <Button
              variant="secondary"
              disabled={!info.enabled || !roundId}
              loading={removeMyAttempt.loading}
              onClick={() => void removeMyAttempt.run(roundId)}
            >
              Refazer minha tentativa
            </Button>
          </div>

          <p className="text-xs text-slate-500">
            "Abrir agora" e "Encerrar agora" deslocam a janela da rodada, o que normalmente é
            proibido depois da abertura (RN-10). "Refazer" apaga <strong>só a sua</strong> tentativa,
            já que ela é única por participante.
          </p>

          <div className="flex flex-wrap items-end gap-2 border-t border-slate-200 pt-3">
            <div className="w-28">
              <Field label="Quantidade">
                <Input
                  type="number"
                  min={1}
                  max={30}
                  value={simulateCount}
                  onChange={(event) => setSimulateCount(Number(event.target.value))}
                />
              </Field>
            </div>

            <Button
              variant="secondary"
              disabled={!info.enabled || !roundId}
              onClick={() =>
                void run.run(`/api/admin/tools/rounds/${roundId}/simulate`, { count: simulateCount })
              }
            >
              Simular participações
            </Button>
          </div>

          <p className="text-xs text-slate-500">
            Cria participantes fictícios que respondem a rodada com desempenhos variados, usando as
            regras reais de pontuação — serve para ver ranking e estatísticas com dados.
          </p>
        </div>
      </Card>

      <Card>
        <h2 className="text-sm font-semibold text-slate-700">Minha sala</h2>

        <p className="mt-1 text-xs text-slate-500">
          Sair da sala mexe <strong>só na sua filiação</strong>: temporadas, rodadas, pontuação e as
          outras pessoas ficam intactas. Serve para ver o app como quem acabou de se cadastrar e
          ainda não entrou em sala nenhuma.
        </p>

        {me?.room ? (
          <>
            <p className="mt-3 text-sm text-slate-700">
              Você está em <strong>{me.room.name}</strong>.
            </p>

            <Button
              className="mt-3"
              variant="secondary"
              disabled={!info.enabled}
              loading={leaveRoom.loading}
              onClick={() => {
                if (window.confirm('Sair da sala? O painel administrativo fica sem conteúdo até você voltar.')) {
                  void leaveRoom.run()
                }
              }}
            >
              Sair da sala
            </Button>

            <p className="mt-2 text-xs text-amber-800">
              Enquanto estiver fora, as outras abas do painel responderão "entre em uma sala" — é o
              comportamento esperado, não um erro. O código para voltar aparece na mensagem.
            </p>
          </>
        ) : (
          <>
            <p className="mt-3 text-sm text-slate-700">Você está fora de qualquer sala.</p>

            <Link
              to="/sala"
              className="mt-3 inline-flex min-h-11 items-center justify-center rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white"
            >
              Entrar em uma sala
            </Link>
          </>
        )}
      </Card>

      <Card className="border-red-200">
        <h2 className="text-sm font-semibold text-red-700">Limpar dados</h2>

        <div className="mt-3 space-y-3">
          <Field label="O que apagar">
            <Select value={scope} onChange={(event) => setScope(event.target.value as typeof scope)}>
              <option value="attempts">Só as participações (zera pontuação e ranking)</option>
              <option value="content">Participações + rodadas + temporadas</option>
              <option value="all">Tudo, inclusive as pessoas (menos administradores)</option>
            </Select>
          </Field>

          <p className="rounded-xl bg-red-50 p-3 text-xs text-red-800">
            Não há desfazer. Administradores, o código de convite e a configuração do GC são sempre
            preservados — sem eles ninguém entra para consertar o estrago.
          </p>

          <Field label={`Digite ${CONFIRMATION} para confirmar`}>
            <Input
              value={confirmation}
              autoCapitalize="characters"
              onChange={(event) => setConfirmation(event.target.value)}
            />
          </Field>

          <Button
            variant="danger"
            disabled={!info.enabled || confirmation.trim() !== CONFIRMATION}
            loading={reset.loading}
            onClick={() => {
              if (window.confirm('Apagar os dados selecionados? A ação não pode ser desfeita.')) {
                void reset.run()
              }
            }}
          >
            Limpar agora
          </Button>
        </div>
      </Card>

      <Card>
        <h2 className="mb-2 text-sm font-semibold text-slate-700">Últimas ações registradas</h2>

        {audit.loading && <Spinner />}
        {audit.data && audit.data.length === 0 && (
          <p className="text-sm text-slate-500">Nenhuma ação registrada ainda.</p>
        )}

        {audit.data && audit.data.length > 0 && (
          <ol className="space-y-2 text-xs">
            {audit.data.map((entry, index) => (
              <li key={index} className="flex flex-wrap items-baseline gap-x-2 border-b border-slate-100 pb-1">
                <span className="tabular-nums text-slate-500">{formatDateTime(entry.occurredAt)}</span>
                <span className="font-semibold text-slate-800">{entry.action}</span>
                <span className="text-slate-500">por {entry.actorName}</span>
                {entry.details ? <span className="text-slate-600">— {entry.details}</span> : null}
              </li>
            ))}
          </ol>
        )}
      </Card>
    </div>
  )
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="truncate font-medium text-slate-800">{value}</dd>
    </div>
  )
}

function Count({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl bg-slate-50 p-2 text-center">
      <p className="text-base font-bold text-slate-900">{value}</p>
      <p className="text-xs text-slate-500">{label}</p>
    </div>
  )
}
