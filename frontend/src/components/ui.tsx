import { useId } from 'react'
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from 'react'

type Tone = 'neutral' | 'success' | 'warning' | 'info' | 'danger'

export function Card({
  children,
  className = '',
  elevated = false,
  padded = true,
}: {
  children: ReactNode
  className?: string
  elevated?: boolean
  padded?: boolean
}) {
  return (
    <section
      className={`rounded-2xl border border-slate-200/80 bg-surface ${
        elevated ? 'shadow-raised' : 'shadow-card'
      } ${padded ? 'p-4 sm:p-5' : ''} ${className}`}
    >
      {children}
    </section>
  )
}

export function PageTitle({
  children,
  subtitle,
  actions,
}: {
  children: ReactNode
  subtitle?: ReactNode
  actions?: ReactNode
}) {
  return (
    <header className="mb-4 flex items-start justify-between gap-3">
      <div className="min-w-0">
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">{children}</h1>
        {subtitle ? <p className="mt-1 text-sm leading-relaxed text-slate-500">{subtitle}</p> : null}
      </div>
      {actions ? <div className="flex shrink-0 gap-2">{actions}</div> : null}
    </header>
  )
}

export function SectionTitle({ children, hint }: { children: ReactNode; hint?: ReactNode }) {
  return (
    <div className="mb-3">
      <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{children}</h2>
      {hint ? <p className="mt-1 text-xs text-slate-500">{hint}</p> : null}
    </div>
  )
}

export function EmptyState({
  title,
  description,
  action,
  icon,
}: {
  title: string
  description?: string
  action?: ReactNode
  icon?: ReactNode
}) {
  return (
    <Card className="animate-rise text-center">
      {icon ? (
        <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-2xl bg-slate-100 text-slate-400">
          {icon}
        </div>
      ) : null}
      <p className="font-semibold text-slate-800">{title}</p>
      {description ? (
        <p className="mx-auto mt-1 max-w-sm text-sm leading-relaxed text-slate-500">{description}</p>
      ) : null}
      {action ? <div className="mt-4 flex justify-center">{action}</div> : null}
    </Card>
  )
}

export function Spinner({ label = 'Carregando...' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-10 text-slate-500" role="status">
      <span className="h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-brand-600" />
      <span className="text-sm">{label}</span>
    </div>
  )
}

/** Placeholder com a forma do conteudo. Reduz a sensacao de espera melhor que um spinner. */
export function Skeleton({ className = '' }: { className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={`relative block overflow-hidden rounded-lg bg-slate-200/70 ${className}`}
    >
      <span className="absolute inset-0 -translate-x-full bg-gradient-to-r from-transparent via-white/60 to-transparent [animation:domus-shimmer_1.4s_infinite]" />
    </span>
  )
}

export function SkeletonCard({ lines = 3 }: { lines?: number }) {
  return (
    <Card>
      <Skeleton className="h-4 w-24" />
      <Skeleton className="mt-3 h-6 w-2/3" />
      <div className="mt-4 space-y-2">
        {Array.from({ length: lines }).map((_, index) => (
          <Skeleton key={index} className={`h-3 ${index === lines - 1 ? 'w-1/2' : 'w-full'}`} />
        ))}
      </div>
    </Card>
  )
}

export function ErrorBox({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div
      className="flex items-start gap-3 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
      role="alert"
    >
      <span aria-hidden="true" className="mt-0.5 text-base leading-none">
        ⚠
      </span>
      <div className="min-w-0 flex-1">
        <p className="leading-relaxed">{message}</p>
        {onRetry ? (
          <button type="button" onClick={onRetry} className="mt-2 font-semibold underline">
            Tentar de novo
          </button>
        ) : null}
      </div>
    </div>
  )
}

const calloutTones: Record<Tone, string> = {
  neutral: 'border-slate-200 bg-slate-50 text-slate-700',
  info: 'border-brand-200 bg-brand-50 text-brand-900',
  success: 'border-emerald-200 bg-emerald-50 text-emerald-900',
  warning: 'border-amber-200 bg-amber-50 text-amber-900',
  danger: 'border-red-200 bg-red-50 text-red-800',
}

/** Aviso em linha. Substitui os Cards coloridos improvisados que cada tela montava do seu jeito. */
export function Callout({
  children,
  tone = 'neutral',
  title,
  live = false,
}: {
  children?: ReactNode
  tone?: Tone
  title?: string
  live?: boolean
}) {
  return (
    <div
      className={`animate-rise rounded-2xl border p-4 text-sm leading-relaxed ${calloutTones[tone]}`}
      role={live ? 'status' : undefined}
      aria-live={live ? 'polite' : undefined}
    >
      {title ? <p className="font-semibold">{title}</p> : null}
      {children ? <div className={title ? 'mt-1' : ''}>{children}</div> : null}
    </div>
  )
}

const badgeTones: Record<Tone, string> = {
  neutral: 'bg-slate-100 text-slate-600 ring-slate-200',
  success: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  warning: 'bg-amber-50 text-amber-800 ring-amber-200',
  info: 'bg-brand-50 text-brand-700 ring-brand-200',
  danger: 'bg-red-50 text-red-700 ring-red-200',
}

export function Badge({
  children,
  tone = 'neutral',
  dot = false,
}: {
  children: ReactNode
  tone?: Tone
  dot?: boolean
}) {
  const dots: Record<Tone, string> = {
    neutral: 'bg-slate-400',
    success: 'bg-emerald-500',
    warning: 'bg-amber-500',
    info: 'bg-brand-500',
    danger: 'bg-red-500',
  }

  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset ${badgeTones[tone]}`}
    >
      {dot ? <span aria-hidden="true" className={`h-1.5 w-1.5 rounded-full ${dots[tone]}`} /> : null}
      {children}
    </span>
  )
}

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'subtle'
  size?: 'sm' | 'md' | 'lg'
  loading?: boolean
  full?: boolean
  icon?: ReactNode
}

const buttonVariants: Record<string, string> = {
  primary:
    'bg-brand-600 text-white shadow-card hover:bg-brand-700 active:bg-brand-800 disabled:bg-brand-300 disabled:shadow-none',
  secondary:
    'bg-surface text-slate-700 ring-1 ring-slate-300 hover:bg-slate-50 active:bg-slate-100 disabled:text-slate-400',
  subtle: 'bg-slate-100 text-slate-700 hover:bg-slate-200 active:bg-slate-300 disabled:text-slate-400',
  ghost: 'text-brand-700 hover:bg-brand-50 active:bg-brand-100 disabled:text-slate-400',
  danger: 'bg-red-600 text-white shadow-card hover:bg-red-700 active:bg-red-800 disabled:bg-red-300',
}

const buttonSizes: Record<string, string> = {
  sm: 'min-h-9 px-3 text-sm',
  md: 'min-h-11 px-4 text-sm',
  lg: 'min-h-13 px-5 text-base',
}

export function Button({
  variant = 'primary',
  size = 'md',
  loading = false,
  full = false,
  icon,
  className = '',
  children,
  disabled,
  ...rest
}: ButtonProps) {
  return (
    <button
      {...rest}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={`inline-flex items-center justify-center gap-2 rounded-xl font-semibold transition-[background-color,box-shadow,transform] duration-150 active:scale-[0.99] disabled:cursor-not-allowed disabled:active:scale-100 ${
        buttonVariants[variant]
      } ${buttonSizes[size]} ${full ? 'w-full' : ''} ${className}`}
    >
      {loading ? (
        <span
          aria-hidden="true"
          className="h-4 w-4 shrink-0 animate-spin rounded-full border-2 border-current border-t-transparent opacity-70"
        />
      ) : (
        icon
      )}
      {children}
    </button>
  )
}

export function IconButton({
  label,
  children,
  className = '',
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { label: string; children: ReactNode }) {
  return (
    <button
      {...rest}
      aria-label={label}
      title={label}
      className={`inline-flex h-11 w-11 items-center justify-center rounded-xl text-slate-500 transition hover:bg-slate-100 hover:text-slate-700 ${className}`}
    >
      {children}
    </button>
  )
}

export function Field({
  label,
  hint,
  error,
  children,
}: {
  label: string
  hint?: string
  error?: string
  children: ReactNode | ((props: { id: string; describedBy?: string }) => ReactNode)
}) {
  const id = useId()
  const hintId = hint ? `${id}-hint` : undefined
  const errorId = error ? `${id}-error` : undefined
  const describedBy = [errorId, hintId].filter(Boolean).join(' ') || undefined

  const isRenderProp = typeof children === 'function'

  const messages = (
    <>
      {error ? (
        <span id={errorId} className="mt-1.5 block text-xs font-medium text-red-700">
          {error}
        </span>
      ) : null}
      {hint ? (
        <span id={hintId} className="mt-1.5 block text-xs leading-relaxed text-slate-500">
          {hint}
        </span>
      ) : null}
    </>
  )

  // Sem render prop o controle fica dentro do <label>: a associacao e implicita e a dica entra
  // no nome acessivel. Com render prop usamos htmlFor + aria-describedby.
  if (!isRenderProp) {
    return (
      <label className="block">
        <span className="mb-1.5 block text-sm font-medium text-slate-700">{label}</span>
        {children}
        {messages}
      </label>
    )
  }

  return (
    <div>
      <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor={id}>
        {label}
      </label>
      {children({ id, describedBy })}
      {messages}
    </div>
  )
}

const controlClass =
  'w-full rounded-xl border border-slate-300 bg-surface px-3.5 py-2.5 text-sm text-slate-900 transition placeholder:text-slate-400 hover:border-slate-400 focus:border-brand-500 focus:outline-none focus:ring-4 focus:ring-brand-500/15 disabled:bg-slate-50 disabled:text-slate-400'

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  const { className = '', ...rest } = props
  return <input {...rest} className={`${controlClass} ${className}`} />
}

export function Textarea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  const { className = '', ...rest } = props
  return <textarea {...rest} className={`${controlClass} ${className}`} />
}

export function Select({
  className = '',
  children,
  ...rest
}: SelectHTMLAttributes<HTMLSelectElement> & { children: ReactNode }) {
  return (
    <select {...rest} className={`${controlClass} cursor-pointer pr-10 ${className}`}>
      {children}
    </select>
  )
}

/** Alternador de visao. Semantica de tablist para leitor de tela e setas do teclado. */
export function SegmentedControl<T extends string>({
  value,
  onChange,
  options,
  label,
}: {
  value: T
  onChange: (value: T) => void
  options: { value: T; label: string }[]
  label: string
}) {
  return (
    <div role="tablist" aria-label={label} className="flex gap-1 rounded-2xl bg-slate-200/70 p-1">
      {options.map((option) => {
        const active = option.value === value
        return (
          <button
            key={option.value}
            role="tab"
            type="button"
            aria-selected={active}
            onClick={() => onChange(option.value)}
            className={`min-h-10 flex-1 rounded-xl px-3 text-sm font-semibold transition ${
              active ? 'bg-surface text-slate-900 shadow-card' : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}

export function StatTile({
  label,
  value,
  hint,
  tone = 'neutral',
}: {
  label: string
  value: ReactNode
  hint?: string
  tone?: 'neutral' | 'brand'
}) {
  return (
    <div
      className={`rounded-2xl border p-3 text-center ${
        tone === 'brand' ? 'border-brand-200 bg-brand-50' : 'border-slate-200/80 bg-surface shadow-card'
      }`}
    >
      <p
        className={`nums text-xl font-bold leading-tight ${
          tone === 'brand' ? 'text-brand-700' : 'text-slate-900'
        }`}
      >
        {value}
      </p>
      <p className="mt-0.5 text-xs font-medium text-slate-500">{label}</p>
      {hint ? <p className="text-[11px] text-slate-400">{hint}</p> : null}
    </div>
  )
}

export function ProgressBar({
  value,
  max = 100,
  label,
  tone = 'brand',
  size = 'md',
}: {
  value: number
  max?: number
  label: string
  tone?: 'brand' | 'danger' | 'neutral' | 'success'
  size?: 'sm' | 'md'
}) {
  const percent = max === 0 ? 0 : Math.min(100, Math.max(0, (value / max) * 100))

  const tones: Record<string, string> = {
    brand: 'bg-brand-500',
    danger: 'bg-red-500',
    neutral: 'bg-slate-400',
    success: 'bg-emerald-500',
  }

  return (
    <div
      className={`overflow-hidden rounded-full bg-slate-200 ${size === 'sm' ? 'h-1.5' : 'h-2.5'}`}
      role="progressbar"
      aria-label={label}
      aria-valuenow={Math.round(value)}
      aria-valuemin={0}
      aria-valuemax={max}
    >
      <div
        className={`h-full rounded-full transition-[width] duration-300 ease-out ${tones[tone]}`}
        style={{ width: `${percent}%` }}
      />
    </div>
  )
}

export function Avatar({
  name,
  url,
  size = 40,
  ring = false,
}: {
  name: string
  url?: string | null
  size?: number
  ring?: boolean
}) {
  const initials = name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')

  const ringClass = ring ? 'ring-2 ring-white shadow-card' : ''

  if (url) {
    return (
      <img
        src={url}
        alt=""
        width={size}
        height={size}
        className={`shrink-0 rounded-full bg-slate-100 object-cover ${ringClass}`}
        style={{ width: size, height: size }}
      />
    )
  }

  return (
    <span
      className={`inline-flex shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-brand-100 to-brand-200 font-bold text-brand-700 ${ringClass}`}
      style={{ width: size, height: size, fontSize: size / 2.6 }}
      aria-hidden="true"
    >
      {initials || '?'}
    </span>
  )
}
