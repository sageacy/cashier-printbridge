using System;
using System.Drawing.Printing;
using System.Text;

public class PrinterDetailsEnumerator
{
    // public static void Main(string[] args)
    // {
    //     Console.WriteLine("Enumerating available printers with details...");
    //     Console.WriteLine("=============================================");

    //     // Use a StringBuilder for efficient string concatenation
    //     StringBuilder printerDetails = new StringBuilder();

    //     try
    //     {
    //         // Iterate through the collection of installed printer names.
    //         foreach (string printerName in PrinterSettings.InstalledPrinters)
    //         {
    //             printerDetails.AppendLine($"Printer: {printerName}");
    //             printerDetails.AppendLine("---------------------------------");

    //             PrinterSettings settings = new PrinterSettings();
    //             settings.PrinterName = printerName;

    //             // --- Get Default Margins ---
    //             // The Margins are measured in hundredths of an inch. Note: PageSettings.HardMargin is not directly accessible.
    //             // Instead, we can get the printable area.
    //             printerDetails.AppendLine("Printable Area (in hundredths of an inch):");
    //             printerDetails.AppendLine($"  Left:   {settings.DefaultPageSettings.PrintableArea.X}");
    //             printerDetails.AppendLine($"  Top:    {settings.DefaultPageSettings.PrintableArea.Y}");
    //             printerDetails.AppendLine($"  Width:  {settings.DefaultPageSettings.PrintableArea.Width}");
    //             printerDetails.AppendLine($"  Height: {settings.DefaultPageSettings.PrintableArea.Height}");
    //             printerDetails.AppendLine(); // Add an empty line for better readability

    //             // --- Get Default DPI ---
    //             printerDetails.AppendLine("Default Resolution (DPI):");
    //             var defaultResolution = settings.DefaultPageSettings.PrinterResolution;
    //             printerDetails.AppendLine($"  X (Horizontal): {defaultResolution.X}");
    //             printerDetails.AppendLine($"  Y (Vertical):   {defaultResolution.Y}");
    //             printerDetails.AppendLine($"  Kind:           {defaultResolution.Kind}");
    //             printerDetails.AppendLine();

    //             // --- Get Supported Resolutions ---
    //             printerDetails.AppendLine("Supported Resolutions:");
    //             if (settings.PrinterResolutions.Count > 0)
    //             {
    //                 foreach (PrinterResolution resolution in settings.PrinterResolutions)
    //                 {
    //                     printerDetails.AppendLine($"  - {resolution}"); // .ToString() is descriptive
    //                 }
    //             }
    //             else
    //             {
    //                 printerDetails.AppendLine("  (No resolutions reported)");
    //             }
    //             printerDetails.AppendLine();

    //             // --- Get Supported Paper Sizes ---
    //             printerDetails.AppendLine("Supported Paper Sizes:");
    //             if (settings.PaperSizes.Count > 0)
    //             {
    //                 foreach (PaperSize paperSize in settings.PaperSizes)
    //                 {
    //                     // Dimensions are in hundredths of an inch.
    //                     printerDetails.AppendLine($"  - {paperSize.PaperName}: {paperSize.Width / 100.0}\" x {paperSize.Height / 100.0}\"");
    //                 }
    //             }
    //             else
    //             {
    //                 printerDetails.AppendLine("  (No paper sizes reported)");
    //             }

    //             printerDetails.AppendLine("=============================================");
    //         }

    //         Console.WriteLine(printerDetails.ToString());
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"An error occurred while retrieving printer details: {ex.Message}");
    //     }
    // }
}