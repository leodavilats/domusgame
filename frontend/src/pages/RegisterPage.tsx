import { useState } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { Me } from '../api/types'
import { useMutation } from '../api/hooks'
import { useSession } from '../auth/SessionContext'
import { GoogleButton } from '../components/GoogleButton'
import { Logo } from '../components/Logo'
import { Button, Card, ErrorBox, Field, Input, Spinner } from '../components/ui'
import { describeAuthError } from '../lib/authErrors'

export function RegisterPage() {
  const { me, loading, setMe } = useSession()
  const [params] = useSearchParams()

  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const register = useMutation(async () => {
    const session = await api.post<Me>('/api/auth/register', { displayName, email, password })
    setMe(session)
    return session
  })

  if (loading) return <Spinner label="Carregando..." />
  if (me) return <Navigate to="/" replace />

  const redirectError = describeAuthError(params.get('erro'))

  return (
    <div className="mx-auto flex min-h-dvh w-full max-w-md flex-col justify-center gap-5 p-4">
      <div>
        <Logo />
        <p className="mt-3 text-center text-sm text-slate-500">
          Crie sua conta. O código da sala vem depois.
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
          {redirectError ? <ErrorBox message={redirectError} /> : null}
          {register.error ? <ErrorBox message={register.error} /> : null}

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

        <div className="my-4 flex items-center gap-3 text-xs text-slate-400">
          <span className="h-px flex-1 bg-slate-200" />
          ou
          <span className="h-px flex-1 bg-slate-200" />
        </div>

        <GoogleButton label="Criar conta com o Google" displayName={displayName} />
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
