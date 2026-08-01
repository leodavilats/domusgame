import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { useMutation } from '../api/hooks'
import type { Me } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Avatar, Badge, Button, Callout, Card, ErrorBox, Field, Input, PageTitle } from '../components/ui'
import { BadgeGrid } from '../components/badges/BadgeGrid'

export function ProfilePage() {
  const { me, setMe, logout } = useSession()
  const navigate = useNavigate()

  const [displayName, setDisplayName] = useState(me?.displayName ?? '')
  const [saved, setSaved] = useState(false)

  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [confirmation, setConfirmation] = useState('')

  const save = useMutation(async () => {
    const updated = await api.put<Me>('/api/profile', { displayName })

    setMe(updated)
    setSaved(true)
    return updated
  })

  const remove = useMutation(async () => {
    await api.post('/api/profile/delete', { confirmation })
    await logout()
    navigate('/entrar', { replace: true })
  })

  const dirty = displayName.trim() !== (me?.displayName ?? '')

  return (
    <div className="mx-auto max-w-lg space-y-4">
      <PageTitle subtitle="Como você aparece para o restante do GC">Meu perfil</PageTitle>

      <Card elevated>
        <div className="flex items-center gap-4">
          <Avatar name={me?.displayName ?? '?'} url={me?.avatarUrl} size={64} ring />

          <div className="min-w-0">
            <p className="truncate text-lg font-bold text-slate-900">{me?.displayName}</p>
            <div className="mt-1 flex flex-wrap gap-1.5">
              {me?.room ? <Badge tone="info">{me.room.name}</Badge> : <Badge>Sem sala</Badge>}
              {me?.isAdmin ? <Badge tone="success">Administrador</Badge> : null}
            </div>
          </div>
        </div>

        <p className="mt-3 text-xs leading-relaxed text-slate-500">
          {me?.avatarUrl
            ? 'Sua foto vem da sua conta do Google — para trocar, troque por lá.'
            : 'Entre com o Google para que sua foto apareça aqui e no ranking.'}
        </p>
      </Card>

      <Card>
        <h2 className="text-sm font-semibold text-slate-700">Selos</h2>
        <p className="mt-1 text-xs leading-relaxed text-slate-500">
          Marcas conquistadas ao longo da jornada. Passe o mouse ou toque para ver o critério.
        </p>
        <div className="mt-3">
          <BadgeGrid badges={me?.badges ?? []} />
        </div>
      </Card>

      <Card>
        <form
          className="space-y-4"
          onSubmit={(event) => {
            event.preventDefault()
            setSaved(false)
            void save.run()
          }}
        >
          {save.error ? <ErrorBox message={save.error} /> : null}
          {saved && !dirty ? (
            <Callout tone="success" live>
              Perfil atualizado.
            </Callout>
          ) : null}

          <Field label="Nome de exibição" hint="É o nome que aparece no ranking para todo o GC.">
            <Input
              required
              minLength={2}
              maxLength={40}
              value={displayName}
              onChange={(event) => {
                setDisplayName(event.target.value)
                setSaved(false)
              }}
            />
          </Field>

          <Button type="submit" full loading={save.loading} disabled={!dirty}>
            {dirty ? 'Salvar alterações' : 'Nada para salvar'}
          </Button>
        </form>
      </Card>

      <Card>
        <Button
          variant="secondary"
          full
          onClick={() => {
            void logout().then(() => navigate('/entrar', { replace: true }))
          }}
        >
          Sair da conta
        </Button>
      </Card>

      <Card className="border-red-200/80">
        <h2 className="text-sm font-semibold text-red-700">Excluir minha conta</h2>
        <p className="mt-1 text-xs leading-relaxed text-slate-600">
          Seu nome sai do ranking e seus dados pessoais são apagados. As pontuações das rodadas
          continuam contabilizadas de forma anônima.
        </p>

        {!confirmingDelete ? (
          <Button variant="secondary" size="sm" className="mt-3" onClick={() => setConfirmingDelete(true)}>
            Quero excluir
          </Button>
        ) : (
          <form
            className="mt-4 space-y-3"
            onSubmit={(event) => {
              event.preventDefault()
              void remove.run()
            }}
          >
            {remove.error ? <ErrorBox message={remove.error} /> : null}

            <Field label={`Digite "${me?.displayName}" para confirmar`}>
              <Input
                value={confirmation}
                onChange={(event) => setConfirmation(event.target.value)}
                required
              />
            </Field>

            <div className="flex gap-2">
              <Button
                type="submit"
                variant="danger"
                loading={remove.loading}
                disabled={confirmation.trim().toLowerCase() !== (me?.displayName ?? '').toLowerCase()}
              >
                Excluir definitivamente
              </Button>
              <Button type="button" variant="ghost" onClick={() => setConfirmingDelete(false)}>
                Cancelar
              </Button>
            </div>
          </form>
        )}
      </Card>
    </div>
  )
}
