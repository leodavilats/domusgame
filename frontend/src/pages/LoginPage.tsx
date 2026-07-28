import { useState } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { Me } from '../api/types'
import { useMutation } from '../api/hooks'
import { useSession } from '../auth/SessionContext'
import { Button, Card, ErrorBox, Field, Input, Spinner } from '../components/ui'

const messages: Record<string, string> = {
  google: 'Não foi possivel entrar com o Google. Tente de novo.',
  'google-indisponivel': 'O login com Google não esta configurado neste ambiente.',
  'google-sem-email': 'Sua conta Google não liberou o e-mail. Use e-mail e senha.',
}

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

  const externalError = params.get('erro')

  return (
    <div className="mx-auto flex min-h-dvh w-full max-w-md flex-col justify-center gap-4 p-4">
      <div className="text-center">
        <h1 className="text-2xl font-bold text-slate-900">GC Domus</h1>
        <p className="mt-1 text-sm text-slate-500">Desafios semanais das lições do GC</p>
      </div>

      <Card>
        <form
          className="space-y-3"
          onSubmit={(event) => {
            event.preventDefault()
            void login.run()
          }}
        >
          {externalError && messages[externalError] ? <ErrorBox message={messages[externalError]} /> : null}
          {login.error ? <ErrorBox message={login.error} /> : null}

          <Field label="E-mail">
            <Input
              type="email"
              autoComplete="email"
              required
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

          <Button type="submit" full loading={login.loading}>
            Entrar
          </Button>
        </form>

        <div className="my-4 flex items-center gap-3 text-xs text-slate-400">
          <span className="h-px flex-1 bg-slate-200" />
          ou
          <span className="h-px flex-1 bg-slate-200" />
        </div>

        <a
          href="/api/auth/google/start"
          role="button"
          className="flex min-h-11 w-full items-center justify-center rounded-xl border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-700"
        >
          Continuar com Google
        </a>
      </Card>

      <p className="text-center text-sm text-slate-600">
        Primeira vez aqui?{' '}
        <Link to="/cadastro" className="font-semibold text-brand-600">
          Criar conta com codigo de convite
        </Link>
      </p>
    </div>
  )
}
