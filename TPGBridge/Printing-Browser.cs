using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PrintingBrowser {
    // PrintBox type used for printable area
    // and output rendering target

    public class RenderAndPrintHTML
    {
        private string? _htmlContent;
        private PrinterSpec? _printer;
        private ManualResetEvent _mre = new ManualResetEvent(false);

        // --- Win32 API to set the default printer ---
        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDefaultPrinter(string Name);

        /// <summary>
        /// Merges a Handlebars template with data and prints the RENDERED HTML to a named printer.
        /// </summary>
        /// <param name="htmlTemplate">The HTML-based Handlebars template string.</param>
        /// <param name="data">An object containing the data to merge.</param>
        /// <param name="printerName">The exact name of the printer to use (e.g., "Microsoft Print to PDF").</param>
        public void RenderAndPrint(string htmlTemplate, object data)
        {
            // 1. Merge the template to create the final HTML string
            _htmlContent = HandlebarsWrapper.Render(htmlTemplate, data);

            // Console.WriteLine("--- Generated HTML ---");
            // Console.WriteLine(_htmlContent);
            // Console.WriteLine("----------------------");

            // 2. Print the rendered HTML. Printing UI controls requires an STA thread.
            var thread = new Thread(PrintThread);
            thread.SetApartmentState(ApartmentState.STA); // Set thread to Single-Threaded Apartment
            thread.Start();
    
            // Wait for the printing thread to complete
            _mre.WaitOne();
        }
    
        private void PrintThread()
        {
            try
            {
                // Use a hidden form to host the WebBrowser control. This provides a proper
                // message loop and window handle, which can improve stability when the
                // control needs to show modal dialogs (like a print dialog).
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
                            // If we can't even create the browser, close the form to exit the thread.
                            form.Close();
                        }
                    };

                    // Application.Run(form) starts a message loop and shows the form (invisibly).
                    // The loop exits when form.Close() is called.
                    Application.Run(form);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in printing thread: {ex.Message}");
            }
            finally
            {
                // Ensure the main thread is always released, regardless of success or failure.
                _mre.Set();
            }
        }
    
        private void WebBrowser_DocumentCompleted(object? sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (sender is not WebBrowser webBrowser) return;

            // The DocumentCompleted event can fire multiple times. We only want to print once.
            if (webBrowser.Url == null || e?.Url?.AbsolutePath != webBrowser.Url.AbsolutePath)
            {
                return;
            }

            // Unsubscribe to prevent the handler from running again.
            webBrowser.DocumentCompleted -= WebBrowser_DocumentCompleted;

            // We can assert that the form is not null because we explicitly parented it.
            var parentForm = webBrowser.FindForm()!;

            string originalDefaultPrinter = new PrinterSettings().PrinterName;
            Console.WriteLine($"Original default printer was: '{originalDefaultPrinter}'");

            try
            {
                if (_printer?.DeviceName == null)
                {
                    throw new InvalidOperationException("Target printer DeviceName is not set.");
                }

                Console.WriteLine($"Temporarily setting default printer to: '{_printer.DeviceName}'...");
                if (!SetDefaultPrinter(_printer.DeviceName))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // This call should block until the user interacts with the print dialog (e.g., clicks Save/Print or Cancel).
                webBrowser.Print();
                Console.WriteLine("Print job sent to the spooler. Will clean up in a few seconds...");

                // The WebBrowser.Print() method spools the job asynchronously. We must not dispose the control
                // or exit the thread's message loop immediately, or the print job may fail.
                // A timer is a pragmatic, if imperfect, way to allow time for spooling.
                var cleanupTimer = new System.Windows.Forms.Timer { Interval = 5000 }; // 5-second delay
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
                        // Closing the form exits the message loop on the print thread.
                        parentForm.Close();
                    }
                };
                cleanupTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during printing: {ex.Message}");
                // On error, clean up and exit the thread immediately.
                try
                {
                    Console.WriteLine($"Restoring default printer to: '{originalDefaultPrinter}'...");
                    SetDefaultPrinter(originalDefaultPrinter);
                }
                finally
                {
                    parentForm.Close();
                }
            }
        }

        public RenderAndPrintHTML(string printerName)
        {
            this._printer = PrinterSpec.getPrinterSpec(printerName);
        }
    }
}
