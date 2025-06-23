using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.ComponentModel;
using System.Runtime.InteropServices;

// PrintBox type used for printable area
// and output rendering target
public struct PrintBox
{
    public float left;
    public float top;
    public float width;
    public float height;
}

public class PrinterSpec
{
    public string? ShortName { get; set; }  // printer device name
    public string? DeviceName { get; set; } // printer device name
    public float PaperWidth { get; set; }   // paper total width in inches
    public float PaperHeight { get; set; }  // paper total height in inches
    public PrintBox PrintArea { get; set; }
    public uint Xdpi { get; set; }          // horizontal DPI
    public uint Ydpi { get; set; }          // vertical DPI
    public float Xmargin { get; set; }      // horizontal margin (size for each left & right margins) in inches)
    public float Ymargin { get; set; }      // horizontal margin (size for each left & right margins) in inches)

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }

    static PrinterSpec[] printers = new[] {
        new PrinterSpec
        {
            ShortName = "A776 Check",
            DeviceName = "CognitiveTPG Narrow Slip",
            PrintArea = new PrintBox { left = 0f, top = 0f, width = 307.24637f, height = 600f }, // in 100ths of an inch
            Xdpi = 138,
            Ydpi = 72
        },
        new PrinterSpec
        {
            ShortName = "A776WS Receipt",
            DeviceName = "CognitiveTPG Receipt",
            PrintArea = new PrintBox { left = 15.841584f, top = 0f, width = 285.14853f, height = 3600f }, // in 100ths of an inch
            Xdpi = 202,
            Ydpi = 204
        },
        new PrinterSpec
        {
            ShortName = "PDF Check",
            DeviceName = "Microsoft Print to PDF",
            PrintArea = new PrintBox { left = 0f, top = 0f, width = 850f, height = 1100f }, // in 100ths of an inch
            Xdpi = 600,
            Ydpi = 600
        }
    };

    public static PrinterSpec getPrinterSpec(string shortName)
    {
       return printers.First(p => string.Equals(p.ShortName, shortName, StringComparison.OrdinalIgnoreCase));
    }
}

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
            // Use a WebBrowser control to render the HTML
            var webBrowser = new WebBrowser { ScriptErrorsSuppressed = true };
            webBrowser.DocumentCompleted += WebBrowser_DocumentCompleted;
            webBrowser.DocumentText = _htmlContent ?? string.Empty;
 
            // Application.Run() starts a message loop for the browser events to fire.
            Application.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in printing thread: {ex.Message}");
            // Ensure the main thread is released in case of an exception before the message loop runs.
            _mre.Set();
        }
    }
 
    private void WebBrowser_DocumentCompleted(object? sender, WebBrowserDocumentCompletedEventArgs e)
    {
        if (sender is not WebBrowser webBrowser) return;

        // The DocumentCompleted event can fire multiple times (e.g., for frames).
        // We only want to print once the main document is ready.
        if (e.Url.AbsolutePath != webBrowser.Url?.AbsolutePath)
        {
            return;
        }

        // Unsubscribe to prevent the handler from running multiple times.
        webBrowser.DocumentCompleted -= WebBrowser_DocumentCompleted;

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

            webBrowser.Print();
            Console.WriteLine("Print job sent to the spooler. Will clean up in a few seconds...");

            // The WebBrowser.Print() method is asynchronous. We must not dispose the control
            // or exit the thread's message loop until printing is complete. Since there's no
            // direct event for print completion, we use a timer to delay the cleanup.
            // This keeps the message loop running, allowing the print job to be processed.
            var cleanupTimer = new System.Windows.Forms.Timer { Interval = 5000 }; // 5-second delay
            cleanupTimer.Tick += (timerSender, timerArgs) =>
            {
                cleanupTimer.Stop();
                try
                {
                    Console.WriteLine($"Restoring default printer to: '{originalDefaultPrinter}'...");
                    SetDefaultPrinter(originalDefaultPrinter);
                }
                finally
                {
                    webBrowser.Dispose();
                    Application.ExitThread();
                    _mre.Set();
                    cleanupTimer.Dispose();
                }
            };
            cleanupTimer.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during printing: {ex.Message}");
            // If an error occurs, we must clean up and exit immediately.
            try
            {
                Console.WriteLine($"Restoring default printer to: '{originalDefaultPrinter}'...");
                SetDefaultPrinter(originalDefaultPrinter);
            }
            finally
            {
                webBrowser.Dispose();
                Application.ExitThread();
                _mre.Set();
            }
        }
    }

    public RenderAndPrintHTML(string printerName)
    {
        this._printer = PrinterSpec.getPrinterSpec(printerName);
    }
}
