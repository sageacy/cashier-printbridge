using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace TPGBridge
{
    // Note: This implementation requires the PuppeteerSharp NuGet package.
    // For silent printing to physical printers, it also depends on an external
    // program (SumatraPDF), which must be installed and in the system's PATH.

    public class PuppeteerPrintService : IPrintService
    {
        private readonly PrinterSpec _printer;

        public PuppeteerPrintService(string printerName)
        {
            _printer = PrinterSpec.getPrinterSpec(printerName) ?? throw new ArgumentException($"Printer with short name '{printerName}' not found.", nameof(printerName));
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
            Console.WriteLine("Ensuring browser for Puppeteer is available...");
            await new BrowserFetcher().DownloadAsync();

            // 3. Launch a headless browser instance.
            Console.WriteLine("Launching headless browser...");
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            await using var page = await browser.NewPageAsync();

            // 4. Set the page content to our rendered HTML.
            await page.SetContentAsync(htmlContent, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });

            // 5. Generate a PDF from the page content into a temporary file.
            string tempPdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");

            try
            {
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

                Console.WriteLine($"Generating temporary PDF at: {tempPdfPath}");
                await page.PdfAsync(tempPdfPath, pdfOptions);

                // 6. Print the generated PDF to the target printer.
                PrintPdf(tempPdfPath, _printer.DeviceName);
            }
            finally
            {
                // 7. Clean up the temporary PDF file.
                if (File.Exists(tempPdfPath))
                {
                    try
                    {
                        File.Delete(tempPdfPath);
                        Console.WriteLine("Cleaned up temporary PDF file.");
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Warning: Could not delete temporary file '{tempPdfPath}'. {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Prints a PDF file to a specified printer silently using an external tool.
        /// </summary>
        private void PrintPdf(string pdfPath, string printerName)
        {
            const string printExecutable = "SumatraPDF.exe";

            var startInfo = new ProcessStartInfo
            {
                FileName = printExecutable,
                Arguments = $"-print-to \"{printerName}\" -silent -exit-on-print \"{pdfPath}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            };

            Console.WriteLine($"Attempting to print '{Path.GetFileName(pdfPath)}' to printer '{printerName}' using {printExecutable}...");

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException($"Could not start the printing process. Ensure '{printExecutable}' is installed and accessible via the system's PATH.");
                }

                bool exited = process.WaitForExit(30000); // 30-second timeout
                if (!exited)
                {
                    process.Kill();
                    throw new TimeoutException("The printing process timed out and was terminated.");
                }
                Console.WriteLine("Print command sent successfully.");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException($"Failed to start '{printExecutable}'. Please ensure it is installed and in your system's PATH. Error: {ex.Message}", ex);
            }
        }
    }
}