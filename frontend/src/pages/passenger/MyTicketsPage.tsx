import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../api/client'
import { useAuth } from '../../auth/AuthContext'
import type { PagedTickets } from '../../api/types'
import { PriorityBadge, SlaBadge, StateBadge } from '../../components/Badges'
import { Card, EmptyState, ErrorBanner, SkeletonRows, buttonPrimaryCls, inputCls } from '../../components/ui'

export default function MyTicketsPage() {
  const { session } = useAuth()
  const queryClient = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [subject, setSubject] = useState('')
  const [description, setDescription] = useState('')
  const [lastName, setLastName] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['my-tickets'],
    queryFn: () => api<PagedTickets>('/api/tickets/mine?pageSize=50'),
  })

  const createTicket = useMutation({
    mutationFn: () =>
      api('/api/tickets', {
        method: 'POST',
        body: JSON.stringify({
          subject,
          description,
          pnr: session?.pnr,
          passengerLastName: lastName,
        }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-tickets'] })
      setShowForm(false)
      setSubject('')
      setDescription('')
      setError(null)
    },
    onError: (err) => setError(err instanceof Error ? err.message : 'Could not create the request'),
  })

  function submit(e: FormEvent) {
    e.preventDefault()
    createTicket.mutate()
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">My Support Requests</h1>
          <p className="mt-1 text-sm text-slate-500">
            Track your cancellations, refunds, baggage and delay requests.
          </p>
        </div>
        <button onClick={() => setShowForm((s) => !s)} className={buttonPrimaryCls}>
          {showForm ? 'Cancel' : '+ New Request'}
        </button>
      </div>

      {showForm && (
        <Card className="mb-6">
          <form onSubmit={submit} className="space-y-4">
            <p className="rounded-xl bg-brand-50 px-4 py-2.5 text-sm text-brand-800">
              Booking: <span className="font-mono font-semibold">{session?.pnr}</span> — requests are
              opened against the reservation you verified.
            </p>
            <div>
              <label className="mb-1.5 block text-sm font-medium">Subject</label>
              <input value={subject} onChange={(e) => setSubject(e.target.value)} required maxLength={500} className={inputCls} />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium">Description</label>
              <textarea value={description} onChange={(e) => setDescription(e.target.value)} required rows={4} maxLength={5000} className={inputCls} />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium">Last name (as on the booking)</label>
              <input value={lastName} onChange={(e) => setLastName(e.target.value)} required className={inputCls} />
            </div>
            <ErrorBanner message={error} />
            <button type="submit" disabled={createTicket.isPending} className={buttonPrimaryCls}>
              {createTicket.isPending ? 'Submitting…' : 'Submit Request'}
            </button>
          </form>
        </Card>
      )}

      {isLoading && <SkeletonRows count={3} />}

      <div className="space-y-3">
        {data?.tickets.map((t) => (
          <Link
            key={t.id}
            to={`/tickets/${t.id}`}
            className="block rounded-2xl border border-slate-200/80 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-brand-300 hover:shadow-md"
          >
            <div className="flex items-center justify-between gap-4">
              <div className="min-w-0">
                <p className="truncate font-semibold">{t.subject}</p>
                <p className="mt-0.5 text-xs text-slate-500">
                  {t.ticketNumber} · {new Date(t.createdAt).toLocaleString('en-US')}
                </p>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <SlaBadge atRisk={t.slaRisk} />
                <PriorityBadge priority={t.priority} />
                <StateBadge state={t.state} />
              </div>
            </div>
          </Link>
        ))}
        {data && data.tickets.length === 0 && !showForm && (
          <EmptyState
            icon="🎫"
            title="No support requests yet"
            hint="Create your first request with the 'New Request' button above."
          />
        )}
      </div>
    </div>
  )
}
