import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/AuthContext'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import MyTicketsPage from './pages/passenger/MyTicketsPage'
import QueuePage from './pages/agent/QueuePage'
import DashboardPage from './pages/agent/DashboardPage'
import PoliciesPage from './pages/admin/PoliciesPage'
import TicketDetailPage from './pages/TicketDetailPage'
import type { ReactElement } from 'react'

function RequireAuth({
  children,
  staffOnly = false,
  adminOnly = false,
}: { children: ReactElement; staffOnly?: boolean; adminOnly?: boolean }) {
  const { session } = useAuth()
  if (!session) return <Navigate to="/login" replace />
  const isStaff = session.role === 'SupportAgent' || session.role === 'Admin'
  if (adminOnly && session.role !== 'Admin') return <Navigate to="/" replace />
  if (staffOnly && !isStaff) return <Navigate to="/my-tickets" replace />
  return children
}

function Home() {
  const { session } = useAuth()
  if (!session) return <Navigate to="/login" replace />
  const isStaff = session.role === 'SupportAgent' || session.role === 'Admin'
  return <Navigate to={isStaff ? '/agent/queue' : '/my-tickets'} replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<Layout />}>
        <Route path="/" element={<Home />} />
        <Route
          path="/my-tickets"
          element={<RequireAuth><MyTicketsPage /></RequireAuth>}
        />
        <Route
          path="/agent/queue"
          element={<RequireAuth staffOnly><QueuePage /></RequireAuth>}
        />
        <Route
          path="/agent/my-queue"
          element={<RequireAuth staffOnly><QueuePage mine /></RequireAuth>}
        />
        <Route
          path="/agent/dashboard"
          element={<RequireAuth staffOnly><DashboardPage /></RequireAuth>}
        />
        <Route
          path="/admin/policies"
          element={<RequireAuth adminOnly><PoliciesPage /></RequireAuth>}
        />
        <Route
          path="/tickets/:id"
          element={<RequireAuth><TicketDetailPage /></RequireAuth>}
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

