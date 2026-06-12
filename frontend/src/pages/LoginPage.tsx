import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useAuth } from '../auth/AuthContext'
import { Button, ErrorBanner, LogoMark, inputCls } from '../components/ui'

type Tab = 'passenger' | 'staff'

/** Abstract flight-route artwork: dashed orbital arcs + a glowing waypoint. */
function RouteArt() {
  return (
    <svg
      viewBox="0 0 600 600"
      fill="none"
      className="pointer-events-none absolute inset-0 h-full w-full text-white"
      preserveAspectRatio="xMidYMid slice"
      aria-hidden="true"
    >
      <circle cx="300" cy="300" r="260" stroke="currentColor" strokeOpacity="0.08" strokeWidth="1.5" strokeDasharray="2 10" />
      <circle cx="300" cy="300" r="190" stroke="currentColor" strokeOpacity="0.10" strokeWidth="1.5" strokeDasharray="2 8" />
      <circle cx="300" cy="300" r="120" stroke="currentColor" strokeOpacity="0.12" strokeWidth="1.5" strokeDasharray="2 6" />
      <path d="M40 420 C 180 240, 420 240, 560 360" stroke="currentColor" strokeOpacity="0.30" strokeWidth="2" strokeLinecap="round" strokeDasharray="1 12" />
      <path d="M80 180 C 220 320, 400 340, 540 200" stroke="currentColor" strokeOpacity="0.18" strokeWidth="2" strokeLinecap="round" strokeDasharray="1 12" />
      <circle className="route-dot" cx="404" cy="262" r="5" fill="#60a5fa" />
      <circle cx="404" cy="262" r="12" stroke="#60a5fa" strokeOpacity="0.35" strokeWidth="1.5" />
      <circle cx="143" cy="243" r="3.5" fill="currentColor" fillOpacity="0.5" />
      <circle cx="488" cy="318" r="3.5" fill="currentColor" fillOpacity="0.5" />
    </svg>
  )
}

export default function LoginPage() {
  const [tab, setTab] = useState<Tab>('passenger')
  const [pnr, setPnr] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const { passengerLogin, staffLogin } = useAuth()
  const navigate = useNavigate()

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      if (tab === 'passenger') {
        await passengerLogin(pnr.trim().toUpperCase(), lastName.trim())
        navigate('/my-tickets')
      } else {
        const session = await staffLogin(email.trim(), password)
        navigate(session.role === 'Passenger' ? '/my-tickets' : '/agent/queue')
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      {/* Brand panel */}
      <div className="relative hidden items-center justify-center overflow-hidden bg-gradient-to-br from-navy-900 via-navy-800 to-brand-800 p-12 lg:flex">
        <RouteArt />
        <div className="absolute -left-24 -top-24 h-96 w-96 rounded-full bg-brand-500/15 blur-3xl" />
        <div className="absolute -bottom-32 -right-16 h-[28rem] w-[28rem] rounded-full bg-brand-400/10 blur-3xl" />
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.6, ease: 'easeOut' }}
          className="relative max-w-md text-white"
        >
          <LogoMark className="mb-8 h-12 w-12 text-brand-300" />
          <h1 className="text-4xl font-extrabold leading-tight">
            Post-booking support,<br />engineered for trust.
          </h1>
          <p className="mt-4 text-lg text-slate-300">
            Manage cancellations, refunds, baggage and delay requests in one place —
            with AI-powered triage and policy-cited reply drafts.
          </p>
          <ul className="mt-8 space-y-3 text-sm text-slate-300">
            <li className="flex items-center gap-3"><span className="h-1.5 w-1.5 rounded-full bg-brand-400" />Automatic category &amp; priority detection</li>
            <li className="flex items-center gap-3"><span className="h-1.5 w-1.5 rounded-full bg-brand-400" />Policy-cited reply assistant (RAG)</li>
            <li className="flex items-center gap-3"><span className="h-1.5 w-1.5 rounded-full bg-brand-400" />SLA tracking with auto-escalation</li>
            <li className="flex items-center gap-3"><span className="h-1.5 w-1.5 rounded-full bg-brand-400" />Role-based access &amp; full audit trail</li>
          </ul>
        </motion.div>
      </div>

      {/* Form panel */}
      <div className="flex items-center justify-center bg-slate-50 px-6 py-12">
        <motion.div
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ type: 'spring', stiffness: 260, damping: 24 }}
          className="w-full max-w-sm"
        >
          <div className="mb-8 text-center lg:text-left">
            <LogoMark className="mx-auto mb-4 h-10 w-10 text-brand-600 lg:mx-0" />
            <h2 className="text-2xl font-bold text-slate-900">Welcome back</h2>
            <p className="mt-1 text-sm text-slate-500">Sign in to continue</p>
          </div>

          <div className="mb-6 grid grid-cols-2 rounded-2xl bg-slate-200/70 p-1 text-sm font-semibold">
            {(
              [
                ['passenger', 'Passenger'],
                ['staff', 'Staff'],
              ] as [Tab, string][]
            ).map(([value, label]) => (
              <button
                key={value}
                type="button"
                onClick={() => { setTab(value); setError(null) }}
                className={`rounded-xl py-2.5 transition ${
                  tab === value ? 'bg-white text-brand-700 shadow' : 'text-slate-500 hover:text-slate-700'
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          <form onSubmit={submit} className="space-y-4">
            {tab === 'passenger' ? (
              <>
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-slate-700">PNR Code</label>
                  <input
                    value={pnr}
                    onChange={(e) => setPnr(e.target.value)}
                    placeholder="ABC123"
                    required
                    maxLength={10}
                    autoComplete="off"
                    className={`${inputCls} font-mono uppercase tracking-widest`}
                  />
                </div>
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-slate-700">Last Name</label>
                  <input
                    value={lastName}
                    onChange={(e) => setLastName(e.target.value)}
                    required
                    autoComplete="family-name"
                    className={inputCls}
                  />
                </div>
              </>
            ) : (
              <>
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-slate-700">Email</label>
                  <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                    autoComplete="email"
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-slate-700">Password</label>
                  <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    autoComplete="current-password"
                    className={inputCls}
                  />
                </div>
              </>
            )}

            <ErrorBanner message={error} />

            <Button type="submit" loading={busy} className="w-full py-3">
              {busy ? 'Signing in…' : tab === 'passenger' ? 'Verify PNR' : 'Sign In'}
            </Button>
          </form>

          <p className="mt-6 text-center text-xs text-slate-400">
            Passengers sign in with the PNR code and last name on their booking.
          </p>
        </motion.div>
      </div>
    </div>
  )
}
