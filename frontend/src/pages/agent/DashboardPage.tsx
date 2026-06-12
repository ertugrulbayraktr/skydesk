import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { api } from '../../api/client'
import type { DashboardStats } from '../../api/types'
import { Card, SkeletonRows } from '../../components/ui'

// Staggered entrance for the stat row — each card slides in 60ms apart
const statContainer = {
  hidden: {},
  show: { transition: { staggerChildren: 0.06 } },
}
const statItem = {
  hidden: { opacity: 0, y: 12 },
  show: { opacity: 1, y: 0, transition: { duration: 0.35, ease: 'easeOut' as const } },
}

function StatCard({ label, value, accent, hint }: { label: string; value: string | number; accent?: string; hint?: string }) {
  return (
    <motion.div variants={statItem}>
      <Card className="flex h-full flex-col gap-1">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</p>
        <p className={`text-3xl font-extrabold ${accent ?? 'text-slate-900'}`}>{value}</p>
        {hint && <p className="text-xs text-slate-500">{hint}</p>}
      </Card>
    </motion.div>
  )
}

function BarChart({ title, data, color }: { title: string; data: Record<string, number>; color: string }) {
  const entries = Object.entries(data)
  const max = Math.max(1, ...entries.map(([, v]) => v))
  return (
    <Card>
      <h3 className="mb-3 font-semibold">{title}</h3>
      {entries.length === 0 && <p className="text-sm text-slate-500">No data</p>}
      <div className="space-y-2">
        {entries.map(([key, value]) => (
          <div key={key} className="flex items-center gap-2 text-sm">
            <span className="w-36 shrink-0 truncate text-slate-600">{key}</span>
            <div className="h-5 flex-1 overflow-hidden rounded-md bg-slate-100">
              <div
                className={`h-full rounded-md ${color} transition-all`}
                style={{ width: `${(value / max) * 100}%` }}
              />
            </div>
            <span className="w-8 text-right font-semibold text-slate-700">{value}</span>
          </div>
        ))}
      </div>
    </Card>
  )
}

function WeeklyVolume({ data }: { data: { date: string; count: number }[] }) {
  const max = Math.max(1, ...data.map((d) => d.count))
  return (
    <Card>
      <h3 className="mb-3 font-semibold">Last 7 Days — Ticket Volume</h3>
      <div className="flex h-36 items-end justify-between gap-2">
        {data.map((d) => (
          <div key={d.date} className="flex flex-1 flex-col items-center gap-1">
            <span className="text-xs font-semibold text-slate-600">{d.count}</span>
            <div
              className="w-full rounded-t-lg bg-gradient-to-t from-brand-600 to-brand-300"
              style={{ height: `${Math.max(4, (d.count / max) * 100)}%` }}
            />
            <span className="text-[10px] text-slate-400">
              {new Date(d.date).toLocaleDateString('en-US', { month: '2-digit', day: '2-digit' })}
            </span>
          </div>
        ))}
      </div>
    </Card>
  )
}

export default function DashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => api<DashboardStats>('/api/agent/dashboard'),
    refetchInterval: 60_000,
  })

  if (isLoading || !data) return <SkeletonRows count={4} />

  const totalFeedback = data.draftFeedbackAccepted + data.draftFeedbackRejected
  const acceptanceRate = totalFeedback > 0
    ? `${Math.round((data.draftFeedbackAccepted / totalFeedback) * 100)}%`
    : '—'

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Operations Dashboard</h1>
        <p className="mt-1 text-sm text-slate-500">Live overview of support workload, SLA health and AI copilot quality.</p>
      </div>

      <motion.div variants={statContainer} initial="hidden" animate="show" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Open Tickets" value={data.openTickets} hint={`Total: ${data.totalTickets}`} />
        <StatCard
          label="SLA At Risk"
          value={data.slaAtRiskCount}
          accent={data.slaAtRiskCount > 0 ? 'text-amber-600' : 'text-emerald-600'}
          hint="Within the 30-minute window"
        />
        <StatCard
          label="SLA Breaches"
          value={data.slaBreachedCount}
          accent={data.slaBreachedCount > 0 ? 'text-rose-600' : 'text-emerald-600'}
          hint="Historical total"
        />
        <StatCard
          label="Avg. First Response"
          value={data.avgFirstResponseMinutes != null ? `${data.avgFirstResponseMinutes} min` : '—'}
          hint="Target: 120 min"
        />
      </motion.div>

      <div className="grid gap-4 lg:grid-cols-2">
        <WeeklyVolume data={data.last7Days} />
        <Card>
          <h3 className="mb-3 font-semibold">🤖 AI Draft Feedback</h3>
          <div className="flex items-center gap-6">
            <div>
              <p className="text-4xl font-extrabold text-brand-700">{acceptanceRate}</p>
              <p className="text-xs text-slate-500">acceptance rate</p>
            </div>
            <div className="space-y-1 text-sm text-slate-600">
              <p>👍 Accepted: <span className="font-semibold">{data.draftFeedbackAccepted}</span></p>
              <p>👎 Rejected: <span className="font-semibold">{data.draftFeedbackRejected}</span></p>
              <p className="text-xs text-slate-400">Agent ratings of AI reply drafts</p>
            </div>
          </div>
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <BarChart title="By State" data={data.byState} color="bg-brand-500" />
        <BarChart title="By Category" data={data.byCategory} color="bg-cyan-500" />
        <BarChart title="By Priority" data={data.byPriority} color="bg-amber-500" />
      </div>
    </div>
  )
}
