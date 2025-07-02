using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TPGBridge
{

    /// <summary>
    /// Print service implementation that uses Microsoft Edge directly for printing HTML content.
    /// This implementation launches Edge with command-line arguments to print HTML files
    /// without generating intermediate PDF files.
    /// </summary>
    public class EdgePrintService : IPrintService
    {
        private readonly PrinterSpec _printer;
        private readonly ILogger<EdgePrintService> _logger;
        private readonly string _edgePath;

        public EdgePrintService(string printerName)
        {
            _printer = PrinterSpec.getPrinterSpec(printerName) ?? throw new ArgumentException($"Printer with short name '{printerName}' not found.", nameof(printerName));
            _logger = AppLogger.CreateLogger<EdgePrintService>();
            _edgePath = FindEdgeExecutable();
        }

        /// <summary>
        /// Finds the Microsoft Edge executable on the system.
        /// </summary>
        /// <returns>The path to the Edge executable.</returns>
        /// <exception cref="FileNotFoundException">Thrown when Edge executable is not found.</exception>
        private string FindEdgeExecutable()
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation("Found Microsoft Edge at: {EdgePath}", path);
                    return path;
                }
            }

            _logger.LogError("Microsoft Edge executable not found at any of the default paths");
            throw new FileNotFoundException("Microsoft Edge executable not found. Please ensure Edge is installed.");
        }

        /// <summary>
        /// Merges a Handlebars template with data, renders it to HTML,
        /// and prints it directly using Microsoft Edge.
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

        /// <summary>
        /// Prints HTML content directly using Microsoft Edge.
        /// </summary>
        /// <param name="html">The HTML content to print.</param>
        public async Task RenderAndPrintHTML(string html)
        {
            if (string.IsNullOrEmpty(_printer?.DeviceName))
            {
                const string msg = "Cannot print, printer DeviceName is not set.";
                _logger.LogError(msg);
                throw new InvalidOperationException(msg);
            }

            // Create a temporary HTML file
            string htmlPath = Path.Combine(PrintConfig.TempHtmlDir, $"{Guid.NewGuid()}.html");
            
            try
            {
                // Enhance the HTML with print-specific CSS and meta tags
                string enhancedHtml = EnhanceHtmlForPrinting(html);
                
                _logger.LogInformation("Creating temporary HTML file at: {HtmlPath}", htmlPath);
                await File.WriteAllTextAsync(htmlPath, enhancedHtml);

                if (_printer.DeviceName == "Microsoft Print to PDF")
                {
                    // For PDF printer, we can specify output location
                    await PrintWithEdgeToPdf(htmlPath);
                }
                else
                {
                    // For physical printers, print directly
                    await PrintWithEdge(htmlPath, _printer.DeviceName);
                }
            }
            finally
            {
                // Clean up the temporary HTML file
                if (!PrintConfig.KeepTempHtmlFiles && File.Exists(htmlPath))
                {
                    try
                    {
                        File.Delete(htmlPath);
                        _logger.LogInformation("Cleaned up temporary HTML file.");
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Could not delete temporary file {TempHtmlPath}", htmlPath);
                    }
                }
            }
        }

        /// <summary>
        /// Enhances HTML content with print-specific CSS and meta tags for better printing results.
        /// </summary>
        /// <param name="html">The original HTML content.</param>
        /// <returns>Enhanced HTML with print-specific styling.</returns>
        private string EnhanceHtmlForPrinting(string html)
        {
            var printCss = GeneratePrintCss();
            
            // Check if HTML already has html/head tags
            if (html.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                // Insert print CSS before closing head tag
                if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                {
                    return html.Replace("</head>", $"{printCss}\n</head>", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    // Add head section after html tag
                    return html.Replace("<html", $"<html>\n<head>\n{printCss}\n</head>", StringComparison.OrdinalIgnoreCase);
                }
            }
            else
            {
                // Wrap in complete HTML structure
                return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Print Document</title>
    {printCss}
</head>
<body>
{html}
</body>
</html>";
            }
        }

        /// <summary>
        /// Generates CSS specifically for printing based on printer specifications.
        /// </summary>
        /// <returns>CSS styles for printing.</returns>
        private string GeneratePrintCss()
        {
            var css = @"
    <style type=""text/css"">
        @media print {
            body {
                margin: 0;
                padding: 0;
                -webkit-print-color-adjust: exact;
                color-adjust: exact;
            }
            
            @page {
                margin: 0;";

            // Add page size if available from printer spec
            if (_printer.PaperWidth > 0 && _printer.PaperHeight > 0)
            {
                css += $"\n                size: {_printer.PaperWidth}in {_printer.PaperHeight}in;";
            }
            else if (_printer.PrintArea.width > 0 && _printer.PrintArea.height > 0)
            {
                // Convert from 100ths of an inch
                var widthInches = _printer.PrintArea.width / 100.0f;
                var heightInches = _printer.PrintArea.height / 100.0f;
                css += $"\n                size: {widthInches}in {heightInches}in;";
            }

            css += @"
            }
            
            * {
                box-sizing: border-box;
            }
        }
    </style>";

            return css;
        }

        /// <summary>
        /// Prints an HTML file using Microsoft Edge to a PDF file.
        /// </summary>
        /// <param name="htmlPath">Path to the HTML file to print.</param>
        private async Task PrintWithEdgeToPdf(string htmlPath)
        {
            var pdfPath = Path.Combine(PrintConfig.TempHtmlDir, $"print-{Guid.NewGuid()}.pdf");
            
            var arguments = $"--headless --disable-gpu --print-to-pdf=\"{pdfPath}\" --no-margins --disable-extensions --disable-plugins \"{htmlPath}\"";
            
            _logger.LogInformation("Printing HTML to PDF using Edge with arguments: {Arguments}", arguments);
            
            await RunEdgeProcess(arguments);
            
            _logger.LogInformation("PDF generated at: {PdfPath}", pdfPath);
        }

        /// <summary>
        /// Prints an HTML file directly to a specified printer using Microsoft Edge.
        /// </summary>
        /// <param name="htmlPath">Path to the HTML file to print.</param>
        /// <param name="printerName">Name of the printer to use.</param>
        private async Task PrintWithEdge(string htmlPath, string printerName)
        {
            // Edge command line arguments for direct printing
            var arguments = $"--headless --disable-gpu --print-to-printer=\"{printerName}\" --no-margins --disable-extensions --disable-plugins \"{htmlPath}\"";
            
            _logger.LogInformation("Printing HTML directly to printer '{PrinterName}' using Edge", printerName);
            _logger.LogDebug("Edge arguments: {Arguments}", arguments);
            
            await RunEdgeProcess(arguments);
            
            _logger.LogInformation("Print command sent successfully to '{PrinterName}'.", printerName);
        }

        /// <summary>
        /// Runs Microsoft Edge with the specified arguments and waits for completion.
        /// </summary>
        /// <param name="arguments">Command line arguments for Edge.</param>
        private async Task RunEdgeProcess(string arguments)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _edgePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Microsoft Edge process.");
                }

                // Wait for the process to complete with a reasonable timeout
                var completed = await WaitForProcessAsync(process, TimeSpan.FromMinutes(2));
                
                if (!completed)
                {
                    _logger.LogWarning("Edge process did not complete within timeout, attempting to kill process");
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill Edge process");
                    }
                    throw new TimeoutException("Microsoft Edge print operation timed out.");
                }

                if (process.ExitCode != 0)
                {
                    var errorOutput = await process.StandardError.ReadToEndAsync();
                    var standardOutput = await process.StandardOutput.ReadToEndAsync();
                    
                    _logger.LogError("Edge process exited with code {ExitCode}. Error: {ErrorOutput}, Output: {StandardOutput}", 
                        process.ExitCode, errorOutput, standardOutput);
                    
                    throw new InvalidOperationException($"Microsoft Edge print operation failed with exit code {process.ExitCode}. Error: {errorOutput}");
                }

                _logger.LogDebug("Edge process completed successfully with exit code 0");
            }
            catch (Exception ex) when (!(ex is TimeoutException || ex is InvalidOperationException))
            {
                var errorMessage = "An unexpected error occurred while running Microsoft Edge for printing.";
                _logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// Waits for a process to complete with a specified timeout.
        /// </summary>
        /// <param name="process">The process to wait for.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>True if the process completed within the timeout, false otherwise.</returns>
        private static async Task<bool> WaitForProcessAsync(Process process, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) => tcs.TrySetResult(true);
            
            if (process.HasExited)
            {
                return true;
            }

            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            return completedTask == tcs.Task;
        }
    }
}
