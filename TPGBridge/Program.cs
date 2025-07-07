﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using TPGBridge;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Set Edge as the default print service
        PrintConfig.DefaultPrintService = PrintServiceType.Edge;
        
        // Parse command line arguments
        ParseCommandLineArguments(args);
        
        var host = CreateHostBuilder(args).Build();
        
        Console.WriteLine("WebSocket print server starting...");
        Console.WriteLine($"Using print service: {PrintConfig.DefaultPrintService}");
        Console.WriteLine("Listening for WebSocket connections on ws://localhost:5000/print");
        
        await host.RunAsync();
    }

    private static void ParseCommandLineArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--print-service":
                case "-ps":
                    if (i + 1 < args.Length)
                    {
                        if (Enum.TryParse<PrintServiceType>(args[i + 1], true, out var serviceType))
                        {
                            PrintConfig.DefaultPrintService = serviceType;
                            Console.WriteLine($"Print service set to: {serviceType}");
                        }
                        else
                        {
                            Console.WriteLine($"Invalid print service type: {args[i + 1]}. Currently only Edge supported");
                            Environment.Exit(1);
                        }
                        i++; // Skip the next argument since we consumed it
                    }
                    else
                    {
                        Console.WriteLine("Error: --print-service requires a value. Currently only Edge supported");
                        Environment.Exit(1);
                    }
                    break;

                case "--keep-temp-files":
                case "-kt":
                    PrintConfig.KeepAllTempFiles = true;
                    Console.WriteLine("Temporary files will be kept for debugging");
                    break;

                case "--temp-dir":
                case "-td":
                    if (i + 1 < args.Length)
                    {
                        PrintConfig.TempDir = args[i + 1];
                        Console.WriteLine($"Temporary directory set to: {args[i + 1]}");
                        i++; // Skip the next argument since we consumed it
                    }
                    else
                    {
                        Console.WriteLine("Error: --temp-dir requires a directory path");
                        Environment.Exit(1);
                    }
                    break;

                case "--help":
                case "-h":
                case "/?":
                    ShowHelp();
                    Environment.Exit(0);
                    break;

                case string arg when arg.StartsWith("--"):
                    Console.WriteLine($"Unknown option: {arg}");
                    Console.WriteLine("Use --help for usage information");
                    Environment.Exit(1);
                    break;
            }
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("TPGBridge - WebSocket Print Server");
        Console.WriteLine();
        Console.WriteLine("Usage: TPGBridge [OPTIONS]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        /* Disabling the --print-service command-line option as currently only supporting edge
                Console.WriteLine("  --print-service, -ps <type>    Set the print service type (Edge or Puppeteer");
                Console.WriteLine("                                  Default: Edge");
        */

        Console.WriteLine("  --keep-temp-files, -ktf        Keep temporary files for debugging");
        Console.WriteLine("  --temp-dir, -td <path>         Set temporary files directory");
        Console.WriteLine("  --help, -h                     Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        // Console.WriteLine("  TPGBridge --print-service Edge");
        // Console.WriteLine("  TPGBridge -ps Puppeteer --keep-temp-files");
        Console.WriteLine("  TPGBridge --temp-dir \"C:\\PrintTemp\" --keep-temp-files ");
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                // listen on loopback endpoints to prevent external connections 
                webBuilder.UseUrls("http://127.0.0.1:5000", "http://[::1]:5000"); 
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