/**
 * O lockup completo tem fundo preto por design. Envolvemos em um bloco arredondado
 * para que ele funcione sobre o fundo claro do app sem parecer uma faixa solta.
 */
export function Logo({ className = '' }: { className?: string }) {
  return (
    <div className={`overflow-hidden rounded-2xl bg-black ${className}`}>
      <img src="/logo.svg" alt="gc domus" className="block w-full" />
    </div>
  )
}

/** Versao quadrada, para cabecalho e espacos pequenos. */
export function LogoMark({ size = 36 }: { size?: number }) {
  return (
    <img
      src="/icone.svg"
      alt="gc domus"
      width={size}
      height={size}
      className="rounded-xl"
      style={{ width: size, height: size }}
    />
  )
}
