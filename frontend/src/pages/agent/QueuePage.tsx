import { Link, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '../../api/client'
import { Categories, Priorities, TicketStates, type PagedTickets } from '../../api/types'
import { CategoryBadge, PriorityBadge, SlaBadge, StateBadge } from '../../components/Badges'
import { SkeletonRows } from '../../components/ui'

export default function QueuePage({ mine = false }: { mine?: boolean }) {
  const [params, setParams] = useSearchParams()
  const state = params.get('state') ?? ''
  const priority = params.get('priority') ?? ''
  const category = params.get('category') ?? ''
  const slaRisk = params.get('slaRisk') === '1'
  const page = Number(params.get('page') ?? '1')

  const queryString = new URLSearchParams({ pageNumber: String(page), pageSize: '20' })
  if (state) queryString.set('filterByState', state)
  if (priority) queryString.set('filterByPriority', priority)
  if (category) queryString.set('filterByCategory', category)
  if (slaRisk) queryString.set('filterBySlaRisk', 'true')

  const endpoint = mine
    ? `/api/agent/my-queue?pageNumber=${page}&pageSize=20`
    : `/api/agent/queue?${queryString}`

  const { data, isLoading } = useQuery({
    queryKey: ['queue', mine, state, priority, category, slaRisk, page],
    queryFn: () => api<PagedTickets>(endpoint),
    refetchInterval: 30_000,
  })

  function setFilter(key: string, value: string) {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    next.delete('page')
    setParams(next)
  }

  const selectCls =
    'rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm transition focus:border-brand-500 focus:outline-none focus:ring-4 focus:ring-brand-100'
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="mr-auto">
          <h1 className="text-2xl font-bold">{mine ? 'My Assigned Tickets' : 'Ticket Queue'}</h1>
          {data && (
            <p className="mt-0.5 text-sm text-slate-500">{data.totalCount} ticket{data.totalCount === 1 ? '' : 's'}</p>
          )}
        </div>
        {!mine && (
          <>
            <select value={state} onChange={(e) => setFilter('state', e.target.value)} className={selectCls}>
              <option value="">State (all)</option>
              {TicketStates.map((s, i) => <option key={s} value={i}>{s}</option>)}
            </select>
            <select value={priority} onChange={(e) => setFilter('priority', e.target.value)} className={selectCls}>
              <option value="">Priority (all)</option>
              {Priorities.map((p, i) => <option key={p} value={i}>{p}</option>)}
            </select>
            <select value={category} onChange={(e) => setFilter('category', e.target.value)} className={selectCls}>
              <option value="">Category (all)</option>
              {Categories.map((c, i) => <option key={c} value={i}>{c}</option>)}
            </select>
            <label className="flex items-center gap-1.5 rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm">
              <input
                type="checkbox"
                checked={slaRisk}
                onChange={(e) => setFilter('slaRisk', e.target.checked ? '1' : '')}
              />
              SLA risk only
            </label>
          </>
        )}
      </div>

      {isLoading && <SkeletonRows count={5} />}

      {!isLoading && (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Ticket</th>
                <th className="px-4 py-3">Category</th>
                <th className="px-4 py-3">Priority</th>
                <th className="px-4 py-3">State</th>
                <th className="px-4 py-3">Assignee</th>
                <th className="px-4 py-3">SLA</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data?.tickets.map((t) => (
                <tr key={t.id} className="transition hover:bg-brand-50/40">
                  <td className="px-4 py-3">
                    <Link to={`/tickets/${t.id}`} className="font-medium text-brand-700 hover:underline">
                      {t.subject}
                    </Link>
                    <p className="text-xs text-slate-400">{t.ticketNumber}</p>
                  </td>
                  <td className="px-4 py-3"><CategoryBadge category={t.category} /></td>
                  <td className="px-4 py-3"><PriorityBadge priority={t.priority} /></td>
                  <td className="px-4 py-3"><StateBadge state={t.state} /></td>
                  <td className="px-4 py-3 text-slate-600">{t.assignedToAgentName ?? '—'}</td>
                  <td className="px-4 py-3"><SlaBadge atRisk={t.slaRisk} /></td>
                </tr>
              ))}
              {data && data.tickets.length === 0 && (
                <tr><td colSpan={6} className="px-4 py-10 text-center text-slate-500">The queue is empty 🎉</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {data && totalPages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-3 text-sm">
          <button
            disabled={page <= 1}
            onClick={() => setFilter('page', String(page - 1))}
            className="rounded-xl border border-slate-300 bg-white px-3.5 py-2 transition hover:bg-slate-50 disabled:opacity-40"
          >
            ← Previous
          </button>
          <span className="font-medium text-slate-600">{page} / {totalPages}</span>
          <button
            disabled={page >= totalPages}
            onClick={() => setFilter('page', String(page + 1))}
            className="rounded-xl border border-slate-300 bg-white px-3.5 py-2 transition hover:bg-slate-50 disabled:opacity-40"
          >
            Next →
          </button>
        </div>
      )}
    </div>
  )
}
