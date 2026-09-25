import { useState } from 'react'
import { ApprovalGauge } from '../components/ApprovalGauge'
import { GapChecklist } from '../components/GapChecklist'
import { PatientSummary } from '../components/PatientSummary'
import { RegulatoryPanel } from '../components/RegulatoryPanel'
import { AuditTrail } from '../components/AuditTrail'
import { api } from '../services/api'

// Screen 3 -- the demo's critical path. Re-running validation after applying a fix
// drives the ApprovalGauge from its prior score up toward 93%.
export function ValidationScore({ record, updateRecord, onBack }) {
  const [revalidating, setRevalidating] = useState(false)
  const [refreshToken, setRefreshToken] = useState(0)
  const [finishing, setFinishing] = useState(false)
  const [finished, setFinished] = useState(false)
  const [finishError, setFinishError] = useState('')
  const { validation, soapNote, fromFallback, visitId, soapNoteId } = record

  const handleFinishReview = async () => {
    setFinishing(true)
    setFinishError('')
    try {
      await api.completeVisit(visitId)
      setFinished(true)
      setRefreshToken((t) => t + 1) // pick up the ReviewCompleted audit row
    } catch {
      setFinishError('Could not mark this review as complete. Check that the API is running.')
    } finally {
      setFinishing(false)
    }
  }

  const handleApplyFix = async (fix) => {
    const updatedNote = {
      ...soapNote,
      plan: `${soapNote.plan} ${fix.suggestedLanguage}`.trim(),
    }
    updateRecord({ soapNote: updatedNote })

    setRevalidating(true)
    try {
      const result = await api.validate({
        soapNote: updatedNote,
        icd10Codes: (updatedNote.icd10Codes ?? []).map((c) => c.code),
        visitId,
        soapNoteId,
      })
      updateRecord({ validation: result.result, fromFallback: result.fromFallback })
      setRefreshToken((t) => t + 1)
    } catch {
      // keep the current score displayed if re-validation fails
    } finally {
      setRevalidating(false)
    }
  }

  if (!validation) {
    return (
      <div className="p-8 text-[var(--hc-text-muted)]">
        No validation result yet.{' '}
        <button onClick={onBack} className="text-[var(--hc-primary)] underline">
          Go back
        </button>
      </div>
    )
  }

  return (
    <div className="p-8 max-w-6xl mx-auto">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-3xl font-semibold text-[var(--hc-text)] mb-1">Validation Scorecard</h1>
          <p className="text-base text-[var(--hc-text-muted)]">
            Medical necessity check against CMS NCD/LCD coverage criteria.
          </p>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {finished && (
            <span className="text-xs bg-green-500/10 text-green-600 dark:text-green-400 border border-green-500/30 rounded-lg px-3 py-1.5 font-medium flex items-center gap-1.5">
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
              </svg>
              Reviewed
            </span>
          )}
          {fromFallback && (
            <span className="text-xs bg-amber-500/10 text-amber-500 border border-amber-500/30 rounded-lg px-3 py-1.5 font-medium">
              Pre-generated result (live inference unavailable)
            </span>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-1 flex flex-col items-center hc-card p-8">
          <ApprovalGauge score={validation.approvalProbability} />
          {revalidating && <p className="text-xs text-[var(--hc-text-muted)] mt-4">Re-validating...</p>}
          {validation.humanReviewFlags?.length > 0 && (
            <div className="mt-5 w-full bg-red-500/10 border border-red-500/30 rounded-lg p-3 text-sm text-red-500 text-center font-medium">
              {validation.humanReviewFlags.map((f, i) => (
                <p key={i}>{f}</p>
              ))}
            </div>
          )}
          {validation.policyReferences?.length > 0 && (
            <div className="mt-5 w-full border-t border-[var(--hc-border)] pt-4 text-xs text-[var(--hc-text-muted)] text-center space-y-0.5">
              <div className="uppercase tracking-wider font-semibold mb-1">Cited Policy</div>
              {validation.policyReferences.map((ref, i) => (
                <p key={i} className="font-mono">{ref}</p>
              ))}
            </div>
          )}
        </div>

        <div className="lg:col-span-2 space-y-4">
          <h2 className="text-lg font-semibold text-[var(--hc-text)]">
            {validation.gaps?.length > 0 && validation.approvalProbability < 60
              ? 'Claim Rejection Reasons'
              : 'Documentation Gaps'}
          </h2>
          <GapChecklist gaps={validation.gaps} fixes={validation.fixes} onApplyFix={handleApplyFix} />

          {validation.passingCriteria?.length > 0 && (
            <div className="hc-card p-5">
              <div className="text-sm uppercase tracking-wider text-green-600 dark:text-green-400 font-semibold mb-3">
                ✓ Passing Criteria
              </div>
              <ul className="text-sm text-[var(--hc-text)] space-y-1.5 list-disc list-inside">
                {validation.passingCriteria.map((c, i) => (
                  <li key={i}>{c}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mt-6">
        <PatientSummary soapNote={soapNote} />
        <RegulatoryPanel />
        <AuditTrail visitId={visitId} refreshToken={refreshToken} />
      </div>

      {finishError && <p className="text-red-500 text-sm mt-4">{finishError}</p>}

      <div className="mt-6 flex items-center gap-3">
        <button
          onClick={onBack}
          className="bg-[var(--hc-surface-2)] hover:opacity-80 text-[var(--hc-text)] border border-[var(--hc-border)] px-6 py-2.5 rounded-lg text-sm font-medium"
        >
          Back to Note
        </button>

        {finished ? (
          <span className="text-sm text-[var(--hc-text-muted)]">
            This review is complete — the doctor will see it marked Completed on their dashboard. You can close this tab.
          </span>
        ) : (
          <button
            onClick={handleFinishReview}
            disabled={finishing}
            className="bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white px-6 py-2.5 rounded-lg text-sm font-medium disabled:opacity-40"
          >
            {finishing ? 'Finishing...' : 'Finish Review'}
          </button>
        )}
      </div>
    </div>
  )
}
