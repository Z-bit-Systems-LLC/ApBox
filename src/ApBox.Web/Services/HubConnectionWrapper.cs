using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;

namespace ApBox.Web.Services;

/// <summary>
/// Implementation of IHubConnectionWrapper that wraps SignalR HubConnection
/// </summary>
public class HubConnectionWrapper : IHubConnectionWrapper
{
    private readonly HubConnection _hubConnection;
    
    public HubConnectionWrapper(NavigationManager navigationManager)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(navigationManager.ToAbsoluteUri("/cardevents"))
            .WithAutomaticReconnect()
            .Build();
    }
    
    /// <summary>
    /// Gets the current connection state
    /// </summary>
    public HubConnectionState State => _hubConnection.State;
    
    /// <summary>
    /// Registers a handler that will be invoked when the method with the specified method name is invoked
    /// </summary>
    public IDisposable On<T>(string methodName, Func<T, Task> handler)
    {
        return _hubConnection.On(methodName, handler);
    }
    
    /// <summary>
    /// Starts the connection
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return _hubConnection.StartAsync(cancellationToken);
    }
    
    /// <summary>
    /// Stops the connection
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _hubConnection.StopAsync(cancellationToken);
    }
    
    /// <summary>
    /// Disposes the hub connection
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}