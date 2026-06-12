import { describe, expect, it } from 'vitest'
import { Categories, Priorities, TicketStates, ValidTransitions } from '../api/types'

// Mirrors backend Domain.Services.TicketStateMachine. If the backend rules
// change, this test reminds us to update the client copy.
describe('ValidTransitions (client copy of the state machine)', () => {
  it('terminal states have no transitions', () => {
    expect(ValidTransitions[6]).toEqual([]) // Closed
    expect(ValidTransitions[7]).toEqual([]) // Cancelled
  })

  it('New cannot go directly to Closed', () => {
    expect(ValidTransitions[0]).not.toContain(6)
  })

  it('Resolved can be closed or reopened to InProgress', () => {
    expect(ValidTransitions[5]).toEqual(expect.arrayContaining([6, 3]))
  })

  it('every source state is a valid TicketState index', () => {
    for (const key of Object.keys(ValidTransitions)) {
      expect(Number(key)).toBeLessThan(TicketStates.length)
    }
  })
})

describe('enum label tables', () => {
  it('has 8 states, 4 priorities, 11 categories (matches backend enums)', () => {
    expect(TicketStates).toHaveLength(8)
    expect(Priorities).toHaveLength(4)
    expect(Categories).toHaveLength(11)
  })
})
