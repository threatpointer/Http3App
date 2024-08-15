using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace Http3App.Middleware
{
    public class ConnectionLogMiddleware
    {
        private readonly RequestDelegate _next;
        private static List<QuicConnectionDetail> activeConnections = new List<QuicConnectionDetail>();
        private static List<QuicConnectionDetail> staleConnections = new List<QuicConnectionDetail>();

        public ConnectionLogMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var connectionId = context.Connection.Id;
            var quicDetails = await CaptureQuicDetails(context); // Capture the QUIC details

            activeConnections.Add(new QuicConnectionDetail
            {
                ConnectionId = connectionId,
                QuicPacketDetails = quicDetails
            });

            context.Response.OnCompleted(() =>
            {
                var connection = activeConnections.Find(c => c.ConnectionId == connectionId);
                if (connection != null)
                {
                    activeConnections.Remove(connection);
                    staleConnections.Add(connection);
                }
                return Task.CompletedTask;
            });

            await _next(context);
        }

        private async Task<QuicPacketDetails> CaptureQuicDetails(HttpContext context)
        {
            var request = context.Request;

            // Collect request details
            var method = request.Method;
            var path = request.Path;
            var headers = string.Join(", ", request.Headers.Select(h => $"{h.Key}: {h.Value}"));
            var queryString = request.QueryString.ToString();
            string body = string.Empty;

            // Capture the request body only for methods that typically have a body
            if (method == HttpMethods.Post || method == HttpMethods.Put || method == HttpMethods.Patch || method == HttpMethods.Delete)
            {
                context.Request.EnableBuffering(); // Allow multiple reads of the request body
                using (var reader = new StreamReader(request.Body, leaveOpen: true))
                {
                    body = await reader.ReadToEndAsync();
                    request.Body.Position = 0; // Reset the stream position for further processing by the application
                }
            }

            // Capture TCP connection details
            var localIp = context.Connection.LocalIpAddress?.ToString();
            var localPort = context.Connection.LocalPort;
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            var remotePort = context.Connection.RemotePort;

            return new QuicPacketDetails
            {
                Method = method,
                Path = path,
                Headers = headers,
                QueryString = queryString,
                Body = body,
                LocalIp = localIp,
                LocalPort = localPort,
                RemoteIp = remoteIp,
                RemotePort = remotePort
            };
        }

        public static List<QuicConnectionDetail> GetActiveConnections() => activeConnections;
        public static List<QuicConnectionDetail> GetStaleConnections() => staleConnections;
    }

    public class QuicConnectionDetail
    {
        public string ConnectionId { get; set; }
        public QuicPacketDetails QuicPacketDetails { get; set; }
    }

    public class QuicPacketDetails
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string Headers { get; set; }
        public string QueryString { get; set; }
        public string Body { get; set; }
        public string LocalIp { get; set; }
        public int LocalPort { get; set; }
        public string RemoteIp { get; set; }
        public int RemotePort { get; set; }
    }
}
