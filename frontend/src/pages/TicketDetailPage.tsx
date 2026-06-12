import { useState, type FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { AuditEventTypes, TicketStates, ValidTransitions, type AuditEvent, type DraftReply, type TicketDetail } from '../api/types'
import { CategoryBadge, PriorityBadge, SlaBadge, StateBadge } from '../components/Badges'
import { ErrorBanner, buttonPrimaryCls } from '../components/ui'

function DraftReplyPanel({ ticketId, onUse }: { ticketId: string; onUse: (text: string) => void }) {
  const [draft, setDraft] = useState<DraftReply | null>(null)
  const [feedbackSent, setFeedbackSent] = useState(false)
  const generate = useMutation({
    mutationFn: () => api<DraftReply>(`/api/agent/tickets/${ticketId}/draft-reply`),
    onSuccess: (d) => { setDraft(d); setFeedbackSent(false) },
  })

  const sendFeedback = useMutation({
    mutationFn: (accepted: boolean) =>
      api(`/api/agent/tickets/${ticketId}/draft-feedback`, {
        method: 'POST',
        body: JSON.stringify({ accepted }),
      }),
    onSuccess: () => setFeedbackSent(true),
  })

  return (
    <div className="rounded-2xl border border-brand-200 bg-brand-50/50 p-4">
      <div className="mb-2 flex items-center justify-between">
        <h3 className="font-semibold text-brand-800">🤖 AI Reply Assistant</h3>
        <button
          onClick={() => generate.mutate()}
          disabled={generate.isPending}
          className="rounded-xl bg-brand-600 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-brand-700 disabled:opacity-50"
        >
          {generate.isPending ? 'Generating…' : draft ? 'Regenerate' : 'Generate Draft'}
        </button>
      </div>
      {generate.isError && (
        <p className="text-sm text-red-600">{(generate.error as Error).message}</p>
      )}
      <AnimatePresence>
      {draft && (
        <motion.div
          initial={{ opacity: 0, height: 0 }}
          animate={{ opacity: 1, height: 'auto' }}
          exit={{ opacity: 0, height: 0 }}
          transition={{ duration: 0.3, ease: 'easeOut' }}
          className="space-y-3 overflow-hidden text-sm">
          <div className="whitespace-pre-wrap rounded-xl border border-brand-200 bg-white p-3">{draft.draftText}</div>
          <div className="flex flex-wrap items-center gap-2">
            <button
              onClick={() => { onUse(draft.draftText); if (!feedbackSent) sendFeedback.mutate(true) }}
              className="rounded-xl border border-brand-300 px-3 py-1.5 text-xs font-medium text-brand-700 transition hover:bg-brand-100"
            >
              ↓ Copy to reply box
            </button>
            {feedbackSent ? (
              <span className="text-xs text-slate-500">Feedback recorded ✓</span>
            ) : (
              <>
                <button
                  onClick={() => sendFeedback.mutate(true)}
                  title="The draft was helpful"
                  className="rounded-xl border border-emerald-300 px-2.5 py-1.5 text-xs transition hover:bg-emerald-50"
                >
                  👍
                </button>
                <button
                  onClick={() => sendFeedback.mutate(false)}
                  title="The draft was not helpful"
                  className="rounded-xl border border-rose-300 px-2.5 py-1.5 text-xs transition hover:bg-rose-50"
                >
                  👎
                </button>
              </>
            )}
          </div>
          {draft.riskFlags.length > 0 && (
            <div className="rounded-xl bg-red-50 p-3">
              <p className="mb-1 font-semibold text-red-700">⚠️ Risk Flags</p>
              <ul className="list-inside list-disc text-red-700">
                {draft.riskFlags.map((r, i) => <li key={i}>{r}</li>)}
              </ul>
            </div>
          )}
          {draft.nextActions.length > 0 && (
            <div className="rounded-xl bg-white p-3">
              <p className="mb-1 font-semibold text-slate-700">Suggested Actions</p>
              <ul className="list-inside list-disc text-slate-600">
                {draft.nextActions.map((a, i) => <li key={i}>{a}</li>)}
              </ul>
            </div>
          )}
          {draft.citations.length > 0 && (
            <div className="rounded-xl bg-white p-3">
              <p className="mb-1 font-semibold text-slate-700">📚 Policy Citations</p>
              {draft.citations.map((c, i) => (
                <p key={i} className="text-xs text-slate-500">• {c.sectionTitle}</p>
              ))}
            </div>
          )}
        </motion.div>
      )}
      </AnimatePresence>
    </div>
  )
}

const auditIcons: Record<number, string> = {
  0: '🆕', 1: '💬', 2: '🔒', 3: '👤', 4: '🔄', 5: '🏷️', 6: '🚨', 7: '📈',
  8: '📚', 9: '🔁', 10: '✅', 11: '❌', 12: '🤖',
}

function AuditTimeline({ ticketId }: { ticketId: string }) {
  const [open, setOpen] = useState(false)
  const { data, isLoading } = useQuery({
    queryKey: ['audit', ticketId],
    queryFn: () => api<AuditEvent[]>(`/api/tickets/${ticketId}/audit`),
    enabled: open,
  })

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 text-sm shadow-sm">
      <button
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between font-semibold"
      >
        🕘 History
        <span className="text-slate-400">{open ? '▲' : '▼'}</span>
      </button>
      {open && (
        <div className="mt-3">
          {isLoading && <p className="text-slate-500">Loading…</p>}
          <ol className="relative space-y-3 border-l-2 border-slate-100 pl-4">
            {data?.map((e) => (
              <li key={e.id} className="relative">
                <span className="absolute -left-[1.45rem] top-0.5 text-xs">{auditIcons[e.eventType] ?? '•'}</span>
                <p className="font-medium text-slate-700">
                  {AuditEventTypes[e.eventType] ?? 'Event'}
                  {e.beforeState && e.afterState && (
                    <span className="font-normal text-slate-500"> — {e.beforeState} → {e.afterState}</span>
                  )}
                </p>
                <p className="text-xs text-slate-400">
                  {e.actorName} · {new Date(e.timestamp).toLocaleString('en-US')}
                </p>
                {e.details && <p className="mt-0.5 text-xs text-slate-500">{e.details}</p>}
              </li>
            ))}
            {data && data.length === 0 && <p className="text-slate-500">No records.</p>}
          </ol>
        </div>
      )}
    </div>
  )
}

export default function TicketDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { session } = useAuth()
  const queryClient = useQueryClient()
  const isStaff = session?.role === 'SupportAgent' || session?.role === 'Admin'

  const [message, setMessage] = useState('')
  const [isInternal, setIsInternal] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  const { data: ticket, isLoading, error } = useQuery({
    queryKey: ['ticket', id],
    queryFn: () => api<TicketDetail>(`/api/tickets/${id}`),
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['ticket', id] })
    queryClient.invalidateQueries({ queryKey: ['queue'] })
    queryClient.invalidateQueries({ queryKey: ['audit', id] })
  }

  const sendMessage = useMutation({
    mutationFn: () =>
      api(
        isInternal ? `/api/tickets/${id}/internal-notes` : `/api/tickets/${id}/messages`,
        { method: 'POST', body: JSON.stringify({ content: message }) },
      ),
    onSuccess: () => { setMessage(''); setActionError(null); invalidate() },
    onError: (err) => setActionError((err as Error).message),
  })

  const transition = useMutation({
    mutationFn: (newState: number) =>
      api(`/api/tickets/${id}/transition`, { method: 'POST', body: JSON.stringify({ newState }) }),
    onSuccess: () => { setActionError(null); invalidate() },
    onError: (err) => setActionError((err as Error).message),
  })

  const assignToMe = useMutation({
    mutationFn: () =>
      api(`/api/tickets/${id}/assign`, { method: 'POST', body: JSON.stringify({ agentId: session?.userId }) }),
    onSuccess: () => { setActionError(null); invalidate() },
    onError: (err) => setActionError((err as Error).message),
  })

  if (isLoading) return <p className="text-slate-500">Loading…</p>
  if (error || !ticket) return <p className="text-red-600">{(error as Error)?.message ?? 'Ticket not found'}</p>

  const nextStates = ValidTransitions[ticket.state] ?? []
  const isTerminal = ticket.state === 6 || ticket.state === 7

  function submitMessage(e: FormEvent) {
    e.preventDefault()
    sendMessage.mutate()
  }

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <div className="space-y-4 lg:col-span-2">
        {/* Header */}
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="mb-2 flex flex-wrap items-center gap-2">
            <SlaBadge atRisk={ticket.slaRisk} />
            <PriorityBadge priority={ticket.priority} />
            <CategoryBadge category={ticket.category} />
            <StateBadge state={ticket.state} />
          </div>
          <h1 className="text-xl font-bold">{ticket.subject}</h1>
          <p className="mt-1 text-xs text-slate-500">
            {ticket.ticketNumber} · PNR: {ticket.pnr ?? '—'} · {new Date(ticket.createdAt).toLocaleString('en-US')}
          </p>
          <p className="mt-3 whitespace-pre-wrap text-sm text-slate-700">{ticket.description}</p>
        </div>

        {/* Messages */}
        <div className="space-y-3">
          {ticket.messages.map((m) => {
            const own = m.authorUserId === session?.userId
            return (
              <div
                key={m.id}
                className={`rounded-2xl border p-4 text-sm shadow-sm ${
                  m.isInternal
                    ? 'border-amber-300 bg-amber-50'
                    : own
                      ? 'border-brand-200 bg-brand-50'
                      : 'border-slate-200 bg-white'
                }`}
              >
                <div className="mb-1 flex items-center justify-between text-xs text-slate-500">
                  <span className="font-medium text-slate-700">
                    {m.authorName} {m.isInternal && <span className="text-amber-700">· 🔒 Internal Note</span>}
                  </span>
                  <span>{new Date(m.createdAt).toLocaleString('en-US')}</span>
                </div>
                <p className="whitespace-pre-wrap">{m.content}</p>
              </div>
            )
          })}
          {ticket.messages.length === 0 && (
            <p className="rounded-2xl border-2 border-dashed border-slate-200 p-6 text-center text-sm text-slate-500">
              No messages yet.
            </p>
          )}
        </div>

        {/* Reply box */}
        {!isTerminal && (
          <form onSubmit={submitMessage} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
            <textarea
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              rows={3}
              required
              maxLength={10000}
              placeholder={isInternal ? 'Internal note (hidden from the passenger)…' : 'Write your message…'}
              className="w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm transition focus:border-brand-500 focus:outline-none focus:ring-4 focus:ring-brand-100"
            />
            <div className="mt-2 flex items-center justify-between">
              {isStaff ? (
                <label className="flex items-center gap-1.5 text-sm text-amber-700">
                  <input type="checkbox" checked={isInternal} onChange={(e) => setIsInternal(e.target.checked)} />
                  🔒 Send as internal note
                </label>
              ) : <span />}
              <button type="submit" disabled={sendMessage.isPending} className={buttonPrimaryCls}>
                {sendMessage.isPending ? 'Sending…' : 'Send'}
              </button>
            </div>
          </form>
        )}

        <ErrorBanner message={actionError} />
      </div>

      {/* Sidebar */}
      <div className="space-y-4">
        {isStaff && (
          <>
            <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
              <h3 className="mb-3 font-semibold">Actions</h3>
              {!ticket.assignedToAgentId && !isTerminal && (
                <button
                  onClick={() => assignToMe.mutate()}
                  disabled={assignToMe.isPending}
                  className="mb-2 w-full rounded-xl bg-emerald-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:opacity-50"
                >
                  Assign to Me
                </button>
              )}
              {nextStates.length > 0 ? (
                <div className="space-y-2">
                  {nextStates.map((s) => (
                    <button
                      key={s}
                      onClick={() => transition.mutate(s)}
                      disabled={transition.isPending}
                      className="w-full rounded-xl border border-slate-300 px-3 py-2 text-sm font-medium transition hover:bg-slate-50 disabled:opacity-50"
                    >
                      → {TicketStates[s]}
                    </button>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-slate-500">No transitions from this state (terminal).</p>
              )}
            </div>
            {!isTerminal && <DraftReplyPanel ticketId={ticket.id} onUse={(text) => { setIsInternal(false); setMessage(text) }} />}
            <AuditTimeline ticketId={ticket.id} />
          </>
        )}

        <div className="rounded-2xl border border-slate-200 bg-white p-4 text-sm shadow-sm">
          <h3 className="mb-3 font-semibold">SLA Details</h3>
          <dl className="space-y-2 text-slate-600">
            <div className="flex justify-between">
              <dt>First response due</dt>
              <dd>{new Date(ticket.firstResponseDueAt).toLocaleString('en-US')}</dd>
            </div>
            <div className="flex justify-between">
              <dt>Resolution due</dt>
              <dd>{new Date(ticket.resolutionDueAt).toLocaleString('en-US')}</dd>
            </div>
            {ticket.firstResponseAt && (
              <div className="flex justify-between">
                <dt>First response</dt>
                <dd>{new Date(ticket.firstResponseAt).toLocaleString('en-US')}</dd>
              </div>
            )}
            {ticket.resolvedAt && (
              <div className="flex justify-between">
                <dt>Resolved</dt>
                <dd>{new Date(ticket.resolvedAt).toLocaleString('en-US')}</dd>
              </div>
            )}
          </dl>
        </div>
      </div>
    </div>
  )
}
