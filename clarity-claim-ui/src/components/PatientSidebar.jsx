export function PatientSidebar({ patient }) {
  if (!patient) {
    return (
      <aside className="w-72 shrink-0 bg-[var(--hc-surface)] border-r border-[var(--hc-border)] p-6 text-[var(--hc-text-muted)] text-sm">
        Loading patient...
      </aside>
    )
  }

  return (
    <aside className="w-72 shrink-0 bg-[var(--hc-surface)] border-r border-[var(--hc-border)] p-6 space-y-5 overflow-y-auto">
      <div>
        <div className="text-xs uppercase tracking-wider text-[var(--hc-text-muted)] mb-1">Patient</div>
        <div className="text-lg font-semibold text-[var(--hc-text)]">{patient.fullName}</div>
        <div className="text-xs text-[var(--hc-text-muted)] mt-1">
          DOB {patient.dateOfBirth ? new Date(patient.dateOfBirth).toLocaleDateString() : '—'} · {patient.gender}
        </div>
      </div>

      <Section title="Conditions" items={patient.conditions} empty="No known conditions" />
      <Section title="Medications" items={patient.medications} empty="No current medications" />
      <Section title="Allergies" items={patient.allergies} empty="No known allergies" accent="text-red-500" />
    </aside>
  )
}

function Section({ title, items, empty, accent = 'text-[var(--hc-text)]' }) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wider text-[var(--hc-text-muted)] mb-2">{title}</div>
      {items && items.length > 0 ? (
        <ul className="space-y-1">
          {items.map((item, i) => (
            <li key={i} className={`text-sm ${accent}`}>
              {item}
            </li>
          ))}
        </ul>
      ) : (
        <div className="text-sm text-[var(--hc-text-muted)] italic">{empty}</div>
      )}
    </div>
  )
}
