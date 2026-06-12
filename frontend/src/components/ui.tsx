import type { ButtonHTMLAttributes, ReactNode } from 'react'

export function Spinner({ label }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-12 text-slate-500">
      <span className="h-5 w-5 animate-spin rounded-full border-2 border-slate-300 border-t-brand-600" />
      {label && <span className="text-sm">{label}</span>}
    </div>
  )
}

export function SkeletonRows({ count = 4 }: { count?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: count }, (_, i) => (
        <div key={i} className="skeleton h-16 w-full" />
      ))}
    </div>
  )
}

export function EmptyState({ icon, title, hint }: { icon: string; title: string; hint?: string }) {
  return (
    <div className="rounded-2xl border-2 border-dashed border-slate-200 bg-white/60 p-12 text-center">
      <p className="mb-2 text-4xl">{icon}</p>
      <p className="font-semibold text-slate-700">{title}</p>
      {hint && <p className="mt-1 text-sm text-slate-500">{hint}</p>}
    </div>
  )
}

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`rounded-2xl border border-slate-200/80 bg-white p-5 shadow-sm transition-shadow hover:shadow-md ${className}`}>
      {children}
    </div>
  )
}

export function ErrorBanner({ message }: { message: string | null }) {
  if (!message) return null
  return (
    <p className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-700">
      {message}
    </p>
  )
}

/** Abstract "meridian" logo mark — replaces the plane emoji across the app. */
export function LogoMark({ className = 'h-9 w-9' }: { className?: string }) {
  return (
    <svg viewBox="0 0 40 40" fill="none" className={className} aria-hidden="true">
      <circle cx="20" cy="20" r="18" stroke="currentColor" strokeOpacity="0.35" strokeWidth="1.5" />
      <path d="M5 26 C 14 12, 26 12, 35 22" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="29" cy="17.2" r="3" fill="currentColor" />
    </svg>
  )
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  loading?: boolean
  variant?: 'primary' | 'secondary'
}

/** Button with a built-in loading spinner state. */
export function Button({ loading = false, variant = 'primary', className = '', children, disabled, ...rest }: ButtonProps) {
  const base = variant === 'primary' ? buttonPrimaryCls : buttonSecondaryCls
  return (
    <button className={`${base} inline-flex items-center justify-center gap-2 ${className}`} disabled={disabled || loading} {...rest}>
      {loading && (
        <span className="h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white" aria-hidden="true" />
      )}
      {children}
    </button>
  )
}

export const inputCls =
  'w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm transition focus:border-brand-500 focus:outline-none focus:ring-4 focus:ring-brand-100'

export const buttonPrimaryCls =
  'rounded-xl bg-gradient-to-b from-brand-600 to-brand-700 px-4 py-2.5 text-sm font-semibold text-white shadow-sm shadow-brand-900/20 transition hover:from-brand-500 hover:to-brand-600 active:scale-[0.98] disabled:opacity-50'

export const buttonSecondaryCls =
  'rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 active:scale-[0.98] disabled:opacity-50'
