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
        public string? PrinterName { get; set; }
        public string? Template { get; set; }
        public JsonElement Data { get; set; } // Use JsonElement to handle arbitrary JSON data
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
            var buffer = new byte[1024 * 8]; // 8KB buffer for incoming messages

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

                    PrintRequest printRequest;
                    try
                    {
                        printRequest = JsonSerializer.Deserialize<PrintRequest>(message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Error deserializing print request JSON.");
                        await SendResponseAsync(webSocket, new { success = false, message = $"Invalid JSON format: {jsonEx.Message}" });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(printRequest.PrinterName) || string.IsNullOrWhiteSpace(printRequest.Template))
                    {
                        await SendResponseAsync(webSocket, new { success = false, message = "Invalid print request. 'printerName' and 'template' are required." });
                        continue;
                    }

                    try
                    {
                        // Deserialize the data part into a dynamic ExpandoObject that Handlebars can use.
                        object dataObject = JsonSerializer.Deserialize<ExpandoObject>(printRequest.Data.GetRawText())!;
                        _logger.LogInformation("Processing print request for printer: {PrinterName}", printRequest.PrinterName);

                        IPrintService printer = new PuppeteerPrintService(printRequest.PrinterName);
                        await printer.RenderAndPrintHBS(printRequest.Template, dataObject);

                        _logger.LogInformation("Print job completed successfully for printer: {PrinterName}", printRequest.PrinterName);
                        await SendResponseAsync(webSocket, new { success = true, message = "Print job completed successfully." });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while processing the print job for printer '{PrinterName}'.", printRequest.PrinterName);
                        await SendResponseAsync(webSocket, new { success = false, message = $"An error occurred: {ex.Message}" });
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