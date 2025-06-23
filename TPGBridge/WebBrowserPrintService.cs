using System;
using System.Threading;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TPGBridge
{
    public class WebBrowserPrintService : IPrintService
    {
        private string? _htmlContent;
        private readonly PrinterSpec _printer;
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        // --- Win32 API to set the default printer ---
        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDefaultPrinter(string Name);

        public WebBrowserPrintService(string printerName)
        {
            _printer = PrinterSpec.getPrinterSpec(printerName) ?? throw new ArgumentException($"Printer with short name '{printerName}' not found.", nameof(printerName));
        }

        /// <summary>
        /// Merges a Handlebars template with data and prints the RENDERED HTML to a named printer.
        /// This implementation uses the legacy WebBrowser control and will show a print dialog.
        /// </summary>
        public Task RenderAndPrint(string htmlTemplate, object data)
        {
            // 1. Merge the template to create the final HTML string
            _htmlContent = HandlebarsWrapper.Render(htmlTemplate, data);
            _tcs = new TaskCompletionSource<bool>();

            // 2. Printing UI controls requires an STA thread.
            var thread = new Thread(PrintThread);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // 3. Return the task that will be completed when the print thread finishes.
            return _tcs.Task;
        }

        private void PrintThread()
        {
            try
            {
                // Use a hidden form to host the WebBrowser control.
                using (var form = new Form { Opacity = 0, ShowInTaskbar = false, WindowState = FormWindowState.Minimized })
                {
                    form.Load += (s, e) =>
                    {
                        try
                        {
                            var webBrowser = new WebBrowser { ScriptErrorsSuppressed = true, Parent = form };
                            webBrowser.DocumentCompleted += WebBrowser_DocumentCompleted;
                            webBrowser.DocumentText = _htmlContent ?? string.Empty;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating WebBrowser control: {ex.Message}");
                            form.Close(); // Close form to exit thread
                            _tcs.TrySetException(ex);
                        }
                    };

                    // This blocks until form.Close() is called.
                    Application.Run(form);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in printing thread: {ex.Message}");
                _tcs.TrySetException(ex);
            }
            finally
            {
                // Ensure the task is always completed when the thread exits.
                _tcs.TrySetResult(true);
            }
        }

        private void WebBrowser_DocumentCompleted(object? sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (sender is not WebBrowser webBrowser) return;

            if (webBrowser.Url == null || e?.Url?.AbsolutePath != webBrowser.Url.AbsolutePath)
            {
                return;
            }

            webBrowser.DocumentCompleted -= WebBrowser_DocumentCompleted;
            var parentForm = webBrowser.FindForm()!;
            string originalDefaultPrinter = new PrinterSettings().PrinterName;

            try
            {
                if (string.IsNullOrEmpty(_printer.DeviceName))
                {
                    throw new InvalidOperationException("Target printer DeviceName is not set.");
                }

                Console.WriteLine($"Temporarily setting default printer to: '{_printer.DeviceName}'...");
                if (!SetDefaultPrinter(_printer.DeviceName))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // This call shows the print dialog.
                webBrowser.Print();
                Console.WriteLine("Print job sent to the spooler. Will clean up in a few seconds...");

                // A timer is a pragmatic way to allow time for spooling before we exit.
                var cleanupTimer = new System.Windows.Forms.Timer { Interval = 5000 };
                cleanupTimer.Tick += (timerSender, timerArgs) =>
                {
                    cleanupTimer.Stop();
                    cleanupTimer.Dispose();
                    try
                    {
                        Console.WriteLine($"Restoring default printer to: '{originalDefaultPrinter}'...");
                        SetDefaultPrinter(originalDefaultPrinter);
                    }
                    finally
                    {
                        parentForm.Close(); // This exits the message loop on the print thread.
                    }
                };
                cleanupTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during printing: {ex.Message}");
                _tcs.TrySetException(ex); // Signal failure to the waiting task.
                try
                {
                    SetDefaultPrinter(originalDefaultPrinter);
                }
                finally
                {
                    parentForm.Close();
                }
            }
        }
    }
}