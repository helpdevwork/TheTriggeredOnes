import { useState } from 'react'

const SEVERITY_STYLE = {
  Critical: { border: 'border-red-500/40', bg: 'bg-red-500/10', text: 'text-red-500' },
  Major: { border: 'border-amber-500/40', bg: 'bg-amber-500/10', text: 'text-amber-500' },
  Minor: { border: 'border-blue-500/40', bg: 'bg-blue-500/10', text: 'text-blue-500' },
}

export function GapChecklist({ gaps, fixes, onApplyFix }) {
  const [openIndex, setOpenIndex] = useState(null)

  if (!gaps || gaps.length === 0) {
    return (
      <div className="bg-green-500/10 border border-green-500/30 rounded-xl p-6 text-green-600 dark:text-green-400 text-base font-medium">
        No documentation gaps found — this note meets policy requirements.
      </div>
    )
  }

  return (
    <div className="space-y-3">
      {gaps.map((gap, i) => {
        const fix = fixes?.find((f) => f.gapIndex === i)
        const isOpen = openIndex === i
        const style = SEVERITY_STYLE[gap.severity] ?? SEVERITY_STYLE.Minor

        return (
          <div key={i} className="hc-card p-5">
            <div className="flex items-start justify-between gap-3">
              <div className="flex-1">
                <span className={`inline-block text-xs font-bold uppercase tracking-wide px-2.5 py-1 rounded-md border ${style.border} ${style.bg} ${style.text}`}>
                  {gap.severity}
                </span>
                <div className="text-xs text-[var(--hc-text-muted)] mt-2 font-mono">{gap.policyRef}</div>
                <div className="text-base text-[var(--hc-text)] mt-1 font-medium">{gap.requirement}</div>
                <div className="text-sm text-[var(--hc-text-muted)] mt-2 italic">Currently: {gap.currentDoc}</div>
              </div>
              {fix && (
                <button
                  onClick={() => setOpenIndex(isOpen ? null : i)}
                  className="shrink-0 bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white text-sm font-medium px-4 py-2 rounded-lg"
                >
                  Fix This
                </button>
              )}
            </div>

            {isOpen && fix && (
              <div className="mt-4 bg-[var(--hc-surface-2)] border border-[var(--hc-primary)]/30 rounded-lg p-4">
                <div className="text-xs uppercase tracking-wider text-[var(--hc-primary)] font-semibold mb-1">
                  Suggested language
                </div>
                <p className="text-sm text-[var(--hc-text)]">{fix.suggestedLanguage}</p>
                {onApplyFix && (
                  <button
                    onClick={() => onApplyFix(fix)}
                    className="mt-3 text-sm text-[var(--hc-primary)] hover:underline font-medium"
                  >
                    Insert into note
                  </button>
                )}
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}
