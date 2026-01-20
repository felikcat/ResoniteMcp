using ModelContextProtocol.Protocol;
using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NetfxMcp;

/// <summary>
/// Server transport implementation for Model Context Protocol communication using SSE and POST.
/// </summary>
public sealed class StatelessHttpServerTransport : ITransport
{
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Channel<JsonRpcMessage> _incomingChannel = Channel.CreateBounded<JsonRpcMessage>(new BoundedChannelOptions(20)
    {
        SingleReader = true,
        SingleWriter = false,
    });

    // Store active SSE clients
    private readonly ConcurrentDictionary<Guid, Channel<JsonRpcMessage>> _sseClients = new();

    private readonly byte[] _messageEventPrefix = Encoding.UTF8.GetBytes("event: message\r\ndata: ");
    private readonly byte[] _messageEventSuffix = Encoding.UTF8.GetBytes("\r\n\r\n");
    private readonly byte[] _endpointEventPrefix = Encoding.UTF8.GetBytes("event: endpoint\r\ndata: ");
    private readonly byte[] _newline = Encoding.UTF8.GetBytes("\r\n\r\n");

    /// <summary>
    /// Gets the channel reader for receiving JSON-RPC messages.
    /// </summary>
    public ChannelReader<JsonRpcMessage> MessageReader => _incomingChannel.Reader;

    /// <summary>
    /// Gets the initialization request parameters, if any have been received.
    /// </summary>
    public InitializeRequestParams? InitializeRequest { get; private set; }

    /// <summary>
    /// Handles a new SSE connection.
    /// </summary>
    public async Task HandleSseConnection(IDuplexPipe connection, string endpointUri, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        var clientChannel = Channel.CreateBounded<JsonRpcMessage>(new BoundedChannelOptions(20) { SingleReader = true, SingleWriter = true });

        _sseClients.TryAdd(clientId, clientChannel);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        var token = linkedCts.Token;

        try
        {
            // Send 'endpoint' event so client knows where to POST
            var endpointData = Encoding.UTF8.GetBytes(endpointUri);
            await connection.Output.WriteAsync(_endpointEventPrefix, token).ConfigureAwait(false);
            await connection.Output.WriteAsync(endpointData, token).ConfigureAwait(false);
            await connection.Output.WriteAsync(_newline, token).ConfigureAwait(false);
            await connection.Output.FlushAsync(token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                var message = await clientChannel.Reader.ReadAsync(token).ConfigureAwait(false);

                await connection.Output.WriteAsync(_messageEventPrefix, token).ConfigureAwait(false);
                await JsonSerializer.SerializeAsync(connection.Output.AsStream(), message, cancellationToken: token).ConfigureAwait(false);
                await connection.Output.WriteAsync(_newline, token).ConfigureAwait(false);
                await connection.Output.FlushAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { } // Client disconnected
        finally
        {
            _sseClients.TryRemove(clientId, out _);
        }
    }

    /// <summary>
    /// Handles an incoming HTTP POST request containing a JSON-RPC message.
    /// </summary>
    public async Task HandlePostRequest(IDuplexPipe httpBodies, CancellationToken cancellationToken)
    {
        try
        {
             var message = await JsonSerializer.DeserializeAsync<JsonRpcMessage>(httpBodies.Input.AsStream(), cancellationToken: cancellationToken).ConfigureAwait(false);

             if (message != null)
             {
                 if (message is JsonRpcRequest request && request.Method == RequestMethods.Initialize)
                 {
                     if (request.Params is not null)
                        InitializeRequest = JsonSerializer.Deserialize<InitializeRequestParams>(request.Params);
                 }

                 await _incomingChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
             }
        }
        catch (Exception)
        {
            // Invalid message or other error
            throw;
        }
    }

    /// <summary>
    /// Sends a JSON-RPC message asynchronously to all connected SSE clients.
    /// </summary>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        foreach (var client in _sseClients.Values)
        {
            try 
            {
                 await client.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException) { }
        }
    }

    /// <summary>
    /// Disposes the transport asynchronously.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        return default;
    }
}