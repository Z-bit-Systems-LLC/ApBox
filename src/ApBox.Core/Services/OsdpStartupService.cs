using ApBox.Core.Data.Repositories;
using ApBox.Core.Extensions;
using ApBox.Core.OSDP;

namespace ApBox.Core.Services;

/// <summary>
/// Hosted service that initializes OSDP communication on application startup
/// </summary>
public class OsdpStartupService : IHostedService
{
    private readonly IOsdpCommunicationManager _osdpManager;
    private readonly IReaderConfigurationRepository _readerConfigRepository;
    private readonly IOsdpSecurityService _securityService;
    private readonly ILogger<OsdpStartupService> _logger;

    public OsdpStartupService(
        IOsdpCommunicationManager osdpManager,
        IReaderConfigurationRepository readerConfigRepository,
        IOsdpSecurityService securityService,
        ILogger<OsdpStartupService> logger)
    {
        _osdpManager = osdpManager;
        _readerConfigRepository = readerConfigRepository;
        _securityService = securityService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting OSDP communication system...");

        try
        {
            // Load all reader configurations from database
            var readerConfigs = (await _readerConfigRepository.GetAllAsync()).ToList();
            
            _logger.LogInformation("Found {ReaderCount} reader configurations", readerConfigs.Count());

            // Add each enabled reader to the OSDP communication manager
            foreach (var readerConfig in readerConfigs.Where(r => r.IsEnabled))
            {
                try
                {
                    // Convert reader configuration to OSDP device configuration
                    var osdpConfig = readerConfig.ToOsdpConfiguration(_securityService);
                    
                    // Add device to communication manager
                    var success = await _osdpManager.AddDeviceAsync(osdpConfig);
                    
                    if (success)
                    {
                        _logger.LogInformation("Added OSDP device {ReaderName} (Address: {Address}, Port: {SerialPort})", 
                            readerConfig.ReaderName, readerConfig.Address, readerConfig.SerialPort ?? "None");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to add OSDP device {ReaderName}", readerConfig.ReaderName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding OSDP device {ReaderName}", readerConfig.ReaderName);
                }
            }

            // Start the OSDP communication manager
            await _osdpManager.StartAsync();
            
            _logger.LogInformation("OSDP communication system started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OSDP communication system");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OSDP communication system...");

        try
        {
            await _osdpManager.StopAsync();
            _logger.LogInformation("OSDP communication system stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping OSDP communication system");
        }
    }
}