export const TicketStates = [
  'New', 'Triaged', 'Assigned', 'InProgress', 'WaitingOnPassenger', 'Resolved', 'Closed', 'Cancelled',
] as const

export const Priorities = ['P3', 'P2', 'P1', 'P0'] as const

export const Categories = [
  'General', 'Booking', 'Cancellation', 'Refund', 'Baggage', 'FlightDelay',
  'FlightCancellation', 'SeatChange', 'SpecialAssistance', 'MealRequest', 'Other',
] as const

// Mirrors Domain.Services.TicketStateMachine. Used only to decide which
// transition buttons to render; the backend remains the source of truth.
export const ValidTransitions: Record<number, number[]> = {
  0: [1, 2, 7],
  1: [2, 7],
  2: [3, 7],
  3: [4, 5, 7],
  4: [3, 5, 7],
  5: [6, 3],
  6: [],
  7: [],
}

export interface TicketSummary {
  id: string
  ticketNumber: string
  subject: string
  state: number
  priority: number
  category: number
  pnr?: string
  assignedToAgentId?: string
  assignedToAgentName?: string
  createdAt: string
  updatedAt: string
  slaRisk: boolean
}

export interface TicketMessage {
  id: string
  authorUserId: string
  authorName: string
  content: string
  isInternal: boolean
  createdAt: string
}

export interface TicketDetail extends TicketSummary {
  description: string
  firstResponseDueAt: string
  resolutionDueAt: string
  firstResponseAt?: string
  resolvedAt?: string
  closedAt?: string
  messages: TicketMessage[]
}

export interface PagedTickets {
  tickets: TicketSummary[]
  totalCount: number
  pageNumber: number
  pageSize: number
}

export interface DraftReply {
  draftText: string
  citations: { sectionTitle: string; content: string }[]
  missingInfoQuestions: string[]
  nextActions: string[]
  riskFlags: string[]
}

export interface AppNotification {
  id: string
  title: string
  message: string
  isRead: boolean
  ticketId?: string
  createdAt: string
}

export interface NotificationList {
  notifications: AppNotification[]
  totalCount: number
  unreadCount: number
}

export const PolicyStatuses = ['Draft', 'Published', 'Archived'] as const

export const AuditEventTypes = [
  'Created', 'MessageAdded', 'InternalNoteAdded', 'Assigned', 'StateChanged',
  'PriorityChanged', 'SlaBreached', 'Escalated', 'PolicyPublished',
  'PolicyReindexed', 'TicketClosed', 'TicketCancelled', 'DraftFeedback',
] as const

export interface AuditEvent {
  id: string
  timestamp: string
  actorType: number
  actorName?: string
  eventType: number
  beforeState?: string
  afterState?: string
  details?: string
}

export interface DashboardStats {
  totalTickets: number
  openTickets: number
  slaAtRiskCount: number
  slaBreachedCount: number
  avgFirstResponseMinutes?: number
  draftFeedbackAccepted: number
  draftFeedbackRejected: number
  byState: Record<string, number>
  byCategory: Record<string, number>
  byPriority: Record<string, number>
  last7Days: { date: string; count: number }[]
}

export interface PolicySummary {
  id: string
  title: string
  status: number
  version: number
  publishedAt?: string
  createdAt: string
  updatedAt: string
  chunkCount: number
}

export interface PolicyList {
  policies: PolicySummary[]
  totalCount: number
}
