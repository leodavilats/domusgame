import { useState } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { Me } from '../api/types'
import { useMutation } from '../api/hooks'
import { useSession } from '../auth/SessionContext'
import { GoogleButton } from '../components/GoogleButton'
import { Button, ErrorBox, Field, Input, Spinner } from '../components/ui'
import { describeAuthError } from '../lib/authErrors'
import { AuthDivider, AuthShell } from './AuthShell'

export function LoginPage() {
  const { me, loading, setMe } = useSession()
  const [params] = useSearchParams()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const login = useMutation(async () => {
    const session = await api.post<Me>('/api/auth/login', { email, password })
    setMe(session)
    return session
  })

  if (loading) return <Spinner label="Carregando..." />
  if (me) return <Navigate to="/" replace />

  const redirectError = describeAuthError(params.get('erro'))

  return (
    <AuthShell
      tagline="Desafios semanais do GC"
      footer={
        <>
          Primeira vez aqui?{' '}
          <Link to="/cadastro" className="font-semibold text-brand-700 underline underline-offset-4">
            Criar conta
          </Link>
        </>
      }
    >
      <form
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          void login.run()
        }}
      >
        {redirectError ? <ErrorBox message={redirectError} /> : null}
        {login.error ? <ErrorBox message={login.error} /> : null}

        <Field label="E-mail">
          <Input
            type="email"
            autoComplete="email"
            inputMode="email"
            required
            placeholder="voce@exemplo.com"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </Field>

        <Field label="Senha">
          <Input
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </Field>

        <Button type="submit" size="lg" full loading={login.loading}>
          Entrar
        </Button>
      </form>

      <AuthDivider />

      <GoogleButton label="Entrar com o Google" />
    </AuthShell>
  )
}
