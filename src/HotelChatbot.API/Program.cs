using HotelChatbot.Core.Interfaces;
using HotelChatbot.Infrastructure.AI;
using HotelChatbot.Infrastructure.GoogleSheets;
using HotelChatbot.Infrastructure.RAG;
using HotelChatbot.Infrastructure.RAG.Services;
using HotelChatbot.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// SERILOG LOGGING
// ============================================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/hotel-chatbot-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ============================================================
// SERVICES
// ============================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "🏨 Hotel AI Chatbot API",
        Version = "v1",
        Description = "API cho hệ thống chatbot AI lễ tân khách sạn thông minh. " +
                      "Tích hợp Claude AI + Google Sheets realtime."
    });
});

// Memory Cache for sessions and sheet data
builder.Services.AddMemoryCache();

// Google Sheets
builder.Services.Configure<GoogleSheetsOptions>(
    builder.Configuration.GetSection("GoogleSheets"));
builder.Services.AddSingleton<IGoogleSheetsService, GoogleSheetsService>();

// Claude AI
builder.Services.Configure<AIProviderOptions>(
    builder.Configuration.GetSection("AIProvider"));
builder.Services.Configure<ClaudeAIOptions>(
    builder.Configuration.GetSection("ClaudeAI"));
builder.Services.AddHttpClient<ClaudeAIService>();
builder.Services.AddScoped<IClaudeAIService>(sp => sp.GetRequiredService<ClaudeAIService>());

// OpenAI
builder.Services.Configure<OpenAIOptions>(
    builder.Configuration.GetSection("OpenAI"));
builder.Services.AddHttpClient<OpenAIService>();

builder.Services.AddScoped<IHotelAIService>(sp =>
{
    var provider = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value.Provider;
    return provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<OpenAIService>()
        : sp.GetRequiredService<ClaudeAIService>();
});

// Domain Services
builder.Services.AddScoped<IHotelDataService, HotelDataService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.Configure<MessengerOptions>(
    builder.Configuration.GetSection("Messenger"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IMessengerWebhookService, MessengerWebhookService>();
builder.Services.Configure<AIUsageOptions>(
    builder.Configuration.GetSection("AIUsage"));
builder.Services.AddScoped<IAIUsageService, AIUsageService>();

// RAG / Qdrant
builder.Services.Configure<RagOptions>(
    builder.Configuration.GetSection("RAG"));
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<OpenAIEmbeddingOptions>(
    builder.Configuration.GetSection("OpenAIEmbedding"));

builder.Services.AddScoped<HotelKnowledgeDocumentFactory>();
builder.Services.AddHttpClient<OpenAIEmbeddingService>();
builder.Services.AddHttpClient<QdrantVectorStoreService>();
builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<OpenAIEmbeddingService>());
builder.Services.AddScoped<IVectorStoreService>(sp => sp.GetRequiredService<QdrantVectorStoreService>());
builder.Services.AddScoped<IKnowledgeIndexingService, KnowledgeIndexingService>();
builder.Services.AddScoped<RagContextService>();
builder.Services.AddScoped<NoOpRagContextService>();
builder.Services.AddScoped<IRagContextService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
    return options.Enabled
        ? sp.GetRequiredService<RagContextService>()
        : sp.GetRequiredService<NoOpRagContextService>();
});

// CORS - Allow all origins for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ============================================================
// MIDDLEWARE PIPELINE
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Chatbot API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Hotel AI Chatbot API";
    });
}

app.UseSerilogRequestLogging();
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0",
    service = "Hotel AI Chatbot API"
});

// Welcome endpoint
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
