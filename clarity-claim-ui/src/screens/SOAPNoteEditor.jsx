import { useState } from 'react'
import { SOAPPanel } from '../components/SOAPPanel'
import { ICD10Chips } from '../components/ICD10Chips'
import { ReviewerSelectModal } from '../components/ReviewerSelectModal'
import { api } from '../services/api'

// mode='doctor' (default): doctor edits the note, then sends it to a chosen
// reviewer instead of validating it themselves -- medical-necessity sign-off
// is a reviewer/coder function, not something the ordering physician runs.
// mode='reviewer': read-only view of the exact note the doctor sent, with a
// "Run Validation" button that runs the last step and shows the scorecard.
export function SOAPNoteEditor({ record, updateRecord, onNext, onBack, onFinish, mode = 'doctor' }) {
  const [note, setNote] = useState(record.soapNote)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [showReviewerModal, setShowReviewerModal] = useState(false)
  const [assigning, setAssigning] = useState(false)
  const [sentTo, setSentTo] = useState(null)

  const readOnly = mode === 'reviewer'

  const handleFieldChange = (field, value) =>
    setNote((prev) => ({ ...prev, [field]: value }))

  const handleSendForReview = () => {
    setError('')
    setShowReviewerModal(true)
  }

  const handleAssignReviewer = async (reviewTeamMemberId, reviewerName) => {
    setAssigning(true)
    setError('')
    try {
      if (record.soapNoteId) {
        await api.updateSoapNote(record.soapNoteId, note) // persist the doctor's final edits
      }
      updateRecord({ soapNote: note })
      await api.assignReviewer(record.visitId, reviewTeamMemberId)

      const reviewUrl = `${window.location.origin}${window.location.pathname}?reviewVisitId=${record.visitId}`
      window.open(reviewUrl, '_blank')

      setShowReviewerModal(false)
      setSentTo(reviewerName)
    } catch (err) {
      setError('Could not send the note for review. Check that the API is running.')
    } finally {
      setAssigning(false)
    }
  }

  const handleRunValidation = async () => {
    setLoading(true)
    setError('')
    try {
      const result = await api.validate({
        soapNote: note,
        icd10Codes: (note.icd10Codes ?? []).map((c) => c.code),
        visitId: record.visitId,
        soapNoteId: record.soapNoteId,
      })
      updateRecord({ validation: result.result, fromFallback: result.fromFallback })
      onNext()
    } catch (err) {
      setError('Could not validate the note. Check that the API is running.')
    } finally {
      setLoading(false)
    }
  }

  if (!note) {
    return (
      <div className="p-8 text-[var(--hc-text-muted)]">
        No clinical note yet.{' '}
        {onBack && (
          <button onClick={onBack} className="text-[var(--hc-primary)] underline">
            Go back
          </button>
        )}
      </div>
    )
  }

  if (sentTo) {
    return (
      <div className="p-8 max-w-2xl mx-auto text-center">
        <div className="hc-card p-10">
          <div className="w-14 h-14 rounded-full bg-green-500/10 text-green-500 flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-xl font-semibold text-[var(--hc-text)] mb-2">Sent for review</h2>
          <p className="text-sm text-[var(--hc-text-muted)] mb-6">
            This note was sent to <span className="text-[var(--hc-text)] font-medium">{sentTo}</span> for
            medical-necessity validation. A new tab has opened with their view.
          </p>
          <button
            onClick={onFinish ?? onBack}
            className="bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white px-6 py-2.5 rounded-lg text-sm font-medium"
          >
            Back to Patient List
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="p-8 max-w-5xl mx-auto">
      <h1 className="text-2xl font-semibold text-[var(--hc-text)] mb-1">SOAP Note</h1>
      <p className="text-sm text-[var(--hc-text-muted)] mb-6">
        {mode === 'reviewer'
          ? 'The clinical note as sent by the ordering physician.'
          : 'Review and edit the generated note, then send it to the review team.'}
      </p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {['subjective', 'objective', 'assessment', 'plan'].map((field) => (
          <SOAPPanel
            key={field}
            label={field}
            value={note?.[field] ?? ''}
            onChange={(v) => handleFieldChange(field, v)}
            readOnly={readOnly}
          />
        ))}
      </div>

      <div className="mt-6">
        <ICD10Chips codes={note?.icd10Codes ?? []} />
      </div>

      {error && <p className="text-red-500 text-sm mt-4">{error}</p>}

      <div className="mt-6 flex gap-3">
        {onBack && (
          <button
            onClick={onBack}
            className="bg-[var(--hc-surface-2)] hover:opacity-80 text-[var(--hc-text)] border border-[var(--hc-border)] px-6 py-2.5 rounded-lg text-sm font-medium"
          >
            Back
          </button>
        )}
        {mode === 'reviewer' ? (
          <button
            onClick={handleRunValidation}
            disabled={loading}
            className="bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white px-6 py-2.5 rounded-lg text-sm font-medium disabled:opacity-40"
          >
            {loading ? 'Validating...' : 'Run Validation'}
          </button>
        ) : (
          <button
            onClick={handleSendForReview}
            className="bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white px-6 py-2.5 rounded-lg text-sm font-medium"
          >
            Send for Review Team
          </button>
        )}
      </div>

      {showReviewerModal && (
        <ReviewerSelectModal
          assigning={assigning}
          onClose={() => setShowReviewerModal(false)}
          onAssign={(id, name) => handleAssignReviewer(id, name)}
        />
      )}
    </div>
  )
}
