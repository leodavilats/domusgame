import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminQuestion, AdminRound, QuestionMediaType } from '../../api/types'
import {
  Badge,
  Button,
  Callout,
  Card,
  ErrorBox,
  Field,
  IconButton,
  Input,
  PageTitle,
  SectionTitle,
  Select,
  SkeletonCard,
  Textarea,
} from '../../components/ui'
import { formatDateTime } from '../../lib/format'
import { Markdown } from '../../lib/markdown'
import { AvailabilityBadge } from '../HomePage'

interface OptionDraft {
  text: string
  isCorrect: boolean
}

const emptyOptions: OptionDraft[] = [
  { text: '', isCorrect: true },
  { text: '', isCorrect: false },
  { text: '', isCorrect: false },
  { text: '', isCorrect: false },
]

export function AdminRoundEditorPage() {
  const { roundId } = useParams<{ roundId: string }>()
  const round = useApi<AdminRound>(`/api/admin/rounds/${roundId}`)

  if (round.loading) return <SkeletonCard lines={5} />
  if (round.error) return <ErrorBox message={round.error} onRetry={round.reload} />
  if (!round.data) return null

  const data = round.data

  const editable = data.canEdit

  return (
    <div className="space-y-4">
      <PageTitle subtitle={`Semana ${data.round.weekNumber} · ${data.round.questionCount} perguntas`}>
        {data.round.title}
      </PageTitle>

      <Card>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <p className="text-sm text-slate-600">
              {formatDateTime(data.round.opensAt)} → {formatDateTime(data.round.closesAt)}
            </p>
            <p className="text-xs text-slate-500">
              {data.round.pointsPerCorrectAnswer} pts/acerto + até {data.round.maxSpeedBonus} de bônus ·{' '}
              {data.round.questionTimeLimitSeconds}s por pergunta · máximo {data.round.maxPoints} pontos
            </p>
          </div>

          {data.status === 'Draft' ? <Badge tone="neutral">Rascunho</Badge> : <AvailabilityBadge availability={data.round.availability} />}
        </div>

        {!editable && (
          <div className="mt-4">
            <Callout tone="warning" title="Esta rodada já abriu">
              Ela não pode mais ser alterada: há respostas e pontuação em jogo.
            </Callout>
          </div>
        )}

        {data.status === 'Published' && (
          <Link
            to={`/admin/rodadas/${data.round.id}/estatisticas`}
            className="mt-4 inline-flex min-h-10 items-center rounded-xl bg-slate-100 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-200"
          >
            Ver estatísticas
          </Link>
        )}
      </Card>

      <LessonEditor round={data} editable={editable} onSaved={round.reload} />
      <QuestionsEditor round={data} editable={editable} onChanged={round.reload} />
      <PublishCard round={data} onPublished={round.reload} />
      <DangerCard round={data} />
    </div>
  )
}

function LessonEditor({
  round,
  editable,
  onSaved,
}: {
  round: AdminRound
  editable: boolean
  onSaved: () => void
}) {
  const [title, setTitle] = useState(round.lesson.title)
  const [reference, setReference] = useState(round.lesson.scriptureReference)
  const [content, setContent] = useState(round.lesson.content)
  const [externalUrl, setExternalUrl] = useState(round.lesson.externalUrl ?? '')
  const [preview, setPreview] = useState(false)

  const save = useMutation(async () => {
    await api.put(`/api/admin/rounds/${round.round.id}/lesson`, {
      title,
      scriptureReference: reference,
      content,
      externalUrl: externalUrl.trim() === '' ? null : externalUrl.trim(),
    })
    onSaved()
  })

  return (
    <Card>
      <h2 className="mb-3 text-sm font-semibold text-slate-700">Lição da semana</h2>

      {!editable ? (
        <div>
          <p className="font-medium text-slate-800">{round.lesson.title}</p>
          <p className="text-sm text-slate-500">{round.lesson.scriptureReference}</p>
          <div className="mt-3">
            <Markdown content={round.lesson.content} />
          </div>
        </div>
      ) : (
        <form
          className="space-y-3"
          onSubmit={(event) => {
            event.preventDefault()
            void save.run()
          }}
        >
          {save.error ? <ErrorBox message={save.error} /> : null}

          <Field label="Título">
            <Input required maxLength={160} value={title} onChange={(event) => setTitle(event.target.value)} />
          </Field>

          <Field label="Referência bíblica">
            <Input
              required
              maxLength={160}
              placeholder="Efésios 2.1-10"
              value={reference}
              onChange={(event) => setReference(event.target.value)}
            />
          </Field>

          <Field label="Conteúdo" hint="Aceita markdown simples: ## título, **negrito**, listas e links.">
            <Textarea
              required
              rows={8}
              value={content}
              onChange={(event) => setContent(event.target.value)}
            />
          </Field>

          <Field label="Link complementar (opcional)">
            <Input
              type="url"
              placeholder="https://..."
              value={externalUrl}
              onChange={(event) => setExternalUrl(event.target.value)}
            />
          </Field>

          <div className="flex gap-2">
            <Button type="submit" loading={save.loading}>
              Salvar lição
            </Button>
            <Button type="button" variant="secondary" onClick={() => setPreview((value) => !value)}>
              {preview ? 'Ocultar prévia' : 'Pré-visualizar'}
            </Button>
          </div>

          {preview && (
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
              <Markdown content={content} />
            </div>
          )}
        </form>
      )}
    </Card>
  )
}

function QuestionsEditor({
  round,
  editable,
  onChanged,
}: {
  round: AdminRound
  editable: boolean
  onChanged: () => void
}) {
  const [editingId, setEditingId] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)

  const remove = useMutation(async (questionId: string) => {
    await api.del(`/api/admin/rounds/${round.round.id}/questions/${questionId}`)
    onChanged()
  })

  const move = useMutation(async (questionId: string, offset: number) => {
    await api.post(`/api/admin/rounds/${round.round.id}/questions/${questionId}/move`, { offset })
    onChanged()
  })

  return (
    <Card>
      <div className="mb-4 flex items-center justify-between gap-3">
        <h2 className="text-sm font-semibold text-slate-700">
          Perguntas ({round.questions.length})
        </h2>
        {editable && !adding && (
          <Button size="sm" onClick={() => setAdding(true)}>
            Adicionar
          </Button>
        )}
      </div>

      {remove.error ? <ErrorBox message={remove.error} /> : null}
      {move.error ? <ErrorBox message={move.error} /> : null}

      <ul className="space-y-3">
        {round.questions.map((question) => (
          <li key={question.id} className="rounded-xl border border-slate-200 p-3">
            {editingId === question.id ? (
              <QuestionForm
                roundId={round.round.id}
                question={question}
                onDone={() => {
                  setEditingId(null)
                  onChanged()
                }}
                onCancel={() => setEditingId(null)}
              />
            ) : (
              <div>
                <div className="flex items-start gap-2.5">
                  <span
                    aria-hidden="true"
                    className="nums mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-xs font-bold text-slate-500"
                  >
                    {question.order}
                  </span>
                  <p className="text-sm font-semibold leading-snug text-slate-900">{question.text}</p>
                </div>

                {question.mediaType !== 'None' && (
                  <p className="mt-2 pl-8">
                    <Badge tone="info">{question.mediaType === 'Image' ? 'imagem' : 'áudio'}</Badge>
                  </p>
                )}

                <ul className="mt-2.5 space-y-1">
                  {question.options.map((option, index) => (
                    <li
                      key={option.id}
                      className={`flex items-center gap-2 rounded-lg px-2 py-1 text-sm ${
                        option.isCorrect
                          ? 'bg-emerald-50 font-semibold text-emerald-800'
                          : 'text-slate-600'
                      }`}
                    >
                      <span
                        aria-hidden="true"
                        className={`flex h-5 w-5 shrink-0 items-center justify-center rounded text-[10px] font-bold ${
                          option.isCorrect ? 'bg-emerald-600 text-white' : 'bg-slate-100 text-slate-500'
                        }`}
                      >
                        {option.isCorrect ? '✓' : ['A', 'B', 'C', 'D', 'E'][index] ?? index + 1}
                      </span>
                      {option.text}
                    </li>
                  ))}
                </ul>

                {question.explanation ? (
                  <p className="mt-2 text-xs leading-relaxed text-slate-500">
                    <span className="font-semibold">Explicação:</span> {question.explanation}
                  </p>
                ) : null}

                {editable && (
                  <div className="mt-3 flex flex-wrap items-center gap-1.5">
                    <Button size="sm" variant="subtle" onClick={() => setEditingId(question.id)}>
                      Editar
                    </Button>
                    <IconButton
                      label="Mover para cima"
                      className="h-9 w-9"
                      onClick={() => void move.run(question.id, -1)}
                    >
                      ↑
                    </IconButton>
                    <IconButton
                      label="Mover para baixo"
                      className="h-9 w-9"
                      onClick={() => void move.run(question.id, 1)}
                    >
                      ↓
                    </IconButton>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="ml-auto text-red-700 hover:bg-red-50"
                      onClick={() => {
                        if (window.confirm('Remover esta pergunta?')) void remove.run(question.id)
                      }}
                    >
                      Remover
                    </Button>
                  </div>
                )}
              </div>
            )}
          </li>
        ))}
      </ul>

      {adding && (
        <div className="mt-3 rounded-xl border border-brand-200 bg-brand-50 p-3">
          <QuestionForm
            roundId={round.round.id}
            question={null}
            onDone={() => {
              setAdding(false)
              onChanged()
            }}
            onCancel={() => setAdding(false)}
          />
        </div>
      )}
    </Card>
  )
}

function QuestionForm({
  roundId,
  question,
  onDone,
  onCancel,
}: {
  roundId: string
  question: AdminQuestion | null
  onDone: () => void
  onCancel: () => void
}) {
  const [text, setText] = useState(question?.text ?? '')
  const [mediaType, setMediaType] = useState<QuestionMediaType>(question?.mediaType ?? 'None')
  const [mediaUrl, setMediaUrl] = useState(question?.mediaUrl ?? '')
  const [explanation, setExplanation] = useState(question?.explanation ?? '')
  const [options, setOptions] = useState<OptionDraft[]>(
    question ? question.options.map((option) => ({ text: option.text, isCorrect: option.isCorrect })) : emptyOptions,
  )

  const save = useMutation(async () => {
    const payload = {
      text,
      mediaType,
      mediaUrl: mediaUrl.trim() === '' ? null : mediaUrl.trim(),
      explanation: explanation.trim() === '' ? null : explanation.trim(),
      options: options.filter((option) => option.text.trim() !== ''),
    }

    if (question) {
      await api.put(`/api/admin/rounds/${roundId}/questions/${question.id}`, payload)
    } else {
      await api.post(`/api/admin/rounds/${roundId}/questions`, payload)
    }

    onDone()
  })

  function updateOption(index: number, patch: Partial<OptionDraft>) {
    setOptions((current) => current.map((option, i) => (i === index ? { ...option, ...patch } : option)))
  }

  return (
    <form
      className="space-y-3"
      onSubmit={(event) => {
        event.preventDefault()
        void save.run()
      }}
    >
      {save.error ? <ErrorBox message={save.error} /> : null}

      <Field label="Enunciado">
        <Textarea required rows={2} maxLength={500} value={text} onChange={(event) => setText(event.target.value)} />
      </Field>

      <div className="grid grid-cols-3 gap-3">
        <Field label="Mídia">
          <Select value={mediaType} onChange={(event) => setMediaType(event.target.value as QuestionMediaType)}>
            <option value="None">Nenhuma</option>
            <option value="Image">Imagem</option>
            <option value="Audio">Áudio</option>
          </Select>
        </Field>
        <div className="col-span-2">
          <Field label="URL da mídia">
            <Input
              type="url"
              placeholder="https://..."
              disabled={mediaType === 'None'}
              value={mediaUrl}
              onChange={(event) => setMediaUrl(event.target.value)}
            />
          </Field>
        </div>
      </div>

      <fieldset className="space-y-2">
        <legend className="mb-2 text-sm font-medium text-slate-700">
          Alternativas
          <span className="ml-1 font-normal text-slate-500">— 2 a 5, marque a correta</span>
        </legend>

        {options.map((option, index) => (
          <div
            key={index}
            className={`flex items-center gap-2 rounded-xl border p-2 transition ${
              option.isCorrect ? 'border-emerald-300 bg-emerald-50/60' : 'border-slate-200'
            }`}
          >
            <label className="flex shrink-0 cursor-pointer items-center gap-1.5 px-1">
              <input
                type="radio"
                name="correct"
                className="h-4 w-4 accent-emerald-600"
                checked={option.isCorrect}
                onChange={() =>
                  setOptions((current) => current.map((item, i) => ({ ...item, isCorrect: i === index })))
                }
                aria-label={`Alternativa ${['A', 'B', 'C', 'D', 'E'][index] ?? index + 1} é a correta`}
              />
              <span
                aria-hidden="true"
                className={`text-xs font-bold ${option.isCorrect ? 'text-emerald-700' : 'text-slate-400'}`}
              >
                {['A', 'B', 'C', 'D', 'E'][index] ?? index + 1}
              </span>
            </label>

            <Input
              value={option.text}
              maxLength={300}
              placeholder={`Alternativa ${['A', 'B', 'C', 'D', 'E'][index] ?? index + 1}`}
              onChange={(event) => updateOption(index, { text: event.target.value })}
            />

            {options.length > 2 && (
              <IconButton
                label={`Remover alternativa ${index + 1}`}
                className="h-9 w-9 shrink-0 hover:bg-red-50 hover:text-red-700"
                onClick={() => setOptions((current) => current.filter((_, i) => i !== index))}
              >
                ×
              </IconButton>
            )}
          </div>
        ))}

        {options.length < 5 && (
          <Button
            type="button"
            size="sm"
            variant="ghost"
            onClick={() => setOptions((current) => [...current, { text: '', isCorrect: false }])}
          >
            + alternativa
          </Button>
        )}
      </fieldset>

      <Field label="Explicação (aparece no gabarito)">
        <Textarea
          rows={2}
          maxLength={1000}
          value={explanation}
          onChange={(event) => setExplanation(event.target.value)}
        />
      </Field>

      <div className="flex gap-2">
        <Button type="submit" loading={save.loading}>
          {question ? 'Salvar pergunta' : 'Adicionar pergunta'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancelar
        </Button>
      </div>
    </form>
  )
}

function DangerCard({ round }: { round: AdminRound }) {
  const navigate = useNavigate()

  const remove = useMutation(async () => {
    await api.del(`/api/admin/rounds/${round.round.id}`)
    navigate('/admin/rodadas', { replace: true })
  })

  if (!round.canDelete) {
    if (!round.canEdit) return null

    return (
      <Callout tone="neutral">
        Esta rodada já tem {round.attemptCount} participação(ões) e por isso não pode ser excluída.
      </Callout>
    )
  }

  return (
    <Card className="border-red-200/80">
      <h2 className="text-sm font-semibold text-red-700">Excluir rodada</h2>
      <p className="mt-1 text-xs leading-relaxed text-slate-600">
        Apaga a rodada, a lição e as perguntas. Só é possível porque ela ainda não abriu.
      </p>

      {remove.error ? <div className="mt-2"><ErrorBox message={remove.error} /></div> : null}

      <Button
        variant="danger"
        className="mt-3"
        loading={remove.loading}
        onClick={() => {
          if (window.confirm('Excluir esta rodada? A ação não pode ser desfeita.')) void remove.run()
        }}
      >
        Excluir rodada
      </Button>
    </Card>
  )
}

function PublishCard({ round, onPublished }: { round: AdminRound; onPublished: () => void }) {
  const [previewing, setPreviewing] = useState(false)

  const publish = useMutation(async () => {
    await api.post(`/api/admin/rounds/${round.round.id}/publish`)
    onPublished()
  })

  if (round.status === 'Published') return null

  const ready = round.problems.length === 0

  return (
    <Card elevated>
      <SectionTitle>Publicação</SectionTitle>

      {publish.error ? <ErrorBox message={publish.error} /> : null}

      {ready ? (
        <Callout tone="success" title="Tudo certo para publicar">
          A rodada abre sozinha em {formatDateTime(round.round.opensAt)}. Enquanto não abrir, você
          ainda pode editá-la ou excluí-la.
        </Callout>
      ) : (
        <Callout tone="warning" title={`Faltam ${round.problems.length} item(ns)`}>
          <ul className="mt-1 space-y-1">
            {round.problems.map((problem) => (
              <li key={problem} className="flex gap-2">
                <span aria-hidden="true">•</span>
                {problem}
              </li>
            ))}
          </ul>
        </Callout>
      )}

      <div className="mt-4 flex flex-wrap gap-2">
        <Button
          disabled={!ready}
          loading={publish.loading}
          onClick={() => {
            if (window.confirm('Publicar a rodada? Ela abre e fecha sozinha na janela definida.')) {
              void publish.run()
            }
          }}
        >
          Publicar
        </Button>

        <Button variant="secondary" onClick={() => setPreviewing((value) => !value)}>
          {previewing ? 'Fechar prévia' : 'Pré-visualizar como participante'}
        </Button>
      </div>

      {previewing && (
        <div className="mt-3 space-y-3 rounded-xl border border-slate-200 bg-slate-50 p-3">
          <p className="text-xs font-semibold uppercase text-slate-500">Prévia (sem marcar a correta)</p>
          {round.questions.map((question) => (
            <div key={question.id} className="rounded-xl bg-white p-3">
              <p className="text-sm font-medium text-slate-900">
                {question.order}. {question.text}
              </p>
              <ul className="mt-2 space-y-1 text-sm text-slate-600">
                {question.options.map((option) => (
                  <li key={option.id} className="rounded-lg border border-slate-200 px-3 py-2">
                    {option.text}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </Card>
  )
}
