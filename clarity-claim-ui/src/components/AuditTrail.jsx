import { useEffect, useState } from 'react'
import { api } from '../services/api'

const STATUS_STYLE = {
  Success: 'text-green-600 dark:text-green-400',
  Fallback: 'text-amber-500',
  Error: 'text-red-500',
}

// Reads back the audit trail written by each pipeline step (Transcription,
// SoapGeneration, Validation) for this visit -- a visible record of what ran,
// when, how long it took, and whether it used live inference or a fallback.
export function AuditTrail({ visitId, refreshToken }) {
  const [entries, setEntries] = useState(null)

  useEffect(() => {
    if (!visitId) return
    api
      .getVisitHistory(visitId)
      .then((h) => setEntries(h.auditTrail ?? []))
      .catch(() => setEntries([]))
  }, [visitId, refreshToken])

  if (!visitId || !entries || entries.length === 0) return null

  return (
    <div className="hc-card p-4">
      <div className="text-xs uppercase tracking-wider text-[var(--hc-text-muted)] font-semibold mb-3">
        Audit Trail
      </div>
      <div className="space-y-2">
        {entries.map((e) => (
          <div key={e.auditLogId} className="flex items-center justify-between text-sm border-l-2 border-[var(--hc-border)] pl-3">
            <div>
              <span className="text-[var(--hc-text)] font-medium">{e.stepName}</span>{' '}
              <span className={STATUS_STYLE[e.status] ?? 'text-[var(--hc-text-muted)]'}>· {e.status}</span>
            </div>
            <div className="text-xs text-[var(--hc-text-muted)] font-mono">
              {e.durationMs != null ? `${e.durationMs}ms` : ''} {new Date(e.createdAt).toLocaleTimeString()}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
