// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;
using System.Net.Sockets;

namespace MarkView.Avalonia.Tests.Shared;

/// <summary>
/// Minimal in-process HTTP server on <c>localhost</c> with canned responses, used to exercise
/// remote-loading code paths without real network access. Unknown paths answer 404.
/// </summary>
internal sealed class LoopbackHttpServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly IReadOnlyDictionary<string, Action<HttpListenerResponse>> _routes;
    private readonly Uri _baseUri;

    public LoopbackHttpServer(IReadOnlyDictionary<string, Action<HttpListenerResponse>> routes)
    {
        _routes = routes;
        _baseUri = new Uri($"http://localhost:{GetFreePort()}/");
        _listener.Prefixes.Add(_baseUri.ToString());
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    public string Url(string path) => new Uri(_baseUri, path).ToString();

    public static Action<HttpListenerResponse> Content(string contentType, byte[] body) => response =>
    {
        response.ContentType = contentType;
        response.ContentLength64 = body.Length;
        response.OutputStream.Write(body);
        response.Close();
    };

    /// <summary>Declares a longer body than it sends, then aborts the connection mid-body.</summary>
    public static Action<HttpListenerResponse> Truncated(byte[] partialBody) => response =>
    {
        response.ContentLength64 = partialBody.Length + 1_000;
        response.OutputStream.Write(partialBody);
        response.OutputStream.Flush();
        response.Abort();
    };

    private async Task AcceptLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_listener.IsListening)
            {
                return;
            }

            try
            {
                if (_routes.TryGetValue(context.Request.Url!.AbsolutePath, out var respond))
                {
                    respond(context.Response);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
            {
                // The client disconnected or the server was disposed mid-request; keep serving.
            }
        }
    }

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();
    }
}
