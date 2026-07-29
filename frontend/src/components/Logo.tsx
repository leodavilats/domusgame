export function Logo({ className = '' }: { className?: string }) {
  return (
    <div className={`overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm ${className}`}>
      <img src="/logo.svg" alt="gc domus" className="block w-full" />
    </div>
  )
}

export function LogoMark({ size = 36 }: { size?: number }) {
  return (
    <img
      src="/icone.svg"
      alt="gc domus"
      width={size}
      height={size}
      className="rounded-xl border border-slate-200"
      style={{ width: size, height: size }}
    />
  )
}
