using System.IO;

namespace TPGBridge
{
    /// <summary>
    /// Specifies the available print service implementations.
    /// </summary>
    public enum PrintServiceType
    {
        /// <summary>
        /// Use PuppeteerPrintService for printing (generates PDF via Puppeteer, then prints).
        /// </summary>
        Puppeteer,
        
        /// <summary>
        /// Use EdgePrintService for printing (prints HTML directly via Microsoft Edge).
        /// </summary>
        Edge
    }

    /// <summary>
    /// Common configuration settings for all print services.
    /// </summary>
    public static class PrintConfig
    {
        /// <summary>
        /// Gets or sets the default print service implementation to use.
        /// Defaults to Edge.
        /// </summary>
        public static PrintServiceType DefaultPrintService { get; set; } = PrintServiceType.Edge;

        /// <summary>
        /// Gets or sets whether to keep temporary PDF files generated during printing.
        /// Useful for debugging PDF generation issues.
        /// </summary>
        public static bool KeepTempPDFs { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to keep temporary HTML files generated during printing.
        /// Useful for debugging HTML rendering issues.
        /// </summary>
        public static bool KeepTempHtmlFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets the directory path for storing temporary PDF files.
        /// Defaults to the system temporary directory.
        /// </summary>
        public static string TempPDFDir { get; set; } = Path.GetTempPath();

        /// <summary>
        /// Gets or sets the directory path for storing temporary HTML files.
        /// Defaults to the system temporary directory.
        /// </summary>
        public static string TempHtmlDir { get; set; } = Path.GetTempPath();

        /// <summary>
        /// Gets or sets whether to keep all temporary files (both PDF and HTML).
        /// This is a convenience property that sets both KeepTempPDFs and KeepTempHtmlFiles.
        /// </summary>
        public static bool KeepAllTempFiles
        {
            get => KeepTempPDFs && KeepTempHtmlFiles;
            set
            {
                KeepTempPDFs = value;
                KeepTempHtmlFiles = value;
            }
        }

        /// <summary>
        /// Gets or sets the common temporary directory for all print-related files.
        /// This is a convenience property that sets both TempPDFDir and TempHtmlDir.
        /// </summary>
        public static string TempDir
        {
            get => TempPDFDir; // Return PDF dir as primary
            set
            {
                TempPDFDir = value;
                TempHtmlDir = value;
            }
        }

        /// <summary>
        /// Creates an instance of the configured default print service implementation.
        /// </summary>
        /// <param name="printerName">The name of the printer to use.</param>
        /// <returns>An IPrintService instance based on the DefaultPrintService setting.</returns>
        public static IPrintService CreatePrintService(string printerName)
        {
            return DefaultPrintService switch
            {
                PrintServiceType.Edge => new EdgePrintService(printerName),
                PrintServiceType.Puppeteer => new PuppeteerPrintService(printerName),
                _ => throw new InvalidOperationException($"Unsupported print service type: {DefaultPrintService}")
            };
        }

        /// <summary>
        /// Creates an instance of the specified print service implementation.
        /// </summary>
        /// <param name="printerName">The name of the printer to use.</param>
        /// <param name="serviceType">The specific print service type to create.</param>
        /// <returns>An IPrintService instance of the specified type.</returns>
        public static IPrintService CreatePrintService(string printerName, PrintServiceType serviceType)
        {
            return serviceType switch
            {
                PrintServiceType.Edge => new EdgePrintService(printerName),
                PrintServiceType.Puppeteer => new PuppeteerPrintService(printerName),
                _ => throw new InvalidOperationException($"Unsupported print service type: {serviceType}")
            };
        }
    }
}
