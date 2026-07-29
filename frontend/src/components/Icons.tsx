interface IconProps {
  className?: string
}

const base = 'h-6 w-6'

function Svg({ className = '', children }: IconProps & { children: React.ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      className={`${base} ${className}`}
    >
      {children}
    </svg>
  )
}

export function HomeIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M3 10.5 12 3l9 7.5" />
      <path d="M5.5 9.5V20h13V9.5" />
      <path d="M10 20v-5.5h4V20" />
    </Svg>
  )
}

export function TrophyIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M7 4h10v4a5 5 0 0 1-10 0z" />
      <path d="M7 5.5H4.5v1A3.5 3.5 0 0 0 8 10" />
      <path d="M17 5.5h2.5v1A3.5 3.5 0 0 1 16 10" />
      <path d="M12 13v4" />
      <path d="M8.5 21h7l-.7-3.2a1 1 0 0 0-1-.8h-3.6a1 1 0 0 0-1 .8z" />
    </Svg>
  )
}

export function CalendarIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <rect x="3.5" y="5" width="17" height="15.5" rx="2.5" />
      <path d="M3.5 10h17" />
      <path d="M8 3.5V6M16 3.5V6" />
      <path d="M8 14h3" />
    </Svg>
  )
}

export function UserIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="12" cy="8.5" r="3.5" />
      <path d="M4.5 20c1.2-3.4 4-5 7.5-5s6.3 1.6 7.5 5" />
    </Svg>
  )
}

export function ShieldIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M12 3.5 5.5 6v5.5c0 4 2.7 7.3 6.5 8.9 3.8-1.6 6.5-4.9 6.5-8.9V6z" />
      <path d="m9.5 12 1.9 1.9 3.3-3.6" />
    </Svg>
  )
}

export function BookIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M4.5 5.5A2 2 0 0 1 6.5 4H19v14H6.5a2 2 0 0 0-2 2z" />
      <path d="M4.5 5.5V20" />
      <path d="M8.5 8.5h6M8.5 12h4" />
    </Svg>
  )
}

export function ArrowLeftIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M19 12H5.5" />
      <path d="m11 5.5-5.5 6.5 5.5 6.5" />
    </Svg>
  )
}

export function ChevronRightIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="m9.5 5.5 6.5 6.5-6.5 6.5" />
    </Svg>
  )
}

export function ClockIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 7.5V12l3 2" />
    </Svg>
  )
}

export function CheckIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="m5 12.5 4.5 4.5L19 7.5" />
    </Svg>
  )
}

export function KeyIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="8" cy="14" r="4" />
      <path d="m11 11 8-8" />
      <path d="m16.5 5.5 2 2M14 8l2 2" />
    </Svg>
  )
}

export function FlameIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M12 21c3.6 0 6-2.3 6-5.4 0-3.7-3-5.4-4.2-9.6-2 1.3-3 3-3 4.6 0 1.2-.8 1.9-1.6 1.9-.9 0-1.5-.7-1.6-1.7C6.6 12.2 6 13.9 6 15.6 6 18.7 8.4 21 12 21z" />
    </Svg>
  )
}
