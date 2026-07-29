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
  const shortPassword = password.length > 0 && password.length < 8

  return (
    <AuthShell
      tagline="Crie sua conta. O código da sala vem depois."
      footer={
        <>
          Já tem conta?{' '}
          <Link to="/entrar" className="font-semibold text-brand-700 underline underline-offset-4">
            Entrar
          </Link>
        </>
      }
    >
      <form
        className="space-y-4"
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
            autoComplete="name"
            placeholder="Como o GC te chama"
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
          />
        </Field>

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

        <Field
          label="Senha"
          hint="Mínimo de 8 caracteres."
          error={shortPassword ? 'Faltam alguns caracteres.' : undefined}
        >
          <Input
            type="password"
            autoComplete="new-password"
            required
            minLength={8}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </Field>

        <Button type="submit" size="lg" full loading={register.loading}>
          Criar conta
        </Button>
      </form>

      <AuthDivider />

      <GoogleButton label="Criar conta com o Google" displayName={displayName} />
    </AuthShell>
  )
}
