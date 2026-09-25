import { useEffect, useMemo, useState } from 'react'
import { api } from '../services/api'

function formatDate(d) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function age(dob) {
  if (!dob) return null
  const diff = Date.now() - new Date(dob).getTime()
  return Math.floor(diff / (365.25 * 24 * 60 * 60 * 1000))
}

function money(v) {
  return v === null || v === undefined ? '—' : `$${Number(v).toFixed(2)}`
}

// The landing page: a grid of every patient with primary insurance/eligibility
// and last-visit summary (from dbo.vw_PatientDashboard), a search bar (wired
// from the header), and a per-row expand panel with full demographic + eligibility
// + last-visit detail before starting an encounter.
export function PatientDashboard({ search, doctor, onStartEncounter }) {
  const [patients, setPatients] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [expandedId, setExpandedId] = useState(null)
  const [detailCache, setDetailCache] = useState({})
  const [detailLoading, setDetailLoading] = useState(false)
  const [startingId, setStartingId] = useState(null)

  useEffect(() => {
    setLoading(true)
    const load = search.trim() ? api.searchPatients(search) : api.listPatients()
    load
      .then(setPatients)
      .catch(() => setError('Could not load patients. Check that the API and ClarityClaimDb are running.'))
      .finally(() => setLoading(false))
  }, [search])

  const toggleExpand = async (patientId) => {
    if (expandedId === patientId) {
      setExpandedId(null)
      return
    }
    setExpandedId(patientId)
    if (!detailCache[patientId]) {
      setDetailLoading(true)
      try {
        const detail = await api.getPatientDetail(patientId)
        setDetailCache((prev) => ({ ...prev, [patientId]: detail }))
      } catch {
        /* row stays expanded with dashboard-level data only */
      } finally {
        setDetailLoading(false)
      }
    }
  }

  const handleStart = async (patientId) => {
    setStartingId(patientId)
    try {
      await onStartEncounter(patientId, detailCache[patientId])
    } finally {
      setStartingId(null)
    }
  }

  const columns = ['Patient', 'Insurance', 'Eligibility Window', 'Last Visit', '']

  if (loading) return <div className="p-10 text-[var(--hc-text-muted)]">Loading patients...</div>
  if (error) return <div className="p-10 text-red-500">{error}</div>

  return (
    <div className="p-6 md:p-8">
      <div className="flex items-baseline justify-between mb-5">
        <div>
          <h1 className="text-2xl font-semibold text-[var(--hc-text)]">Patients</h1>
          <p className="text-sm text-[var(--hc-text-muted)]">
            {patients.length} patient{patients.length === 1 ? '' : 's'}
            {doctor ? ` — welcome back, ${doctor.fullName.split(' ')[0]}` : ''}
          </p>
        </div>
      </div>

      <div className="hc-card overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-[var(--hc-surface-2)] text-left text-xs uppercase tracking-wider text-[var(--hc-text-muted)]">
              {columns.map((c) => (
                <th key={c} className="px-4 py-3 font-semibold">
                  {c}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {patients.map((p) => (
              <PatientRow
                key={p.patientId}
                patient={p}
                expanded={expandedId === p.patientId}
                detail={detailCache[p.patientId]}
                detailLoading={detailLoading && expandedId === p.patientId}
                starting={startingId === p.patientId}
                onToggle={() => toggleExpand(p.patientId)}
                onStart={() => handleStart(p.patientId)}
              />
            ))}
            {patients.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="px-4 py-10 text-center text-[var(--hc-text-muted)]">
                  No patients match "{search}".
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function PatientRow({ patient: p, expanded, detail, detailLoading, starting, onToggle, onStart }) {
  const patientAge = useMemo(() => age(p.dateOfBirth), [p.dateOfBirth])

  return (
    <>
      <tr className="border-t border-[var(--hc-border)] hover:bg-[var(--hc-surface-2)]/60 transition">
        <td className="px-4 py-3">
          <div className="font-medium text-[var(--hc-text)]">{p.fullName}</div>
          <div className="text-xs text-[var(--hc-text-muted)]">
            MRN {p.mrn} · {patientAge != null ? `${patientAge}y` : ''} {p.gender}
          </div>
        </td>
        <td className="px-4 py-3">
          <div className="text-[var(--hc-text)]">{p.payorName || '—'}</div>
          <div className="text-xs text-[var(--hc-text-muted)]">{p.planName || ''}</div>
        </td>
        <td className="px-4 py-3 text-[var(--hc-text)]">
          {formatDate(p.eligibilityStartDate)} – {formatDate(p.eligibilityEndDate)}
        </td>
        <td className="px-4 py-3 text-[var(--hc-text)]">
          {p.lastVisitDate ? (
            <>
              <div className="flex items-center gap-2">
                {formatDate(p.lastVisitDate)}
                <VisitStatusBadge status={p.lastVisitStatus} />
              </div>
              <div className="text-xs text-[var(--hc-text-muted)]">{p.visitCount} visit{p.visitCount === 1 ? '' : 's'}</div>
            </>
          ) : (
            <span className="text-xs text-[var(--hc-text-muted)] italic">No visits yet</span>
          )}
        </td>
        <td className="px-4 py-3 text-right">
          <button
            onClick={onToggle}
            aria-label="Expand patient details"
            className={`inline-flex items-center justify-center w-8 h-8 rounded-lg border border-[var(--hc-border)] text-[var(--hc-text-muted)] hover:bg-[var(--hc-surface-2)] transition ${expanded ? 'rotate-90' : ''}`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
            </svg>
          </button>
        </td>
      </tr>

      {expanded && (
        <tr className="border-t border-[var(--hc-border)] bg-[var(--hc-surface-2)]/40">
          <td colSpan={5} className="px-4 py-5">
            {detailLoading && !detail ? (
              <p className="text-sm text-[var(--hc-text-muted)]">Loading detail...</p>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <DetailBlock title="Demographics">
                  <Row label="Date of birth" value={formatDate(p.dateOfBirth)} />
                  <Row label="Gender" value={p.gender || '—'} />
                  <Row label="Language" value={p.preferredLanguage || '—'} />
                  <Row label="Contact" value={p.contactNumber || '—'} />
                  <Row label="Email" value={p.email || '—'} />
                </DetailBlock>

                <DetailBlock title="Insurance & Eligibility">
                  <Row label="Payor" value={p.payorName || '—'} />
                  <Row label="Plan" value={p.planName || '—'} />
                  <Row label="Group" value={p.groupName || '—'} />
                  <Row label="Network" value={p.ppn || '—'} />
                  <Row label="Eligibility start" value={formatDate(p.eligibilityStartDate)} />
                  <Row label="Eligibility end" value={formatDate(p.eligibilityEndDate)} />
                </DetailBlock>

                <DetailBlock title="Cost Share">
                  <Row label="Deductible remaining" value={money(p.remainingDeductibleIndividual)} />
                  <Row label="Out-of-pocket remaining" value={money(p.remainingOutOfPocketIndividual)} />
                  <Row label="Last eligibility check" value={p.lastEligibilityCheckAt ? formatDate(p.lastEligibilityCheckAt) : '—'} />
                </DetailBlock>

                <DetailBlock title="Clinical Snapshot">
                  <TagList label="Conditions" items={detail?.conditions} />
                  <TagList label="Medications" items={detail?.medications} />
                  <TagList label="Allergies" items={detail?.allergies} accent="text-red-500" />
                </DetailBlock>
              </div>
            )}

            <div className="mt-5 flex items-center justify-between border-t border-[var(--hc-border)] pt-4">
              <div className="text-xs text-[var(--hc-text-muted)]">
                {detail?.visits?.length
                  ? `${detail.visits.length} prior visit${detail.visits.length === 1 ? '' : 's'}, most recent ${formatDate(detail.visits[0].visitDate)}`
                  : 'No prior visits on record'}
              </div>
              <button
                onClick={onStart}
                disabled={starting}
                className="bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white text-sm font-medium px-5 py-2 rounded-lg disabled:opacity-50"
              >
                {starting ? 'Starting...' : 'Start Encounter'}
              </button>
            </div>
          </td>
        </tr>
      )}
    </>
  )
}

const STATUS_STYLE = {
  InProgress: 'bg-amber-500/10 text-amber-500 border-amber-500/30',
  PendingReview: 'bg-blue-500/10 text-blue-500 border-blue-500/30',
  Completed: 'bg-green-500/10 text-green-600 dark:text-green-400 border-green-500/30',
}
const STATUS_LABEL = {
  InProgress: 'In Progress',
  PendingReview: 'Pending Review',
  Completed: 'Reviewed',
}

function VisitStatusBadge({ status }) {
  if (!status) return null
  return (
    <span
      className={`text-[10px] uppercase tracking-wide font-semibold px-2 py-0.5 rounded-full border ${STATUS_STYLE[status] ?? 'bg-[var(--hc-surface-2)] text-[var(--hc-text-muted)] border-[var(--hc-border)]'}`}
    >
      {STATUS_LABEL[status] ?? status}
    </span>
  )
}

function DetailBlock({ title, children }) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wider text-[var(--hc-primary)] font-semibold mb-2">{title}</div>
      <div className="space-y-1.5">{children}</div>
    </div>
  )
}

function Row({ label, value }) {
  return (
    <div className="flex justify-between gap-3 text-sm">
      <span className="text-[var(--hc-text-muted)]">{label}</span>
      <span className="text-[var(--hc-text)] text-right">{value}</span>
    </div>
  )
}

function TagList({ label, items, accent = 'text-[var(--hc-text)]' }) {
  return (
    <div>
      <div className="text-xs text-[var(--hc-text-muted)] mb-1">{label}</div>
      {items && items.length > 0 ? (
        <ul className="space-y-0.5">
          {items.map((item, i) => (
            <li key={i} className={`text-sm ${accent}`}>
              {item}
            </li>
          ))}
        </ul>
      ) : (
        <div className="text-sm text-[var(--hc-text-muted)] italic">None on record</div>
      )}
    </div>
  )
}
