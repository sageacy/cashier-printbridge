# Print Service Implementations

This project contains two implementations of the `IPrintService` interface for different printing approaches:

## PuppeteerPrintService
- Uses PuppeteerSharp to generate PDF files from HTML
- Requires PuppeteerSharp NuGet package
- Generates temporary PDF files that are then sent to the printer using PDFtoPrinter
- Good for complex layouts and precise PDF generation

## EdgePrintService
- Uses Microsoft Edge directly for printing HTML content
- No additional NuGet packages required beyond what's already in the project
- Prints HTML directly without generating intermediate PDF files
- Lighter weight approach that leverages the system's Edge browser

## Usage Examples

### Using EdgePrintService

```csharp
// Create an instance with a printer name
var printService = new EdgePrintService("PDF Check");

// Print a Handlebars template
string hbsTemplate = @"
<h1>Hello {{name}}</h1>
<p>Today is {{date}}</p>
";

var data = new 
{
    name = "John Doe",
    date = DateTime.Now.ToString("yyyy-MM-dd")
};

await printService.RenderAndPrintHBS(hbsTemplate, data);
```

### Using PuppeteerPrintService

```csharp
// Create an instance with a printer name
var printService = new PuppeteerPrintService("PDF Check");

// Print the same template
await printService.RenderAndPrintHBS(hbsTemplate, data);
```

### Switching Between Services

You can now easily switch between implementations using the unified configuration:

```csharp
// Configure globally which service to use
PrintConfig.DefaultPrintService = PrintServiceType.Edge;

// Create service using factory method (will use Edge)
IPrintService printer = PrintConfig.CreatePrintService(printRequest.printer);

// Or create a specific service type
IPrintService edgePrinter = PrintConfig.CreatePrintService(printRequest.printer, PrintServiceType.Edge);
IPrintService puppeteerPrinter = PrintConfig.CreatePrintService(printRequest.printer, PrintServiceType.Puppeteer);
```

In the WebSocketHandler, the service creation is now centralized:

```csharp
IPrintService? printer = null;
if (!string.IsNullOrWhiteSpace(printRequest.printer)) 
{
    // Uses the configured default service (Edge or Puppeteer)
    printer = PrintConfig.CreatePrintService(printRequest.printer);
}
```

## Configuration

Both print services now use a unified `PrintConfig` class for configuration:

```csharp
// Configure temporary file handling for both services
PrintConfig.KeepTempPDFs = false;        // Keep PDF files (PuppeteerPrintService)
PrintConfig.KeepTempHtmlFiles = false;   // Keep HTML files (EdgePrintService)
PrintConfig.KeepAllTempFiles = false;    // Convenience: keep all temp files

// Configure temporary directories
PrintConfig.TempPDFDir = @"C:\Temp\PrintFiles";   // PDF temp directory
PrintConfig.TempHtmlDir = @"C:\Temp\PrintFiles";  // HTML temp directory  
PrintConfig.TempDir = @"C:\Temp\PrintFiles";      // Convenience: set both directories
```

## Key Differences

| Feature | EdgePrintService | PuppeteerPrintService |
|---------|------------------|----------------------|
| Dependencies | None (uses system Edge) | PuppeteerSharp |
| Temp Files | HTML files | PDF files |
| Print Method | Direct HTML printing | PDF generation + printing |
| Performance | Faster (no PDF generation) | Slower (PDF generation step) |
| Layout Control | Good (browser-based) | Excellent (PDF precision) |
| Debugging | HTML files viewable in browser | PDF files viewable in PDF reader |

## Printer Support

Both services support the same printer specifications defined in `PrinterSpec`:
- A776 Check (CognitiveTPG Narrow Slip)
- A776WS Receipt (CognitiveTPG Receipt)  
- PDF Check (Microsoft Print to PDF)

The services automatically configure print settings based on the printer specifications including paper size, margins, and DPI settings.
