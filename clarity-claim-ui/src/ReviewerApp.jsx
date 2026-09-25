import { useEffect, useState } from 'react'
import { AppHeader } from './components/AppHeader'
import { WizardStepper, REVIEWER_STEPS } from './components/WizardStepper'
import { SOAPNoteEditor } from './screens/SOAPNoteEditor'
import { ValidationScore } from './screens/ValidationScore'
import { useTheme } from './hooks/useTheme'
import { api } from './services/api'

// The tab that opens when a doctor sends a SOAP note "for Review Team". No
// doctor intake here -- the reviewer's identity comes from the assignment
// record itself (whoever the doctor picked), not a separate login.
export function ReviewerApp({ visitId }) {
  const { theme, toggleTheme } = useTheme()
  const [context, setContext] = useState(null)
  const [error, setError] = useState('')
  const [screenIndex, setScreenIndex] = useState(0)
  const [record, setRecord] = useState(null)

  useEffect(() => {
    api
      .getReviewContext(visitId)
      .then((c) => {
        setContext(c)
        setRecord({
          visitId: c.visitId,
          soapNoteId: c.soapNoteId,
          soapNote: c.soapNote,
          patient: { fullName: c.patientFullName, dateOfBirth: c.patientDateOfBirth, gender: c.patientGender },
          validation: null,
          fromFallback: false,
        })
      })
      .catch(() => setError('Could not load this review. The link may be invalid or the API may be offline.'))
  }, [visitId])

  const updateRecord = (patch) => setRecord((prev) => ({ ...prev, ...patch }))

  if (error) {
    return <div className="min-h-screen flex items-center justify-center text-red-500 p-8">{error}</div>
  }
  if (!context || !record) {
    return <div className="min-h-screen flex items-center justify-center text-[var(--hc-text-muted)]">Loading review...</div>
  }

  const reviewerProfile = { fullName: context.reviewerFullName || 'Reviewer', specialty: 'Review Team' }

  return (
    <div className="min-h-screen bg-[var(--hc-bg)]">
      <AppHeader doctor={reviewerProfile} theme={theme} onToggleTheme={toggleTheme} showSearch={false} />

      <WizardStepper steps={REVIEWER_STEPS} activeIndex={screenIndex} onNavigate={setScreenIndex} patientName={context.patientFullName} />

      {screenIndex === 0 ? (
        <SOAPNoteEditor record={record} updateRecord={updateRecord} onNext={() => setScreenIndex(1)} mode="reviewer" />
      ) : (
        <ValidationScore record={record} updateRecord={updateRecord} onBack={() => setScreenIndex(0)} />
      )}
    </div>
  )
}
