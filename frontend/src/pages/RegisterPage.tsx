import { useState } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { Me } from '../api/types'
import { useMutation } from '../api/hooks'
import { useSession } from '../auth/SessionContext'
import { Button, Card, ErrorBox, Field, Input, Spinner } from '../components/ui'

const messages: Record<string, string> = {
  convite: 'Código de convite invalido. Peca o codigo ao lider do GC.',
  nome: 'Nome de exibição invalido. Use de 2 a 40 caracteres.',
  'nome-em-uso': 'Já existe alguém com esse nome de exibição.',
  conta: 'Não foi possivel criar a conta com o Google.',
}

export function RegisterPage() {
  const { me, loading, setMe } = useSession()
  const [params] = useSearchParams()

  const [inviteCode, setInviteCode] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const register = useMutation(async () => {
    const session = await api.post<Me>('/api/auth/register', { inviteCode, displayName, email, password })
    setMe(session)
    return session
  })

  if (loading) return <Spinner label="Carregando..." />
  if (me) return <Navigate to="/" replace />

  const externalError = params.get('erro')
  const googleUrl = `/api/auth/google/start?inviteCode=${encodeURIComponent(inviteCode)}&displayName=${encodeURIComponent(displayName)}`

  return (
    <div className="mx-auto flex min-h-dvh w-full max-w-md flex-col justify-center gap-4 p-4">
      <div className="text-center">
        <h1 className="text-2xl font-bold text-slate-900">Criar conta</h1>
        <p className="mt-1 text-sm text-slate-500">Você precisa do codigo de convite do GC</p>
      </div>

      <Card>
        <form
          className="space-y-3"
          onSubmit={(event) => {
            event.preventDefault()
            void register.run()
          }}
        >
          {externalError && messages[externalError] ? <ErrorBox message={messages[externalError]} /> : null}
          {register.error ? <ErrorBox message={register.error} /> : null}

          <Field label="Código de convite">
            <Input
              required
              autoCapitalize="characters"
              value={inviteCode}
              onChange={(event) => setInviteCode(event.target.value)}
            />
          </Field>

          <Field label="Nome de exibição" hint="E o nome que aparece no ranking.">
            <Input
              required
              minLength={2}
              maxLength={40}
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
            />
          </Field>

          <Field label="E-mail">
            <Input
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </Field>

          <Field label="Senha" hint="Mínimo de 8 caracteres.">
            <Input
              type="password"
              autoComplete="new-password"
              required
              minLength={8}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </Field>

          <Button type="submit" full loading={register.loading}>
            Criar conta
          </Button>
        </form>

        <div className="my-4 flex items-center gap-3 text-xs text-slate-400">
          <span className="h-px flex-1 bg-slate-200" />
          ou
          <span className="h-px flex-1 bg-slate-200" />
        </div>

        <a
          href={googleUrl}
          role="button"
          className="flex min-h-11 w-full items-center justify-center rounded-xl border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-700"
        >
          Criar conta com Google
        </a>
        <p className="mt-2 text-center text-xs text-slate-500">
          Preencha o convite e o nome antes de usar o Google.
        </p>
      </Card>

      <p className="text-center text-sm text-slate-600">
        Já tem conta?{' '}
        <Link to="/entrar" className="font-semibold text-brand-600">
          Entrar
        </Link>
      </p>
    </div>
  )
}
