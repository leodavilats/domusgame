import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../../api/client'
import { useApi, useMutation } from '../../api/hooks'
import type { AdminQuestion, AdminRound, QuestionMediaType } from '../../api/types'
import {
  Badge,
  Button,
  Card,
  ErrorBox,
  Field,
  Input,
  PageTitle,
  Select,
  Spinner,
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

  if (round.loading) return <Spinner />
  if (round.error) return <ErrorBox message={round.error} onRetry={round.reload} />
  if (!round.data) return null

  const data = round.data
  const editable = data.status === 'Draft'

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
              {data.round.pointsPerCorrectAnswer} pts/acerto + ate {data.round.maxSpeedBonus} de bonus ·{' '}
              {data.round.questionTimeLimitSeconds}s por pergunta · maximo {data.round.maxPoints} pontos
            </p>
          </div>

          {data.status === 'Draft' ? <Badge tone="neutral">Rascunho</Badge> : <AvailabilityBadge availability={data.round.availability} />}
        </div>

        {!editable && (
          <p className="mt-3 rounded-xl bg-amber-50 p-3 text-sm text-amber-900">
            Rodada publicada não pode ser editada. Corrija antes de publicar (RN-10).
          </p>
        )}

        {data.status === 'Published' && (
          <Link
            to={`/admin/rodadas/${data.round.id}/estatisticas`}
            className="mt-3 inline-block text-sm font-semibold text-brand-600"
          >
            Ver estatisticas
          </Link>
        )}
      </Card>

      <LessonEditor round={data} editable={editable} onSaved={round.reload} />
      <QuestionsEditor round={data} editable={editable} onChanged={round.reload} />
      <PublishCard round={data} onPublished={round.reload} />
    </div>
  )
}

// ---------------------------------------------------------------- licao

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
      <h2 className="mb-3 text-sm font-semibold text-slate-700">Licao da semana</h2>

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

          <Field label="Titulo">
            <Input required maxLength={160} value={title} onChange={(event) => setTitle(event.target.value)} />
          </Field>

          <Field label="Referencia biblica">
            <Input
              required
              maxLength={160}
              placeholder="Efesios 2.1-10"
              value={reference}
              onChange={(event) => setReference(event.target.value)}
            />
          </Field>

          <Field label="Conteudo" hint="Aceita markdown simples: ## titulo, **negrito**, listas e links.">
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
              Salvar licao
            </Button>
            <Button type="button" variant="secondary" onClick={() => setPreview((value) => !value)}>
              {preview ? 'Ocultar previa' : 'Pre-visualizar'}
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

// ---------------------------------------------------------------- perguntas

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
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold text-slate-700">Perguntas ({round.questions.length})</h2>
        {editable && !adding && (
          <Button variant="secondary" onClick={() => setAdding(true)}>
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
                <p className="text-sm font-medium text-slate-900">
                  {question.order}. {question.text}
                </p>

                <ul className="mt-2 space-y-1 text-sm">
                  {question.options.map((option) => (
                    <li
                      key={option.id}
                      className={option.isCorrect ? 'font-semibold text-emerald-700' : 'text-slate-600'}
                    >
                      {option.isCorrect ? '✓ ' : '· '}
                      {option.text}
                    </li>
                  ))}
                </ul>

                {question.explanation ? (
                  <p className="mt-2 text-xs text-slate-500">Explicacao: {question.explanation}</p>
                ) : null}

                {editable && (
                  <div className="mt-3 flex flex-wrap gap-3 text-sm font-semibold">
                    <button type="button" className="text-brand-600" onClick={() => setEditingId(question.id)}>
                      Editar
                    </button>
                    <button type="button" className="text-slate-600" onClick={() => void move.run(question.id, -1)}>
                      Subir
                    </button>
                    <button type="button" className="text-slate-600" onClick={() => void move.run(question.id, 1)}>
                      Descer
                    </button>
                    <button
                      type="button"
                      className="text-red-600"
                      onClick={() => {
                        if (window.confirm('Remover esta pergunta?')) void remove.run(question.id)
                      }}
                    >
                      Remover
                    </button>
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
        <Field label="Midia">
          <Select value={mediaType} onChange={(event) => setMediaType(event.target.value as QuestionMediaType)}>
            <option value="None">Nenhuma</option>
            <option value="Image">Imagem</option>
            <option value="Audio">Audio</option>
          </Select>
        </Field>
        <div className="col-span-2">
          <Field label="URL da midia">
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
        <legend className="mb-1 text-sm font-medium text-slate-700">
          Alternativas (2 a 5, marque exatamente uma correta)
        </legend>

        {options.map((option, index) => (
          <div key={index} className="flex items-center gap-2">
            <input
              type="radio"
              name="correct"
              className="h-5 w-5 shrink-0"
              checked={option.isCorrect}
              onChange={() => setOptions((current) => current.map((item, i) => ({ ...item, isCorrect: i === index })))}
              aria-label={`Alternativa ${index + 1} e a correta`}
            />
            <Input
              value={option.text}
              maxLength={300}
              placeholder={`Alternativa ${index + 1}`}
              onChange={(event) => updateOption(index, { text: event.target.value })}
            />
            {options.length > 2 && (
              <button
                type="button"
                className="shrink-0 px-2 text-sm text-red-600"
                onClick={() => setOptions((current) => current.filter((_, i) => i !== index))}
              >
                remover
              </button>
            )}
          </div>
        ))}

        {options.length < 5 && (
          <Button
            type="button"
            variant="ghost"
            onClick={() => setOptions((current) => [...current, { text: '', isCorrect: false }])}
          >
            + alternativa
          </Button>
        )}
      </fieldset>

      <Field label="Explicacao (aparece no gabarito)">
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

// ---------------------------------------------------------------- publicacao

function PublishCard({ round, onPublished }: { round: AdminRound; onPublished: () => void }) {
  const [previewing, setPreviewing] = useState(false)

  const publish = useMutation(async () => {
    await api.post(`/api/admin/rounds/${round.round.id}/publish`)
    onPublished()
  })

  if (round.status === 'Published') return null

  const ready = round.problems.length === 0

  return (
    <Card>
      <h2 className="mb-3 text-sm font-semibold text-slate-700">Publicacao</h2>

      {publish.error ? <ErrorBox message={publish.error} /> : null}

      {ready ? (
        <p className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">
          Tudo certo. Depois de publicar, a rodada abre sozinha em {formatDateTime(round.round.opensAt)} e não
          podera mais ser editada.
        </p>
      ) : (
        <ul className="space-y-1 rounded-xl bg-amber-50 p-3 text-sm text-amber-900">
          {round.problems.map((problem) => (
            <li key={problem}>• {problem}</li>
          ))}
        </ul>
      )}

      <div className="mt-3 flex flex-wrap gap-2">
        <Button variant="secondary" onClick={() => setPreviewing((value) => !value)}>
          {previewing ? 'Fechar previa' : 'Pre-visualizar como participante'}
        </Button>

        <Button
          disabled={!ready}
          loading={publish.loading}
          onClick={() => {
            if (window.confirm('Publicar a rodada? Depois disso ela não podera ser editada.')) void publish.run()
          }}
        >
          Publicar
        </Button>
      </div>

      {previewing && (
        <div className="mt-3 space-y-3 rounded-xl border border-slate-200 bg-slate-50 p-3">
          <p className="text-xs font-semibold uppercase text-slate-500">Previa (sem marcar a correta)</p>
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
