// MediaRecorder captures audio/webm (Opus-encoded); Whisper.net requires a real
// 16-bit PCM WAV file at 16kHz mono. This decodes whatever the browser recorded
// and re-encodes it to a compliant WAV Blob before it's uploaded.
export async function blobToWhisperWav(blob) {
  const arrayBuffer = await blob.arrayBuffer()
  const AudioCtx = window.AudioContext || window.webkitAudioContext
  const decodeCtx = new AudioCtx()
  const decoded = await decodeCtx.decodeAudioData(arrayBuffer)
  await decodeCtx.close()

  const TARGET_SAMPLE_RATE = 16000
  const offlineCtx = new OfflineAudioContext(1, Math.ceil(decoded.duration * TARGET_SAMPLE_RATE), TARGET_SAMPLE_RATE)
  const source = offlineCtx.createBufferSource()
  source.buffer = decoded
  source.connect(offlineCtx.destination)
  source.start()

  const resampled = await offlineCtx.startRendering()
  const pcm = resampled.getChannelData(0)

  return encodeWav(pcm, TARGET_SAMPLE_RATE)
}

function encodeWav(samples, sampleRate) {
  const bytesPerSample = 2 // 16-bit PCM
  const blockAlign = bytesPerSample
  const dataSize = samples.length * bytesPerSample
  const buffer = new ArrayBuffer(44 + dataSize)
  const view = new DataView(buffer)

  writeString(view, 0, 'RIFF')
  view.setUint32(4, 36 + dataSize, true)
  writeString(view, 8, 'WAVE')
  writeString(view, 12, 'fmt ')
  view.setUint32(16, 16, true) // fmt chunk size
  view.setUint16(20, 1, true) // PCM
  view.setUint16(22, 1, true) // mono
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * blockAlign, true) // byte rate
  view.setUint16(32, blockAlign, true)
  view.setUint16(34, 16, true) // bits per sample
  writeString(view, 36, 'data')
  view.setUint32(40, dataSize, true)

  let offset = 44
  for (let i = 0; i < samples.length; i++, offset += 2) {
    const s = Math.max(-1, Math.min(1, samples[i]))
    view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7fff, true)
  }

  return new Blob([buffer], { type: 'audio/wav' })
}

function writeString(view, offset, str) {
  for (let i = 0; i < str.length; i++) view.setUint8(offset + i, str.charCodeAt(i))
}
