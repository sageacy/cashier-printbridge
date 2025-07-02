using System;
using System.IO;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Microsoft.Extensions.Logging;
using PDFtoPrinter;
using System.Drawing.Printing;

namespace TPGBridge
{

    // Note: This implementation requires the PuppeteerSharp and PDFtoPrinter NuGet packages.
    // It provides a fully self-contained, headless printing solution without external dependencies.
    public class PuppeteerPrintService : IPrintService
    {
        private readonly PrinterSpec _printer;
        private readonly ILogger<PuppeteerPrintService> _logger;
        private readonly string _edgePath;

        public PuppeteerPrintService(string printerName)
        {
            _printer = PrinterSpec.getPrinterSpec(printerName) ?? throw new ArgumentException($"Printer with short name '{printerName}' not found.", nameof(printerName));
            _logger = AppLogger.CreateLogger<PuppeteerPrintService>();
            _edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            if (!File.Exists(_edgePath))
            {
                _edgePath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";
                if (!File.Exists(_edgePath))
                {
                    _logger.LogError("Microsoft Edge executable not found at default paths");
                    throw new FileNotFoundException("Microsoft Edge executable not found. Please ensure Edge is installed or specify a valid path.");
                }
            }

        }

        /// <summary>
        /// Merges a Handlebars template with data, renders it to a PDF using Puppeteer,
        /// and prints it to the specified printer silently.
        /// </summary>
        public async Task RenderAndPrintHBS(string hbs, object data)
        {
            if (string.IsNullOrEmpty(_printer.DeviceName))
            {
                throw new InvalidOperationException("Target printer DeviceName is not set.");
            }

            // Merge the template to create the final HTML string
            string html = HandlebarsWrapper.Render(hbs, data);
            await RenderAndPrintHTML(html);
        }

        public async Task RenderAndPrintHTML(string html) {
            // Launch a headless browser instance.
            _logger.LogInformation("Launching headless browser...");
            var launchOptions = new LaunchOptions
            {
                Headless = true,
                ExecutablePath = this._edgePath
            };
            await using var browser = await Puppeteer.LaunchAsync(launchOptions);
            await using var page = await browser.NewPageAsync();

            // Set the page content to our rendered HTML.
            await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });

            var pdfOptions = new PdfOptions
            {
                PrintBackground = true
            };

            // Use paper size from PrinterSpec if available.
            // Puppeteer dimensions need units (e.g., "in", "cm", "px").
            if (_printer.PaperWidth > 0 && _printer.PaperHeight > 0)
            {
                pdfOptions.Width = $"{_printer.PaperWidth}in";
                pdfOptions.Height = $"{_printer.PaperHeight}in";
            }
            else if (_printer.PrintArea.width > 0 && _printer.PrintArea.height > 0)
            {
                // Fallback to PrintArea, which is in 100ths of an inch.
                pdfOptions.Width = $"{_printer.PrintArea.width / 100.0f}in";
                pdfOptions.Height = $"{_printer.PrintArea.height / 100.0f}in";
            }

            // For all printers, generate a PDF to a temporary file first.
            string pdfPath = Path.Combine(PrintConfig.TempPDFDir, $"{Guid.NewGuid()}.pdf");
            try
            {
                _logger.LogInformation("Generating temporary PDF at: {pdfPath}", pdfPath);
                await page.PdfAsync(pdfPath, pdfOptions);

                if (String.IsNullOrEmpty(_printer?.DeviceName))
                {
                    const string msg = "Cannot print, printer DeviceName is not set.";
                    _logger.LogError(msg);
                    throw new InvalidOperationException(msg);
                }

                if (_printer.DeviceName == "Microsoft Print to PDF")
                {
                    // Special case for Microsoft Print to PDF, which doesn't require a separate print command.
                    _logger.LogInformation("Using Microsoft Print to PDF, no further action needed.");
                    return;
                }
                // Print the generated PDF to the target printer.
                await PrintPdfAsync(pdfPath, _printer.DeviceName);
            }
            finally
            {
                // Clean up the temporary PDF file.
                if (!PrintConfig.KeepTempPDFs && File.Exists(pdfPath))
                {
                    try
                    {
                        File.Delete(pdfPath);
                        _logger.LogInformation("Cleaned up temporary PDF file.");
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Could not delete temporary file {TempPdfPath}", pdfPath);
                    }
                }
            }

        }

        /// <summary>
        /// /// Prints a PDF file to a specified printer silently using an external tool.
        /// </summary>
        async Task PrintPdfAsync(string pdfPath, string printerName)
        {
            _logger.LogInformation("Attempting to print '{PdfFileName}' to printer '{PrinterName}' using PDFtoPrinter...", Path.GetFileName(pdfPath), printerName);

            try
            {
                // The obsolete method is being replaced with the recommended asynchronous approach.
                var printer = new PDFtoPrinterPrinter();
                var options = new PrintingOptions(printerName, pdfPath);

                // The new Print method is asynchronous and can be awaited directly.
                await printer.Print(options);

                _logger.LogInformation("Print command sent successfully to '{PrinterName}'.", printerName);
            }
            catch (InvalidPrinterException ex)
            {
                var errorMessage = $"The printer '{printerName}' is not valid. Please check the printer name and ensure it is installed.";
                _logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
            catch (Exception ex)
            {
                var generalErrorMessage = $"An unexpected error occurred while printing the PDF to '{printerName}'.";
                _logger.LogError(ex, generalErrorMessage);
                throw new InvalidOperationException(generalErrorMessage, ex);
            }
        }
    }
}
