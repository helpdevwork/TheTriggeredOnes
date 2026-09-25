export function SOAPPanel({ label, value, onChange, readOnly = false }) {
  return (
    <div className="hc-card p-4 flex flex-col">
      <label className="text-xs uppercase tracking-wider text-[var(--hc-primary)] font-semibold mb-2">
        {label}
      </label>
      <textarea
        className={`hc-input flex-1 min-h-32 resize-y ${readOnly ? 'opacity-80 cursor-default' : ''}`}
        value={value}
        onChange={(e) => !readOnly && onChange(e.target.value)}
        readOnly={readOnly}
      />
    </div>
  )
}
