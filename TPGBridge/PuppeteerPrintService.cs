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

        public PuppeteerPrintService(string printerName)
        {
            _printer = PrinterSpec.getPrinterSpec(printerName) ?? throw new ArgumentException($"Printer with short name '{printerName}' not found.", nameof(printerName));
            _logger = AppLogger.CreateLogger<PuppeteerPrintService>();
        }

        /// <summary>
        /// Merges a Handlebars template with data, renders it to a PDF using Puppeteer,
        /// and prints it to the specified printer silently.
        /// </summary>
        public async Task RenderAndPrint(string htmlTemplate, object data)
        {
            if (string.IsNullOrEmpty(_printer.DeviceName))
            {
                throw new InvalidOperationException("Target printer DeviceName is not set.");
            }

            // 1. Merge the template to create the final HTML string
            string htmlContent = HandlebarsWrapper.Render(htmlTemplate, data);

            // 2. Download the browser for Puppeteer if it's not already present.
            _logger.LogInformation("Ensuring browser for Puppeteer is available...");
            await new BrowserFetcher().DownloadAsync();

            // 3. Launch a headless browser instance.
            _logger.LogInformation("Launching headless browser...");
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            await using var page = await browser.NewPageAsync();

            // 4. Set the page content to our rendered HTML.
            await page.SetContentAsync(htmlContent, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });

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

            // Special handling for "Microsoft Print to PDF" to avoid the print dialog.
            if (_printer.DeviceName.Equals("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase))
            {
                // For a PDF printer, the "end-to-end" test is successfully creating the PDF file.
                // We can do this directly with Puppeteer, bypassing the interactive print dialog.
                string fileName = $"Invoice-{data.GetType().GetProperty("InvoiceNumber")?.GetValue(data) ?? Guid.NewGuid()}.pdf";
                string outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

                _logger.LogInformation("Target is a PDF printer. Generating file directly to: {OutputPath}", outputPath);
                await page.PdfAsync(outputPath, pdfOptions);
                _logger.LogInformation("PDF generated successfully. Skipping external print step.");
                return; // We are done.
            }

            // 5. For physical printers, generate a PDF to a temporary file.
            string tempPdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
            try
            {
                _logger.LogInformation("Generating temporary PDF at: {TempPdfPath}", tempPdfPath);
                await page.PdfAsync(tempPdfPath, pdfOptions);

                // 6. Print the generated PDF to the target printer.
                await PrintPdfAsync(tempPdfPath, _printer.DeviceName);
            }
            finally
            {
                // 7. Clean up the temporary PDF file.
                if (File.Exists(tempPdfPath))
                {
                    try
                    {
                        File.Delete(tempPdfPath);
                        _logger.LogInformation("Cleaned up temporary PDF file.");
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Could not delete temporary file {TempPdfPath}", tempPdfPath);
                    }
                }
            }
        }
        /// <summary>
        /// Prints a PDF file to a specified printer silently using an external tool.
        /// </summary>
        private async Task PrintPdfAsync(string pdfPath, string printerName)
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
            // The InvalidPrinterException now comes from the PDFtoPrinter.Printing namespace.
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