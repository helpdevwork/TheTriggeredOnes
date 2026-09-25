export function TranscriptStream({ text }) {
  if (!text) return null

  return (
    <div className="mt-4 hc-card p-4">
      <div className="text-xs uppercase tracking-wider text-[var(--hc-text-muted)] mb-2">Transcript</div>
      <p className="text-sm text-[var(--hc-text)] leading-relaxed whitespace-pre-wrap">{text}</p>
    </div>
  )
}
