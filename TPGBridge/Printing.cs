using System;
using System.Drawing.Printing;
using System.Text;
using System.Linq; // Added for .First() extension method

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

public void EnumeratePrinters()
{
    Console.WriteLine("Enumerating available printers with details...");
    Console.WriteLine("=============================================");

    // Use a StringBuilder for efficient string concatenation
    StringBuilder printerDetails = new StringBuilder();

    try
    {
        // Iterate through the collection of installed printer names.
        foreach (string printerName in PrinterSettings.InstalledPrinters)
        {
            printerDetails.AppendLine($"Printer: {printerName}");
            printerDetails.AppendLine("---------------------------------");

            PrinterSettings settings = new PrinterSettings();
            settings.PrinterName = printerName;

            // --- Get Default Margins ---
            // The Margins are measured in hundredths of an inch. Note: PageSettings.HardMargin is not directly accessible.
            // Instead, we can get the printable area.
            printerDetails.AppendLine("Printable Area (in hundredths of an inch):");
            printerDetails.AppendLine($"  Left:   {settings.DefaultPageSettings.PrintableArea.X}");
            printerDetails.AppendLine($"  Top:    {settings.DefaultPageSettings.PrintableArea.Y}");
            printerDetails.AppendLine($"  Width:  {settings.DefaultPageSettings.PrintableArea.Width}");
            printerDetails.AppendLine($"  Height: {settings.DefaultPageSettings.PrintableArea.Height}");
            printerDetails.AppendLine(); // Add an empty line for better readability

            // --- Get Default DPI ---
            printerDetails.AppendLine("Default Resolution (DPI):");
            var defaultResolution = settings.DefaultPageSettings.PrinterResolution;
            printerDetails.AppendLine($"  X (Horizontal): {defaultResolution.X}");
            printerDetails.AppendLine($"  Y (Vertical):   {defaultResolution.Y}");
            printerDetails.AppendLine($"  Kind:           {defaultResolution.Kind}");
            printerDetails.AppendLine();

            // --- Get Supported Resolutions ---
            printerDetails.AppendLine("Supported Resolutions:");
            if (settings.PrinterResolutions.Count > 0)
            {
                foreach (PrinterResolution resolution in settings.PrinterResolutions)
                {
                    printerDetails.AppendLine($"  - {resolution}"); // .ToString() is descriptive
                }
            }
            else
            {
                printerDetails.AppendLine("  (No resolutions reported)");
            }
            printerDetails.AppendLine();

            // --- Get Supported Paper Sizes ---
            printerDetails.AppendLine("Supported Paper Sizes:");
            if (settings.PaperSizes.Count > 0)
            {
                foreach (PaperSize paperSize in settings.PaperSizes)
                {
                    // Dimensions are in hundredths of an inch.
                    printerDetails.AppendLine($"  - {paperSize.PaperName}: {paperSize.Width / 100.0}\" x {paperSize.Height / 100.0}\"");
                }
            }
            else
            {
                printerDetails.AppendLine("  (No paper sizes reported)");
            }

            printerDetails.AppendLine("=============================================");
        }

        Console.WriteLine(printerDetails.ToString());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while retrieving printer details: {ex.Message}");
    }
}