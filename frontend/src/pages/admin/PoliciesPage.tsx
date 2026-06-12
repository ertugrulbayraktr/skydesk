import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../api/client'
import { PolicyStatuses, type PolicyList } from '../../api/types'
import { Card, EmptyState, ErrorBanner, SkeletonRows, buttonPrimaryCls, buttonSecondaryCls, inputCls } from '../../components/ui'

const statusColors: Record<number, string> = {
  0: 'bg-amber-100 text-amber-700',
  1: 'bg-emerald-100 text-emerald-700',
  2: 'bg-slate-200 text-slate-600',
}

export default function PoliciesPage() {
  const queryClient = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['policies'],
    queryFn: () => api<PolicyList>('/api/policies?pageSize=50'),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['policies'] })

  function resetForm() {
    setShowForm(false)
    setEditingId(null)
    setTitle('')
    setContent('')
    setError(null)
  }

  const savePolicy = useMutation({
    mutationFn: () =>
      editingId
        ? api(`/api/policies/${editingId}`, { method: 'PUT', body: JSON.stringify({ title, content }) })
        : api('/api/policies', { method: 'POST', body: JSON.stringify({ title, content }) }),
    onSuccess: () => { resetForm(); invalidate() },
    onError: (err) => setError((err as Error).message),
  })

  const publishPolicy = useMutation({
    mutationFn: (id: string) => api(`/api/policies/${id}/publish`, { method: 'POST' }),
    onSuccess: () => { setError(null); invalidate() },
    onError: (err) => setError((err as Error).message),
  })

  const archivePolicy = useMutation({
    mutationFn: (id: string) => api(`/api/policies/${id}/archive`, { method: 'POST' }),
    onSuccess: () => { setError(null); invalidate() },
    onError: (err) => setError((err as Error).message),
  })

  async function startEdit(id: string) {
    const detail = await api<{ title: string; content: string }>(`/api/policies/${id}`)
    setEditingId(id)
    setTitle(detail.title)
    setContent(detail.content)
    setShowForm(true)
    setError(null)
  }

  function submit(e: FormEvent) {
    e.preventDefault()
    savePolicy.mutate()
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Knowledge Base Policies</h1>
          <p className="mt-1 text-sm text-slate-500">
            Published policies are chunked and indexed for the AI reply assistant's RAG search.
          </p>
        </div>
        <button onClick={() => (showForm ? resetForm() : setShowForm(true))} className={buttonPrimaryCls}>
          {showForm ? 'Cancel' : '+ New Policy'}
        </button>
      </div>

      {showForm && (
        <Card className="mb-6">
          <form onSubmit={submit} className="space-y-4">
            <h3 className="font-semibold">{editingId ? 'Edit Draft (version will increment)' : 'New Policy Draft'}</h3>
            <div>
              <label className="mb-1.5 block text-sm font-medium">Title</label>
              <input value={title} onChange={(e) => setTitle(e.target.value)} required maxLength={500} className={inputCls} />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium">
                Content (Markdown — <code className="rounded bg-slate-100 px-1">##</code> headings become chunk boundaries)
              </label>
              <textarea
                value={content}
                onChange={(e) => setContent(e.target.value)}
                required
                rows={12}
                placeholder={'# Policy Name\n\n## Section 1\nContent...\n\n## Section 2\nContent...'}
                className={`${inputCls} font-mono text-xs`}
              />
            </div>
            <ErrorBanner message={error} />
            <button type="submit" disabled={savePolicy.isPending} className={buttonPrimaryCls}>
              {savePolicy.isPending ? 'Saving…' : editingId ? 'Save Changes' : 'Save as Draft'}
            </button>
          </form>
        </Card>
      )}

      {!showForm && <ErrorBanner message={error} />}

      {isLoading && <SkeletonRows count={4} />}

      <div className="mt-4 space-y-3">
        {data?.policies.map((p) => (
          <Card key={p.id} className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <div className="flex items-center gap-2">
                <p className="font-semibold">{p.title}</p>
                <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${statusColors[p.status] ?? ''}`}>
                  {PolicyStatuses[p.status]}
                </span>
              </div>
              <p className="mt-1 text-xs text-slate-500">
                v{p.version} · {p.chunkCount} chunks ·{' '}
                {p.publishedAt
                  ? `Published: ${new Date(p.publishedAt).toLocaleDateString('en-US')}`
                  : `Updated: ${new Date(p.updatedAt).toLocaleDateString('en-US')}`}
              </p>
            </div>
            <div className="flex gap-2">
              {p.status === 0 && (
                <>
                  <button onClick={() => startEdit(p.id)} className={buttonSecondaryCls}>
                    ✏️ Edit
                  </button>
                  <button
                    onClick={() => publishPolicy.mutate(p.id)}
                    disabled={publishPolicy.isPending}
                    className={buttonSecondaryCls}
                  >
                    🚀 Publish &amp; Index
                  </button>
                </>
              )}
              {p.status === 1 && (
                <button
                  onClick={() => archivePolicy.mutate(p.id)}
                  disabled={archivePolicy.isPending}
                  className={buttonSecondaryCls}
                >
                  🗄️ Archive
                </button>
              )}
            </div>
          </Card>
        ))}
        {data && data.policies.length === 0 && (
          <EmptyState icon="📚" title="No policies yet" hint="Create your first policy document." />
        )}
      </div>
    </div>
  )
}
