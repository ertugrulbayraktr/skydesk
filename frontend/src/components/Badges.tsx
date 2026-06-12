import { Categories, Priorities, TicketStates } from '../api/types'

const stateColors: Record<number, string> = {
  0: 'bg-blue-100 text-blue-700',
  1: 'bg-cyan-100 text-cyan-700',
  2: 'bg-indigo-100 text-indigo-700',
  3: 'bg-amber-100 text-amber-700',
  4: 'bg-purple-100 text-purple-700',
  5: 'bg-emerald-100 text-emerald-700',
  6: 'bg-slate-200 text-slate-600',
  7: 'bg-rose-100 text-rose-700',
}

const priorityColors: Record<number, string> = {
  0: 'bg-slate-100 text-slate-600',
  1: 'bg-yellow-100 text-yellow-700',
  2: 'bg-orange-100 text-orange-700',
  3: 'bg-red-100 text-red-700',
}

export function StateBadge({ state }: { state: number }) {
  return (
    <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${stateColors[state] ?? ''}`}>
      {TicketStates[state]}
    </span>
  )
}

export function PriorityBadge({ priority }: { priority: number }) {
  return (
    <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${priorityColors[priority] ?? ''}`}>
      {Priorities[priority]}
    </span>
  )
}

export function CategoryBadge({ category }: { category: number }) {
  return (
    <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs text-slate-600">
      {Categories[category]}
    </span>
  )
}

export function SlaBadge({ atRisk }: { atRisk: boolean }) {
  if (!atRisk) return null
  return (
    <span className="rounded-full bg-red-600 px-2.5 py-0.5 text-xs font-semibold text-white">
      SLA Risk
    </span>
  )
}

