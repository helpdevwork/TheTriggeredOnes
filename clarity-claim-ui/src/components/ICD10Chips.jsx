export function ICD10Chips({ codes }) {
  return (
    <div className="hc-card p-4">
      <div className="text-xs uppercase tracking-wider text-[var(--hc-primary)] font-semibold mb-3">
        ICD-10 / CPT Codes
      </div>
      {(!codes || codes.length === 0) ? (
        <div className="text-sm text-[var(--hc-text-muted)] italic">No codes generated yet</div>
      ) : (
        <div className="flex flex-wrap gap-2">
          {codes.map((c, i) => (
            <div
              key={i}
              className="bg-[var(--hc-surface-2)] border border-[var(--hc-border)] rounded-lg px-3 py-2 flex items-center gap-2"
              title={c.description}
            >
              <span className="font-mono text-sm text-[var(--hc-primary)]">{c.code}</span>
              <span className="text-xs text-[var(--hc-text-muted)] max-w-48 truncate">{c.description}</span>
              <span className="text-xs font-mono text-[var(--hc-text-muted)]">
                {Math.round((c.confidence ?? 0) * 100)}%
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
