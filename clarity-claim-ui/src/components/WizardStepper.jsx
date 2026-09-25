export const DOCTOR_STEPS = [
  { label: 'Encounter', hint: 'Capture the visit' },
  { label: 'SOAP Note', hint: 'Send for review' },
]

export const REVIEWER_STEPS = [
  { label: 'SOAP Note', hint: 'As sent by the doctor' },
  { label: 'Validation', hint: 'Medical necessity check' },
]

// Large, readable step indicator -- replaces the old small pill nav. Takes a
// `steps` list so the doctor and reviewer flows can each show their own.
export function WizardStepper({ steps = DOCTOR_STEPS, activeIndex, onNavigate, patientName }) {
  const STEPS = steps
  return (
    <div className="bg-[var(--hc-surface)] border-b border-[var(--hc-border)] px-6 md:px-10 py-5">
      {patientName && (
        <div className="text-xs uppercase tracking-wider text-[var(--hc-text-muted)] mb-3">
          Encounter for <span className="text-[var(--hc-text)] font-medium">{patientName}</span>
        </div>
      )}
      <div className="flex items-center">
        {STEPS.map((step, i) => {
          const state = i < activeIndex ? 'done' : i === activeIndex ? 'active' : 'upcoming'
          const clickable = i <= activeIndex

          return (
            <div key={step.label} className="flex items-center flex-1 last:flex-none">
              <button
                onClick={() => clickable && onNavigate(i)}
                disabled={!clickable}
                className={`flex items-center gap-3 ${clickable ? 'cursor-pointer' : 'cursor-not-allowed'}`}
              >
                <div
                  className={`w-11 h-11 rounded-full flex items-center justify-center text-base font-semibold border-2 transition
                    ${state === 'active' ? 'bg-[var(--hc-primary)] border-[var(--hc-primary)] text-white' : ''}
                    ${state === 'done' ? 'bg-[var(--hc-primary)]/15 border-[var(--hc-primary)] text-[var(--hc-primary)]' : ''}
                    ${state === 'upcoming' ? 'bg-[var(--hc-surface-2)] border-[var(--hc-border)] text-[var(--hc-text-muted)]' : ''}
                  `}
                >
                  {state === 'done' ? (
                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
                    </svg>
                  ) : (
                    i + 1
                  )}
                </div>
                <div className="text-left hidden sm:block">
                  <div className={`text-base font-semibold ${state === 'upcoming' ? 'text-[var(--hc-text-muted)]' : 'text-[var(--hc-text)]'}`}>
                    {step.label}
                  </div>
                  <div className="text-xs text-[var(--hc-text-muted)]">{step.hint}</div>
                </div>
              </button>

              {i < STEPS.length - 1 && (
                <div
                  className={`flex-1 h-0.5 mx-4 rounded ${i < activeIndex ? 'bg-[var(--hc-primary)]' : 'bg-[var(--hc-border)]'}`}
                />
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
