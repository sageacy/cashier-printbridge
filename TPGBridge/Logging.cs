using Microsoft.Extensions.Logging;

namespace TPGBridge
{
    /// <summary>
    /// Provides a centralized, static logger factory for the application.
    /// </summary>
    public static class AppLogger
    {
        private static ILoggerFactory LoggerFactory { get; } = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                })
                .SetMinimumLevel(LogLevel.Information); // Set the minimum log level (e.g., Debug, Information, Warning)
        });

        public static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
    }
}