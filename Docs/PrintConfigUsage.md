# PrintConfig Usage Examples

The new unified `PrintConfig` class provides centralized configuration for both `EdgePrintService` and `PuppeteerPrintService`, including which implementation to use by default.

## Print Service Selection

```csharp
using TPGBridge;

// Configure which print service to use by default
PrintConfig.DefaultPrintService = PrintServiceType.Edge;       // Use EdgePrintService
PrintConfig.DefaultPrintService = PrintServiceType.Puppeteer;  // Use PuppeteerPrintService

// Create print service using the default configuration
var printService = PrintConfig.CreatePrintService("PDF Check");

// Or explicitly specify the service type
var edgeService = PrintConfig.CreatePrintService("PDF Check", PrintServiceType.Edge);
var puppeteerService = PrintConfig.CreatePrintService("PDF Check", PrintServiceType.Puppeteer);
```

## Basic Configuration

```csharp
using TPGBridge;

// Configure the default print service
PrintConfig.DefaultPrintService = PrintServiceType.Edge;

// Configure debugging - keep temporary files for inspection
PrintConfig.KeepAllTempFiles = true;

// Configure production - clean up temporary files
PrintConfig.KeepAllTempFiles = false;

// Or configure each type individually
PrintConfig.KeepTempPDFs = true;      // Keep PDFs for debugging PuppeteerPrintService
PrintConfig.KeepTempHtmlFiles = false; // Clean up HTML files from EdgePrintService
```

## Directory Configuration

```csharp
// Set a common directory for all temporary files
PrintConfig.TempDir = @"C:\PrintTemp";

// Or set directories individually
PrintConfig.TempPDFDir = @"C:\PrintTemp\PDFs";
PrintConfig.TempHtmlDir = @"C:\PrintTemp\HTML";
```

## Service Usage with Configuration

```csharp
// Configure before creating services
PrintConfig.DefaultPrintService = PrintServiceType.Edge;  // Use Edge by default
PrintConfig.KeepAllTempFiles = false; // Production mode
PrintConfig.TempDir = @"C:\PrintTemp";

// Create service using factory method (will use Edge based on configuration)
var printService = PrintConfig.CreatePrintService("PDF Check");

// Print using the configured service
await printService.RenderAndPrintHBS(template, data);

// Or create specific service types when needed
var edgePrintService = PrintConfig.CreatePrintService("PDF Check", PrintServiceType.Edge);
var puppeteerPrintService = PrintConfig.CreatePrintService("PDF Check", PrintServiceType.Puppeteer);
```

## Debug Mode Setup

```csharp
// Enable debug mode to keep all temporary files
PrintConfig.KeepAllTempFiles = true;
PrintConfig.TempDir = @"C:\Debug\PrintFiles";

// Now you can inspect the generated files:
// - HTML files in C:\Debug\PrintFiles (from EdgePrintService)
// - PDF files in C:\Debug\PrintFiles (from PuppeteerPrintService)
```

## Advanced Configuration

```csharp
// Fine-grained control for different scenarios
if (isDevelopment)
{
    PrintConfig.DefaultPrintService = PrintServiceType.Edge;  // Faster for development
    PrintConfig.KeepTempPDFs = true;        // Keep PDFs to debug layout issues
    PrintConfig.KeepTempHtmlFiles = true;   // Keep HTML to debug template rendering
    PrintConfig.TempDir = @"C:\Dev\PrintDebug";
}
else
{
    PrintConfig.DefaultPrintService = PrintServiceType.Puppeteer;  // More reliable for production
    PrintConfig.KeepAllTempFiles = false;   // Clean up in production
    PrintConfig.TempDir = Path.GetTempPath(); // Use system temp directory
}

// Create service based on environment configuration
var printService = PrintConfig.CreatePrintService("Production Printer");
```

## Configuration Properties

| Property | Type | Description |
|----------|------|-------------|
| `DefaultPrintService` | `PrintServiceType` | Default print service implementation (Edge or Puppeteer) |
| `KeepTempPDFs` | `bool` | Keep temporary PDF files (used by PuppeteerPrintService) |
| `KeepTempHtmlFiles` | `bool` | Keep temporary HTML files (used by EdgePrintService) |
| `KeepAllTempFiles` | `bool` | Convenience property to set both keep flags |
| `TempPDFDir` | `string` | Directory for temporary PDF files |
| `TempHtmlDir` | `string` | Directory for temporary HTML files |
| `TempDir` | `string` | Convenience property to set both directories |

## Factory Methods

| Method | Description |
|--------|-------------|
| `CreatePrintService(printerName)` | Creates a print service using the default configured type |
| `CreatePrintService(printerName, serviceType)` | Creates a print service of the specified type |
