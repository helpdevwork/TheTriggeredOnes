import { useEffect, useState } from 'react'
import { api } from '../services/api'

// PS 35 -- Regulatory Intelligence panel.
export function RegulatoryPanel() {
  const [updates, setUpdates] = useState([])

  useEffect(() => {
    api.getRegulatoryUpdates().then(setUpdates).catch(() => setUpdates([]))
  }, [])

  if (updates.length === 0) return null

  return (
    <div className="hc-card p-4">
      <div className="text-xs uppercase tracking-wider text-[var(--hc-text-muted)] font-semibold mb-3">
        Regulatory Updates
      </div>
      <div className="space-y-2">
        {updates.map((u, i) => (
          <div key={i} className="text-sm border-l-2 border-amber-500/50 pl-3">
            <div className="font-mono text-xs text-amber-500">
              {u.lcdId} · {u.title}
            </div>
            <div className="text-[var(--hc-text-muted)] text-xs mt-0.5">{u.changeDescription}</div>
          </div>
        ))}
      </div>
    </div>
  )
}
