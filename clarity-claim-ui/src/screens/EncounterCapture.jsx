import { useState } from 'react'
import { PatientSidebar } from '../components/PatientSidebar'
import { AudioRecorder } from '../components/AudioRecorder'
import { TranscriptStream } from '../components/TranscriptStream'
import { api } from '../services/api'

export function EncounterCapture({ record, updateRecord, onNext }) {
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  // Recorded audio is already persisted server-side (POST /api/transcribe?visitId=..);
  // only a manually typed/pasted transcript still needs an explicit save, and only once.
  const [transcriptPersisted, setTranscriptPersisted] = useState(false)

  const handleTranscriptChange = (text, alreadyPersisted = false) => {
    updateRecord({ transcript: text })
    setTranscriptPersisted(alreadyPersisted)
  }

  const handleGenerateClick = async () => {
    setLoading(true)
    setError('')
    try {
      if (record.visitId && !transcriptPersisted) {
        await api.saveManualTranscript(record.visitId, record.transcript).catch(() => {})
        setTranscriptPersisted(true)
      }
      const result = await api.generateSoap({
        transcript: record.transcript,
        patientContext: record.patient,
        visitId: record.visitId,
      })
      updateRecord({ soapNote: result.soapNote, soapNoteId: result.soapNoteId, fromFallback: result.fromFallback })
      onNext()
    } catch (err) {
      setError('Could not generate the clinical note. Check that the API is running.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex" style={{ minHeight: 'calc(100vh - 130px)' }}>
      <PatientSidebar patient={record.patient} />
      <div className="flex-1 flex flex-col p-8 overflow-y-auto">
        <h1 className="text-2xl font-semibold text-[var(--hc-text)] mb-1">Encounter Capture</h1>
        <p className="text-sm text-[var(--hc-text-muted)] mb-6">
          Record the visit, or paste a transcript, then generate a structured clinical note.
        </p>

        <AudioRecorder
          visitId={record.visitId}
          onTranscriptUpdate={handleTranscriptChange}
        />
        <TranscriptStream text={record.transcript} />

        {error && <p className="text-red-500 text-sm mt-3">{error}</p>}

        <button
          onClick={handleGenerateClick}
          disabled={!record.transcript || loading}
          className="mt-6 self-start bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white px-6 py-2.5 rounded-lg text-sm font-medium disabled:opacity-40 disabled:cursor-not-allowed"
        >
          {loading ? 'Generating...' : 'Generate Clinical Note'}
        </button>
      </div>
    </div>
  )
}
