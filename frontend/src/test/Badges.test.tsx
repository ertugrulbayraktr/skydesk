import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { CategoryBadge, PriorityBadge, SlaBadge, StateBadge } from '../components/Badges'

describe('Badges', () => {
  it('renders state label from numeric enum', () => {
    render(<StateBadge state={3} />)
    expect(screen.getByText('InProgress')).toBeInTheDocument()
  })

  it('renders priority label', () => {
    render(<PriorityBadge priority={3} />)
    expect(screen.getByText('P0')).toBeInTheDocument()
  })

  it('renders category label', () => {
    render(<CategoryBadge category={4} />)
    expect(screen.getByText('Baggage')).toBeInTheDocument()
  })

  it('SlaBadge renders only when at risk', () => {
    const { rerender } = render(<SlaBadge atRisk={false} />)
    expect(screen.queryByText('SLA Risk')).not.toBeInTheDocument()

    rerender(<SlaBadge atRisk={true} />)
    expect(screen.getByText('SLA Risk')).toBeInTheDocument()
  })
})
