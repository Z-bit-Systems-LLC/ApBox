using ApBox.Plugins;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApBox.SamplePlugins;

/// <summary>
/// Sample audit logging plugin that records all card access attempts to a file
/// for security auditing and compliance purposes
/// </summary>
public class AuditLoggingPlugin : IApBoxPlugin
{
    private readonly string _logDirectory;
    private readonly object _logLock = new();
    private readonly ILogger<AuditLoggingPlugin>? _logger;

    public AuditLoggingPlugin()
    {
        // Default to logs directory in application folder
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "audit");
        Directory.CreateDirectory(_logDirectory);
    }

    public AuditLoggingPlugin(ILogger<AuditLoggingPlugin> logger) : this()
    {
        _logger = logger;
    }

    public AuditLoggingPlugin(string logDirectory, ILogger<AuditLoggingPlugin>? logger = null)
    {
        _logDirectory = logDirectory;
        _logger = logger;
        Directory.CreateDirectory(_logDirectory);
    }

    public Guid Id => new Guid("C3D4E5F6-789A-BCDE-F012-123456789003");
    public string Name => "Audit Logging Plugin";
    public string Version => "1.0.0";
    public string Description => "Records all card access attempts to audit log files for security and compliance";

    public async Task<bool> ProcessCardReadAsync(CardReadEvent cardRead)
    {
        try
        {
            // Create audit entry
            var auditEntry = new AuditLogEntry
            {
                Timestamp = cardRead.Timestamp,
                EventId = Guid.NewGuid(),
                ReaderId = cardRead.ReaderId,
                ReaderName = cardRead.ReaderName,
                CardNumber = cardRead.CardNumber,
                BitLength = cardRead.BitLength,
                AdditionalData = cardRead.AdditionalData ?? new Dictionary<string, object>()
            };

            // Write to audit log
            await WriteAuditEntryAsync(auditEntry);

            _logger?.LogInformation("Audit entry created for card {CardNumber} from reader {ReaderName}", 
                cardRead.CardNumber, cardRead.ReaderName);

            // Audit plugin doesn't make access decisions - it just logs
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write audit entry for card {CardNumber}", cardRead.CardNumber);
            
            return false;
        }
    }

    public Task InitializeAsync()
    {
        _logger?.LogInformation("Audit Logging Plugin initialized. Log directory: {LogDirectory}", _logDirectory);
        
        // Create today's audit log file if it doesn't exist
        var todayLogFile = GetTodayLogFileName();
        if (!File.Exists(todayLogFile))
        {
            File.WriteAllText(todayLogFile, ""); // Create empty file
            _logger?.LogInformation("Created new audit log file: {LogFile}", todayLogFile);
        }
        
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _logger?.LogInformation("Audit Logging Plugin shutting down");
        return Task.CompletedTask;
    }

    private async Task WriteAuditEntryAsync(AuditLogEntry entry)
    {
        var logFileName = GetTodayLogFileName();
        var jsonEntry = JsonSerializer.Serialize(entry, new JsonSerializerOptions 
        { 
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        lock (_logLock)
        {
            File.AppendAllLines(logFileName, new[] { jsonEntry });
        }

        await Task.CompletedTask;
    }

    private string GetTodayLogFileName()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        return Path.Combine(_logDirectory, $"audit-{today}.jsonl");
    }

    /// <summary>
    /// Retrieve audit entries for a specific date range
    /// </summary>
    public async Task<List<AuditLogEntry>> GetAuditEntriesAsync(DateTime startDate, DateTime endDate)
    {
        var entries = new List<AuditLogEntry>();
        
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var logFileName = Path.Combine(_logDirectory, $"audit-{date:yyyy-MM-dd}.jsonl");
            
            if (File.Exists(logFileName))
            {
                var lines = await File.ReadAllLinesAsync(logFileName);
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    try
                    {
                        var entry = JsonSerializer.Deserialize<AuditLogEntry>(line, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        
                        if (entry != null && entry.Timestamp >= startDate && entry.Timestamp <= endDate)
                        {
                            entries.Add(entry);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger?.LogWarning(ex, "Failed to parse audit log entry: {Line}", line);
                    }
                }
            }
        }
        
        return entries.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// Get audit entries for a specific card number
    /// </summary>
    public async Task<List<AuditLogEntry>> GetCardAuditHistoryAsync(string cardNumber, int daysPast = 30)
    {
        var endDate = DateTime.Now;
        var startDate = endDate.AddDays(-daysPast);
        
        var allEntries = await GetAuditEntriesAsync(startDate, endDate);
        return allEntries.Where(e => e.CardNumber.Equals(cardNumber, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Clean up old audit logs (for maintenance)
    /// </summary>
    public Task CleanupOldLogsAsync(int retentionDays = 365)
    {
        var cutoffDate = DateTime.Today.AddDays(-retentionDays);
        var logFiles = Directory.GetFiles(_logDirectory, "audit-*.jsonl");
        
        foreach (var logFile in logFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(logFile);
            if (fileName.StartsWith("audit-") && fileName.Length >= 16) // "audit-yyyy-MM-dd"
            {
                var dateString = fileName.Substring(6, 10); // Extract "yyyy-MM-dd"
                if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var logDate))
                {
                    if (logDate < cutoffDate)
                    {
                        File.Delete(logFile);
                        _logger?.LogInformation("Deleted old audit log: {LogFile}", logFile);
                    }
                }
            }
        }
        
        return Task.CompletedTask;
    }
}

/// <summary>
/// Represents an audit log entry for card access attempts
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    /// Timestamp when the card read occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Unique identifier for this audit event
    /// </summary>
    public Guid EventId { get; set; }
    
    /// <summary>
    /// ID of the reader that processed the card
    /// </summary>
    public Guid ReaderId { get; set; }
    
    /// <summary>
    /// Name of the reader that processed the card
    /// </summary>
    public string ReaderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Card number that was read
    /// </summary>
    public string CardNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Bit length of the card data
    /// </summary>
    public int BitLength { get; set; }
    
    /// <summary>
    /// Additional data associated with the card read
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}