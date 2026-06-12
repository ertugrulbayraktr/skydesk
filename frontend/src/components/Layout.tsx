import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { api } from '../api/client'
import { LogoMark } from './ui'
import type { NotificationList } from '../api/types'

function NotificationsBell() {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const queryClient = useQueryClient()

  const { data } = useQuery({
    queryKey: ['notifications'],
    queryFn: () => api<NotificationList>('/api/notifications?pageSize=10'),
    refetchInterval: 30_000,
  })

  const markRead = useMutation({
    mutationFn: (id: string) => api(`/api/notifications/${id}/read`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  const unread = data?.unreadCount ?? 0

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen((o) => !o)}
        className="relative rounded-xl p-2 text-lg transition hover:bg-white/10"
        aria-label="Notifications"
      >
        🔔
        {unread > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-500 px-1 text-[10px] font-bold text-white ring-2 ring-navy-800">
            {unread}
          </span>
        )}
      </button>
      {open && (
        <div className="absolute right-0 z-30 mt-2 w-96 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl">
          <div className="border-b border-slate-100 px-4 py-3">
            <p className="font-semibold text-slate-800">Notifications</p>
          </div>
          <div className="max-h-96 overflow-y-auto p-2">
            {(data?.notifications ?? []).length === 0 && (
              <p className="p-4 text-center text-sm text-slate-500">No notifications 📭</p>
            )}
            {(data?.notifications ?? []).map((n) => (
              <div
                key={n.id}
                className={`mb-1 rounded-xl p-3 text-sm transition ${
                  n.isRead ? 'opacity-50' : 'bg-brand-50'
                }`}
              >
                <div className="flex items-start justify-between gap-2">
                  <p className="font-medium text-slate-800">{n.title}</p>
                  <span className="shrink-0 text-[11px] text-slate-400">
                    {new Date(n.createdAt).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })}
                  </span>
                </div>
                <p className="mt-0.5 text-slate-600">{n.message}</p>
                {!n.isRead && (
                  <button
                    onClick={() => markRead.mutate(n.id)}
                    className="mt-1.5 text-xs font-semibold text-brand-600 hover:underline"
                  >
                    ✓ Mark as read
                  </button>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

const navLinkCls = ({ isActive }: { isActive: boolean }) =>
  `rounded-xl px-3.5 py-2 text-sm font-medium transition ${
    isActive ? 'bg-white/15 text-white' : 'text-brand-100 hover:bg-white/10 hover:text-white'
  }`

export default function Layout() {
  const { session, logout } = useAuth()
  const navigate = useNavigate()
  const isStaff = session?.role === 'SupportAgent' || session?.role === 'Admin'
  const isAdmin = session?.role === 'Admin'

  return (
    <div className="min-h-screen">
      <header className="sticky top-0 z-20 bg-gradient-to-r from-navy-900 via-navy-800 to-brand-800 shadow-lg">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-6">
            <Link to="/" className="flex items-center gap-2.5 text-lg font-extrabold tracking-tight text-white">
              <LogoMark className="h-8 w-8 text-brand-300" />
              Skydesk
            </Link>
            <nav className="hidden gap-1 sm:flex">
              {isStaff ? (
                <>
                  <NavLink to="/agent/dashboard" className={navLinkCls}>Dashboard</NavLink>
                  <NavLink to="/agent/queue" end className={navLinkCls}>Queue</NavLink>
                  <NavLink to="/agent/my-queue" className={navLinkCls}>My Tickets</NavLink>
                  {isAdmin && <NavLink to="/admin/policies" className={navLinkCls}>Policies</NavLink>}
                </>
              ) : (
                <NavLink to="/my-tickets" className={navLinkCls}>My Requests</NavLink>
              )}
            </nav>
          </div>
          <div className="flex items-center gap-2">
            {isStaff && <NotificationsBell />}
            <div className="hidden text-right sm:block">
              <p className="text-sm font-semibold text-white">{session?.name}</p>
              <p className="text-[11px] text-brand-200">{session?.role}</p>
            </div>
            <button
              onClick={() => { logout(); navigate('/login') }}
              className="ml-2 rounded-xl border border-white/25 px-3.5 py-2 text-sm font-medium text-white transition hover:bg-white/10"
            >
              Sign Out
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
      <footer className="border-t border-slate-200 py-6 text-center text-xs text-slate-400">
        Skydesk — AI-assisted post-booking support platform
      </footer>
    </div>
  )
}
