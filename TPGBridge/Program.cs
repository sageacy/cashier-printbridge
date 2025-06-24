﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using TPGBridge;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        Console.WriteLine("WebSocket print server starting...");
        Console.WriteLine("Listening for WebSocket connections on ws://localhost:5000/print");
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://localhost:5000"); // Explicitly listen on port 5000
                webBuilder.Configure(app =>
                {
                    app.UseWebSockets(); // Enable WebSockets

                    // Map the /print endpoint to our WebSocket handler middleware
                    app.Map("/print", wsApp =>
                    {
                        wsApp.UseMiddleware<WebSocketHandler>();
                    });

                    // Fallback for any other request
                    app.Run(async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("This server only accepts WebSocket connections on the /print endpoint.");
                    });
                });
            });
}