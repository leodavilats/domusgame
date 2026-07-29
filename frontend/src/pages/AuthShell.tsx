import type { ReactNode } from 'react'
import { Logo } from '../components/Logo'
import { Card } from '../components/ui'

export function AuthShell({
  tagline,
  children,
  footer,
}: {
  tagline: string
  children: ReactNode
  footer: ReactNode
}) {
  return (
    <div className="flex min-h-dvh flex-col justify-center bg-gradient-to-b from-canvas to-slate-200/60 px-4 py-8">
      <div className="mx-auto w-full max-w-md space-y-5">
        <div className="animate-rise">
          <Logo />
          <p className="mt-3 text-center text-sm leading-relaxed text-slate-500">{tagline}</p>
        </div>

        <Card elevated className="animate-rise">
          {children}
        </Card>

        <p className="text-center text-sm text-slate-600">{footer}</p>
      </div>
    </div>
  )
}

export function AuthDivider() {
  return (
    <div className="my-5 flex items-center gap-3 text-xs font-medium text-slate-400">
      <span className="h-px flex-1 bg-slate-200" />
      ou
      <span className="h-px flex-1 bg-slate-200" />
    </div>
  )
}
