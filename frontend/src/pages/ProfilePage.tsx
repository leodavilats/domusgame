import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { useMutation } from '../api/hooks'
import type { Me } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Avatar, Button, Card, ErrorBox, Field, Input, PageTitle } from '../components/ui'

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

  return (
    <div className="space-y-4">
      <PageTitle subtitle="Como você aparece para o restante do GC">Meu perfil</PageTitle>

      <Card>
        <form
          className="space-y-3"
          onSubmit={(event) => {
            event.preventDefault()
            setSaved(false)
            void save.run()
          }}
        >
          {save.error ? <ErrorBox message={save.error} /> : null}
          {saved ? <p className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">Perfil atualizado.</p> : null}

          <div className="flex items-center gap-3 rounded-xl bg-slate-50 p-3">
            <Avatar name={me?.displayName ?? '?'} url={me?.avatarUrl} size={48} />
            <p className="text-xs text-slate-600">
              {me?.avatarUrl
                ? 'Sua foto vem da sua conta do Google. Para trocar, troque a foto por lá.'
                : 'Entre com o Google para que sua foto apareça aqui e no ranking.'}
            </p>
          </div>

          <Field label="Nome de exibição" hint="É o nome que aparece no ranking.">
            <Input
              required
              minLength={2}
              maxLength={40}
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
            />
          </Field>

          <Button type="submit" full loading={save.loading}>
            Salvar
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

      <Card className="border-red-200">
        <h2 className="text-sm font-semibold text-red-700">Excluir minha conta</h2>
        <p className="mt-1 text-xs text-slate-600">
          Seu nome sai do ranking e seus dados pessoais são apagados. As pontuações das rodadas continuam
          contabilizadas de forma anônima.
        </p>

        {!confirmingDelete ? (
          <Button variant="danger" className="mt-3" onClick={() => setConfirmingDelete(true)}>
            Quero excluir
          </Button>
        ) : (
          <form
            className="mt-3 space-y-3"
            onSubmit={(event) => {
              event.preventDefault()
              void remove.run()
            }}
          >
            {remove.error ? <ErrorBox message={remove.error} /> : null}

            <Field label={`Digite "${me?.displayName}" para confirmar`}>
              <Input value={confirmation} onChange={(event) => setConfirmation(event.target.value)} required />
            </Field>

            <div className="flex gap-2">
              <Button type="submit" variant="danger" loading={remove.loading}>
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
