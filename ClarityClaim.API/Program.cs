using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Infrastructure.Cms;
using ClarityClaim.Infrastructure.Fhir;
using ClarityClaim.Infrastructure.Llm;
using ClarityClaim.Infrastructure.Ocr;
using ClarityClaim.Infrastructure.Pdf;
using ClarityClaim.Infrastructure.PolicyText;
using ClarityClaim.Infrastructure.Search;
using ClarityClaim.Infrastructure.SqlServer;
using ClarityClaim.Infrastructure.Whisper;
using ClarityClaim.Services;

var builder = WebApplication.CreateBuilder(args);

// HTTP Clients -- named, managed via factory (never new HttpClient()) (BP-09)
builder.Services.AddHttpClient("ollama", c =>
{
    // Ollama server's OpenAI-compatible API -- confirmed models via GET {base}/v1/models
    var baseUrl = builder.Configuration["Llm:BaseUrl"] ?? "http://172.50.50.83:11434";
    c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(120); // LLM inference can take 30-40s
});
builder.Services.AddHttpClient("fhir", c =>
{
    c.BaseAddress = new Uri("https://hapi.fhir.org");
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient("cms", c =>
{
    // Public Medicare Coverage Database -- fetched at most once per policy
    // per cache lifetime (see PolicyTextResolver), never on the request path.
    c.BaseAddress = new Uri("https://www.cms.gov/");
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("ClarityClaim/1.0 (internal policy-text sync)");
});

// Infrastructure -- singletons (expensive to create, safe to reuse)
builder.Services.AddSingleton<ILanguageModelClient, OllamaClient>();
builder.Services.AddSingleton<IPolicySearchService, PolicyLexicalSearchService>();
builder.Services.AddSingleton<CmsPolicyTextService>();
builder.Services.AddSingleton<PolicyPdfOcrService>();
builder.Services.AddSingleton<PolicyTextResolver>();
builder.Services.AddSingleton<IPdfIndexingService, PdfIndexingService>();
builder.Services.AddSingleton<ITranscriptionService, WhisperTranscriptionService>();
builder.Services.AddSingleton<IFhirService, FhirService>();

// SQL Server data access -- ClarityClaim-Database. Connection string is Windows-
// Authentication trusted connection (no password to leak); see ClarityClaim-Database/README.md.
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IVisitRepository, VisitRepository>();
builder.Services.AddScoped<IPipelinePersistenceRepository, PipelinePersistenceRepository>();
builder.Services.AddScoped<IReviewTeamRepository, ReviewTeamRepository>();

// Services -- scoped per request
builder.Services.AddScoped<SoapGenerationService>();
builder.Services.AddScoped<ValidationService>();
builder.Services.AddScoped<PatientSummaryService>();
builder.Services.AddScoped<RegulatoryService>();

// CORS -- locked to React dev server only (BP-08). Never AllowAnyOrigin().
builder.Services.AddCors(o => o.AddPolicy("ReactUI", p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Index all policy PDFs on startup -- runs once before first request.
// Failure here must never prevent the API from starting (demo-safety / BP-04).
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var indexer = scope.ServiceProvider.GetRequiredService<IPdfIndexingService>();
        var pdfFolder = app.Configuration["Policies:FolderPath"] ?? "Data/Policies";
        await indexer.IndexAllPoliciesAsync(pdfFolder);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Startup PDF indexing failed -- RAG retrieval will return no chunks until resolved");
    }
}

app.UseCors("ReactUI");
app.MapControllers();
app.Run("http://localhost:8000");
