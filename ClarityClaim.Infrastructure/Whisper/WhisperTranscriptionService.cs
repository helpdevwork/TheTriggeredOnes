using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Whisper.net;

namespace ClarityClaim.Infrastructure.Whisper;

// Wraps Whisper.net for local on-device transcription. Uses the medium GGML model at
// /Data/Models/ggml-medium.bin. Model loaded once at startup (lazy on first call).
public class WhisperTranscriptionService : ITranscriptionService, IAsyncDisposable
{
    private WhisperFactory? _factory;
    private readonly string _modelPath;
    private readonly ILogger<WhisperTranscriptionService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public WhisperTranscriptionService(IConfiguration config, ILogger<WhisperTranscriptionService> logger)
    {
        _modelPath = config["Whisper:ModelPath"] ?? "Data/Models/ggml-medium.bin";
        _logger = logger;
    }

    // Called once, lazily, on first transcription request (or can be warmed up via IHostedService)
    public async Task InitialiseAsync()
    {
        if (_factory is not null) return;

        await _initLock.WaitAsync();
        try
        {
            if (_factory is not null) return;

            if (!File.Exists(_modelPath))
                throw new FileNotFoundException(
                    $"Whisper model not found at '{_modelPath}'. Download ggml-medium.bin and place it there.",
                    _modelPath);

            _factory = WhisperFactory.FromPath(_modelPath);
        }
        finally { _initLock.Release(); }
    }

    public async Task<TranscriptResponse> TranscribeAsync(
        string audioFilePath, CancellationToken ct = default)
    {
        if (_factory is null) await InitialiseAsync();

        using var processor = _factory!.CreateBuilder()
            .WithLanguage("en")
            .WithThreads(4)
            .Build();

        var segments = new List<TranscriptSegment>();

        await using var stream = File.OpenRead(audioFilePath);
        await foreach (var seg in processor.ProcessAsync(stream, ct))
        {
            segments.Add(new TranscriptSegment(
                seg.Start.TotalSeconds,
                seg.End.TotalSeconds,
                seg.Text.Trim()
            ));
        }

        var fullText = string.Join(" ", segments.Select(s => s.Text));
        return new TranscriptResponse(fullText, segments);
    }

    public ValueTask DisposeAsync()
    {
        _factory?.Dispose();
        return ValueTask.CompletedTask;
    }
}
