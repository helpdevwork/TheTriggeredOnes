import { useRef, useState } from 'react'
import { api } from '../services/api'
import { blobToWhisperWav } from '../utils/wavEncoder'

// Records a browser mic clip via MediaRecorder, re-encodes it to a 16kHz mono
// PCM WAV (Whisper.net requires that exact format -- MediaRecorder's native
// audio/webm output isn't readable by it), sends it to POST /api/transcribe,
// and reports the resulting text back to the parent screen. A manual textarea
// is also provided so the encounter can be captured without a working microphone.
export function AudioRecorder({ onTranscriptUpdate, visitId }) {
  const [recording, setRecording] = useState(false)
  const [transcribing, setTranscribing] = useState(false)
  const [error, setError] = useState('')
  const mediaRecorderRef = useRef(null)
  const chunksRef = useRef([])

  const handleStartRecording = async () => {
    setError('')
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      const recorder = new MediaRecorder(stream)
      chunksRef.current = []

      recorder.ondataavailable = (e) => chunksRef.current.push(e.data)
      recorder.onstop = async () => {
        stream.getTracks().forEach((t) => t.stop())
        const blob = new Blob(chunksRef.current, { type: recorder.mimeType || 'audio/webm' })
        await handleTranscribe(blob)
      }

      recorder.start()
      mediaRecorderRef.current = recorder
      setRecording(true)
    } catch (err) {
      setError('Microphone unavailable — use the text box below instead.')
    }
  }

  const handleStopRecording = () => {
    mediaRecorderRef.current?.stop()
    setRecording(false)
  }

  const handleTranscribe = async (recordedBlob) => {
    setTranscribing(true)
    try {
      const wavBlob = await blobToWhisperWav(recordedBlob)
      const result = await api.transcribe(wavBlob, visitId)
      onTranscriptUpdate(result.fullText, true) // already persisted server-side by /api/transcribe
    } catch (err) {
      setError('Transcription failed — check that the Whisper model is downloaded and try again, or type the transcript manually.')
    } finally {
      setTranscribing(false)
    }
  }

  return (
    <div className="hc-card p-5">
      <div className="flex items-center gap-3">
        {!recording ? (
          <button
            onClick={handleStartRecording}
            disabled={transcribing}
            className="bg-red-600 hover:bg-red-500 text-white px-5 py-2 rounded-lg text-sm font-medium disabled:opacity-40"
          >
            ● Record Encounter
          </button>
        ) : (
          <button
            onClick={handleStopRecording}
            className="bg-[var(--hc-surface-2)] hover:opacity-80 text-[var(--hc-text)] border border-[var(--hc-border)] px-5 py-2 rounded-lg text-sm font-medium animate-pulse"
          >
            ■ Stop Recording
          </button>
        )}
        {transcribing && <span className="text-sm text-[var(--hc-text-muted)]">Transcribing...</span>}
      </div>

      {error && <p className="text-amber-500 text-xs mt-3">{error}</p>}

      <div className="mt-4">
        <label className="block text-xs uppercase tracking-wider text-[var(--hc-text-muted)] mb-1">
          Or type / paste the encounter transcript
        </label>
        <textarea
          className="hc-input min-h-24"
          placeholder="Patient reports 6 weeks of low back pain radiating down the left leg..."
          onChange={(e) => onTranscriptUpdate(e.target.value, false)}
        />
      </div>
    </div>
  )
}
