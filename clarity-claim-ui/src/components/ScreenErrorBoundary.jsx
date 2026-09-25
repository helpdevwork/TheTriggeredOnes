import { Component } from 'react'

// Wraps each screen. If a screen crashes, shows the pre-rendered fallback image
// instead of white-screening during a judge presentation.
export class ScreenErrorBoundary extends Component {
  constructor(props) {
    super(props)
    this.state = { hasError: false }
  }

  static getDerivedStateFromError() {
    return { hasError: true }
  }

  componentDidCatch(error, info) {
    console.error('Screen crashed:', error, info)
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex flex-col items-center justify-center h-screen bg-[var(--hc-bg)] text-[var(--hc-text-muted)] gap-4">
          <img
            src={this.props.fallbackImage ?? '/fallbacks/screen-error.svg'}
            alt="Screen unavailable"
            className="max-w-md opacity-80"
            onError={(e) => (e.target.style.display = 'none')}
          />
          <p className="text-sm">
            This screen hit an unexpected error. Your data is safe — reload to continue.
          </p>
          <button
            onClick={() => window.location.reload()}
            className="bg-[var(--hc-primary)] hover:bg-[var(--hc-primary-dark)] text-white text-sm px-4 py-2 rounded-lg"
          >
            Reload
          </button>
        </div>
      )
    }
    return this.props.children
  }
}
