import { useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { api } from '../api/client'
import type { Me } from '../api/types'
import { useMutation } from '../api/hooks'
import { useSession } from '../auth/SessionContext'
import { Logo } from '../components/Logo'
import { Button, Card, ErrorBox, Field, Input, Spinner } from '../components/ui'

export function RegisterPage() {
  const { me, loading, setMe } = useSession()

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

  return (
    <div className="mx-auto flex min-h-dvh w-full max-w-md flex-col justify-center gap-5 p-4">
      <div>
        <Logo />
        <p className="mt-3 text-center text-sm text-slate-500">
          Você precisa do código de convite do GC
        </p>
      </div>

      <Card>
        <form
          className="space-y-3"
          onSubmit={(event) => {
            event.preventDefault()
            void register.run()
          }}
        >
          {register.error ? <ErrorBox message={register.error} /> : null}

          <Field label="Código de convite">
            <Input
              required
              autoCapitalize="characters"
              value={inviteCode}
              onChange={(event) => setInviteCode(event.target.value)}
            />
          </Field>

          <Field label="Nome de exibição" hint="É o nome que aparece no ranking.">
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
