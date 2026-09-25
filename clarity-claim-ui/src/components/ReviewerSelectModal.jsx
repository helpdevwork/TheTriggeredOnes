import { useEffect, useState } from 'react'
import { api } from '../services/api'

function initials(name) {
  if (!name) return '?'
  return name.split(' ').map((p) => p[0]).join('').slice(0, 2).toUpperCase()
}

// Table of review-team members the doctor picks from when sending a SOAP note
// for review. Selecting a row assigns that reviewer and opens their tab.
export function ReviewerSelectModal({ onAssign, onClose, assigning }) {
  const [reviewers, setReviewers] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => {
    api
      .listReviewTeam()
      .then(setReviewers)
      .catch(() => setError('Could not load the review team list.'))
  }, [])

  return (
    <div className="fixed inset-0 z-30 flex items-center justify-center bg-black/50 px-4">
      <div className="w-full max-w-lg hc-card p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-[var(--hc-text)]">Send for Review</h2>
          <button onClick={onClose} className="text-[var(--hc-text-muted)] hover:text-[var(--hc-text)]">
            ✕
          </button>
        </div>
        <p className="text-sm text-[var(--hc-text-muted)] mb-4">
          Choose a reviewer to run the medical-necessity validation on this note.
        </p>

        {error && <p className="text-red-500 text-sm mb-3">{error}</p>}

        {!reviewers ? (
          <p className="text-sm text-[var(--hc-text-muted)]">Loading review team...</p>
        ) : (
          <div className="divide-y divide-[var(--hc-border)] border border-[var(--hc-border)] rounded-lg overflow-hidden">
            {reviewers.map((r) => (
              <button
                key={r.reviewTeamMemberId}
                onClick={() => onAssign(r.reviewTeamMemberId, r.fullName)}
                disabled={assigning}
                className="w-full flex items-center gap-3 px-4 py-3 hover:bg-[var(--hc-surface-2)] text-left disabled:opacity-50"
              >
                <div className="w-9 h-9 rounded-full bg-[var(--hc-accent)] flex items-center justify-center text-white text-xs font-semibold shrink-0">
                  {initials(r.fullName)}
                </div>
                <div className="flex-1">
                  <div className="text-sm font-medium text-[var(--hc-text)]">{r.fullName}</div>
                  <div className="text-xs text-[var(--hc-text-muted)]">{r.specialty || 'Reviewer'}</div>
                </div>
                <span className="text-xs text-[var(--hc-primary)] font-medium">
                  {assigning ? 'Sending...' : 'Select'}
                </span>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
