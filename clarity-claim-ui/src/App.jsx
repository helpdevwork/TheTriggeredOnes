import { useState } from 'react'
import { useClinicalRecord } from './hooks/useClinicalRecord'
import { useTheme } from './hooks/useTheme'
import { useDoctor } from './hooks/useDoctor'
import { ScreenErrorBoundary } from './components/ScreenErrorBoundary'
import { AppHeader } from './components/AppHeader'
import { WizardStepper } from './components/WizardStepper'
import { DoctorIntakeForm } from './components/DoctorIntakeForm'
import { PatientDashboard } from './screens/PatientDashboard'
import { EncounterCapture } from './screens/EncounterCapture'
import { SOAPNoteEditor } from './screens/SOAPNoteEditor'
import { ReviewerApp } from './ReviewerApp'
import { api } from './services/api'

// Doctor's wizard ends at the SOAP note -- validation runs on the reviewer's
// side (see ReviewerApp), not the ordering physician's.
const WIZARD_SCREENS = [EncounterCapture, SOAPNoteEditor]

export default function App() {
  // A tab opened from "Send for Review Team" carries ?reviewVisitId=<id> --
  // that's the reviewer's whole session, no doctor intake involved.
  const reviewVisitId = new URLSearchParams(window.location.search).get('reviewVisitId')
  if (reviewVisitId) {
    return <ReviewerApp visitId={Number(reviewVisitId)} />
  }

  return <DoctorApp />
}

function DoctorApp() {
  const { doctor, setDoctor, clearDoctor } = useDoctor()
  const { theme, toggleTheme } = useTheme()
  const { record, updateRecord, resetRecord } = useClinicalRecord()

  const [mode, setMode] = useState('dashboard') // 'dashboard' | 'wizard'
  const [screenIndex, setScreenIndex] = useState(0)
  const [search, setSearch] = useState('')

  if (!doctor) {
    return <DoctorIntakeForm onComplete={setDoctor} />
  }

  const handleStartEncounter = async (patientId, detail) => {
    const { visitId } = await api.createVisit(patientId, doctor.doctorId)
    const dash = detail?.dashboard
    updateRecord({
      visitId,
      patientId,
      patient: {
        fullName: dash?.fullName ?? '',
        dateOfBirth: dash?.dateOfBirth ?? '',
        gender: dash?.gender ?? '',
        conditions: detail?.conditions ?? [],
        medications: detail?.medications ?? [],
        allergies: detail?.allergies ?? [],
      },
      transcript: '',
      soapNote: null,
      soapNoteId: null,
      validation: null,
      fromFallback: false,
    })
    setScreenIndex(0)
    setMode('wizard')
  }

  const handleBackToDashboard = () => {
    resetRecord()
    setMode('dashboard')
    setScreenIndex(0)
  }

  const Screen = WIZARD_SCREENS[screenIndex]

  return (
    <div className="min-h-screen bg-[var(--hc-bg)]">
      <AppHeader
        doctor={doctor}
        onSwitchDoctor={clearDoctor}
        theme={theme}
        onToggleTheme={toggleTheme}
        search={search}
        onSearchChange={setSearch}
        showSearch={mode === 'dashboard'}
      />

      {mode === 'dashboard' ? (
        <ScreenErrorBoundary>
          <PatientDashboard search={search} doctor={doctor} onStartEncounter={handleStartEncounter} />
        </ScreenErrorBoundary>
      ) : (
        <>
          <div className="flex items-center justify-between px-6 md:px-10 pt-4">
            <button
              onClick={handleBackToDashboard}
              className="text-sm text-[var(--hc-text-muted)] hover:text-[var(--hc-text)] flex items-center gap-1"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
              </svg>
              Back to patient list
            </button>
          </div>

          <WizardStepper
            activeIndex={screenIndex}
            onNavigate={setScreenIndex}
            patientName={record.patient?.fullName}
          />

          <ScreenErrorBoundary>
            <Screen
              record={record}
              updateRecord={updateRecord}
              onNext={() => setScreenIndex((i) => Math.min(i + 1, WIZARD_SCREENS.length - 1))}
              onBack={() => setScreenIndex((i) => Math.max(i - 1, 0))}
              onFinish={handleBackToDashboard}
            />
          </ScreenErrorBoundary>
        </>
      )}
    </div>
  )
}
