using System;
using System.Dynamic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TPGBridge
{
    // Defines the structure of the incoming WebSocket message
    public class PrintRequest
    {
        public string? printer { get; set; }
        public string? html { get; set; }
    }

    public class WebSocketHandler
    {
        private readonly ILogger<WebSocketHandler> _logger;

        // The 'next' delegate is required for middleware, but we won't use it
        // because this handler is a terminal endpoint for the /print path.
        public WebSocketHandler(RequestDelegate next)
        {
            _logger = AppLogger.CreateLogger<WebSocketHandler>();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                _logger.LogInformation("WebSocket connection established from {RemoteIpAddress}", context.Connection.RemoteIpAddress);
                await HandleWebSocketSession(webSocket);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Error: Expected a WebSocket request.");
            }
        }

        private async Task HandleWebSocketSession(WebSocket webSocket)
        {
            var buffer = new byte[32 * 1024]; // 32KB buffer for incoming messages

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                    _logger.LogInformation("Received WebSocket message.");

                    PrintRequest? printRequest = null;
                    try
                    {
                        printRequest = JsonSerializer.Deserialize<PrintRequest>(message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                    }
                    catch (JsonException jsonEx)
                    {
                        const string msg = "Error deserializing print request JSON";
                        _logger.LogError(jsonEx, msg);
                        await SendResponseAsync(webSocket, new { success = false, message = msg });
                    }

                    // Ensure that the html to print is provided
                    if (! string.IsNullOrWhiteSpace(printRequest?.html))
                    {
                        await SendResponseAsync(webSocket, new { success = false, message = "Invalid print request, html must be provided." });
                        continue;
                    }

                    // TODO remove the static printer configurations and use printers discovered by Windows

                    IPrintService? printer = null;
                    if (!string.IsNullOrWhiteSpace(printRequest?.printer)) {
                        printer = PrintConfig.CreatePrintService(printRequest.printer);
                    }

                    if (printer == null)
                    {
                        await SendResponseAsync(webSocket, new { success = false, message = "Invalid printer name in request" });
                        continue;          
                    }

                    if (!string.IsNullOrWhiteSpace(printRequest?.html))
                    {
                        try
                        {
                            _logger.LogInformation("Sending print job to '{printer}'", printRequest.printer);
                            await printer.RenderAndPrintHTML(printRequest.html);
                            _logger.LogInformation("Print job completed successfully");
                            await SendResponseAsync(webSocket, new { success = true, message = "Print job completed successfully." });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "An error occurred while processing the print job for printer '{printer}'.", printRequest.printer);
                            await SendResponseAsync(webSocket, new { success = false, message = $"An error occurred: {ex.Message}" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in the WebSocket session.");
            }
            finally
            {
                _logger.LogInformation("WebSocket session ended.");
            }
        }

        private static async Task SendResponseAsync(WebSocket webSocket, object response)
        {
            if (webSocket.State != WebSocketState.Open) return;
            var responseJson = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
            await webSocket.SendAsync(new ArraySegment<byte>(responseBytes, 0, responseBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}