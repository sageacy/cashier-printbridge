using System.Drawing.Printing;
using System.Text;
using System.Text.Json;

namespace TPGBridge
{
    public struct PrintBox
    {
        public float left;
        public float top;
        public float width;
        public float height;
    }

    public class PrinterSpec
    {
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

        static readonly PrinterSpec[] printers = new[] {
            new PrinterSpec
            {
                DeviceName = "CognitiveTPG Narrow Slip",
                PrintArea = new PrintBox { left = 0f, top = 0f, width = 307.24637f, height = 600f }, // in 100ths of an inch
                Xdpi = 138,
                Ydpi = 72
            },
            new PrinterSpec
            {
                DeviceName = "CognitiveTPG Receipt",
                PrintArea = new PrintBox { left = 15.841584f, top = 0f, width = 285.14853f, height = 3600f }, // in 100ths of an inch
                Xdpi = 202,
                Ydpi = 204
            },
            new PrinterSpec
            {
                DeviceName = "Microsoft Print to PDF",
                PaperWidth = 8.5f,
                PaperHeight = 11f,
                PrintArea = new PrintBox { left = 0f, top = 0f, width = 850f, height = 1100f }, // in 100ths of an inch
                Xdpi = 600,
                Ydpi = 600
            }
        };

        /// <summary>
        /// Gets a PrinterSpec for a named printer
        /// </summary>
        /// <returns>PrinterSpec object if found; otherwise, null.</returns>
        public static PrinterSpec? getPrinterSpec(string deviceName)
        {
            // Use FirstOrDefault for safety. It returns null if no match is found.
            return printers.FirstOrDefault(p => string.Equals(p.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// A utility class for printer-related operations.
    /// </summary>
    public static class PrinterUtils
    {
        public static void EnumeratePrinters()
        {
            Console.WriteLine("Enumerating available printers with details...");
            Console.WriteLine("=============================================");

            StringBuilder printerDetails = new StringBuilder();

            try
            {
                foreach (string printerName in PrinterSettings.InstalledPrinters)
                {
                    printerDetails.AppendLine($"Printer: {printerName}");
                    printerDetails.AppendLine("---------------------------------");

                    PrinterSettings settings = new PrinterSettings() { PrinterName = printerName };

                    printerDetails.AppendLine($"  Printable Area (1/100 in): {settings.DefaultPageSettings.PrintableArea}");
                    printerDetails.AppendLine($"  Default Resolution (DPI): {settings.DefaultPageSettings.PrinterResolution}");
                    printerDetails.AppendLine();
                }

                Console.WriteLine(printerDetails.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while retrieving printer details: {ex.Message}");
            }
        }
    }
}