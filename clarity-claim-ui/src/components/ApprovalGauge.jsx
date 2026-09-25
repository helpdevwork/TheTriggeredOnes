import { useEffect, useState } from 'react'

// Animated SVG gauge -- the demo's critical "wow" moment (74% -> 93%).
export function ApprovalGauge({ score }) {
  const [displayed, setDisplayed] = useState(0)

  useEffect(() => {
    let start = displayed
    const duration = 1200 // ms -- smooth animation
    const startTime = performance.now()
    let raf

    const tick = (now) => {
      const progress = Math.min((now - startTime) / duration, 1)
      setDisplayed(Math.round(start + (score - start) * progress))
      if (progress < 1) raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [score])

  const colour = displayed >= 80 ? '#22c55e' : displayed >= 60 ? '#f59e0b' : '#ef4444'
  const label = displayed >= 80 ? 'Likely to be approved' : displayed >= 60 ? 'Needs attention' : 'High denial risk'

  return (
    <div className="flex flex-col items-center">
      <svg width="240" height="144" viewBox="0 0 200 120">
        <path
          d="M 20 100 A 80 80 0 0 1 180 100"
          fill="none"
          stroke="var(--hc-border)"
          strokeWidth="14"
        />
        <path
          d="M 20 100 A 80 80 0 0 1 180 100"
          fill="none"
          stroke={colour}
          strokeWidth="14"
          strokeLinecap="round"
          strokeDasharray={`${(displayed / 100) * 251} 251`}
          style={{ transition: 'stroke 0.3s ease' }}
        />
      </svg>
      <div className="text-5xl font-bold -mt-2" style={{ color: colour }}>
        {displayed}%
      </div>
      <div className="text-sm text-[var(--hc-text-muted)] mt-1">Approval Probability</div>
      <div className="text-xs font-semibold mt-2 px-3 py-1 rounded-full" style={{ color: colour, backgroundColor: `${colour}1a` }}>
        {label}
      </div>
    </div>
  )
}
