import type { JSX } from 'react'

export function Markdown({ content }: { content: string }) {
  const blocks = content.replace(/\r\n/g, '\n').split(/\n{2,}/)

  return (
    <div className="space-y-3 text-slate-700">
      {blocks.map((block, index) => (
        <Block key={index} text={block.trim()} />
      ))}
    </div>
  )
}

function Block({ text }: { text: string }) {
  if (!text) return null

  const heading = /^(#{1,4})\s+(.*)$/.exec(text)
  if (heading) {
    const level = heading[1].length
    const content = <Inline text={heading[2]} />

    if (level <= 2) return <h2 className="text-lg font-semibold text-slate-900">{content}</h2>
    return <h3 className="text-base font-semibold text-slate-900">{content}</h3>
  }

  const lines = text.split('\n')

  if (lines.every((line) => /^\s*[-*]\s+/.test(line))) {
    return (
      <ul className="list-disc space-y-1 pl-5">
        {lines.map((line, index) => (
          <li key={index}>
            <Inline text={line.replace(/^\s*[-*]\s+/, '')} />
          </li>
        ))}
      </ul>
    )
  }

  if (lines.every((line) => /^\s*\d+[.)]\s+/.test(line))) {
    return (
      <ol className="list-decimal space-y-1 pl-5">
        {lines.map((line, index) => (
          <li key={index}>
            <Inline text={line.replace(/^\s*\d+[.)]\s+/, '')} />
          </li>
        ))}
      </ol>
    )
  }

  if (/^>\s?/.test(text)) {
    return (
      <blockquote className="border-l-4 border-brand-300 bg-brand-50 py-2 pl-3 text-slate-700 italic">
        <Inline text={text.replace(/^>\s?/gm, '')} />
      </blockquote>
    )
  }

  return (
    <p className="leading-relaxed">
      <Inline text={text} />
    </p>
  )
}

const PATTERN = /(\*\*[^*]+\*\*|\*[^*]+\*|\[[^\]]+\]\((https?:\/\/[^)\s]+)\))/g

function Inline({ text }: { text: string }) {
  const parts: (string | JSX.Element)[] = []
  let lastIndex = 0
  let match: RegExpExecArray | null
  const regex = new RegExp(PATTERN)

  while ((match = regex.exec(text)) !== null) {
    if (match.index > lastIndex) parts.push(text.slice(lastIndex, match.index))

    const token = match[0]

    if (token.startsWith('**')) {
      parts.push(<strong key={match.index}>{token.slice(2, -2)}</strong>)
    } else if (token.startsWith('[')) {
      const label = /\[([^\]]+)\]/.exec(token)?.[1] ?? token
      const href = match[2]
      parts.push(
        <a
          key={match.index}
          href={href}
          target="_blank"
          rel="noreferrer noopener"
          className="text-brand-600 underline"
        >
          {label}
        </a>,
      )
    } else {
      parts.push(<em key={match.index}>{token.slice(1, -1)}</em>)
    }

    lastIndex = match.index + token.length
  }

  if (lastIndex < text.length) parts.push(text.slice(lastIndex))

  return <>{parts}</>
}
