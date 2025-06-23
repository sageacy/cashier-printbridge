using System;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.ComponentModel; // Added for Win32Exception
using System.Threading;
using System.Windows.Forms;

public class Program
{
    public static void Main(string[] args)
    {
        // --- Define the Handlebars HTML Template ---
        string htmlTemplate = @"
            <!DOCTYPE html>
            <html>
            <head>
                <title>Invoice {{InvoiceNumber}}</title>
                <style>
                    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #333; }
                    .invoice-box { max-width: 800px; margin: auto; padding: 30px; border: 1px solid #eee; box-shadow: 0 0 10px rgba(0, 0, 0, .15); }
                    .header { text-align: center; margin-bottom: 20px; }
                    .item-table { width: 100%; border-collapse: collapse; }
                    .item-table th, .item-table td { border-bottom: 1px solid #eee; padding: 8px; text-align: left; }
                    .item-table th { background-color: #f7f7f7; }
                    .total-row { font-weight: bold; }
                    .total { text-align: right; }
                </style>
            </head>
            <body>
                <div class='invoice-box'>
                    <div class='header'>
                        <h2>INVOICE</h2>
                    </div>
                    <p>
                        <strong>Invoice #:</strong> {{InvoiceNumber}}<br>
                        <strong>Date:</strong> {{Date}}<br>
                        <strong>Billed To:</strong> {{Customer.Name}}
                    </p>
                    <table class='item-table'>
                        <thead>
                            <tr>
                                <th>Item Description</th>
                                <th>Quantity</th>
                                <th>Unit Price</th>
                                <th>Total</th>
                            </tr>
                        </thead>
                        <tbody>
                            {{#each Items}}
                            <tr>
                                <td>{{this.Name}}</td>
                                <td>{{this.Quantity}}</td>
                                <td>{{this.Price:C}}</td>
                                <td>{{@custom.TotalLine this.Quantity this.Price:C}}</td>
                            </tr>
                            {{/each}}
                            <tr class='total-row'>
                                <td colspan='3' class='total'>Grand Total</td>
                                <td class='total'>{{Total:C}}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </body>
            </html>";

        // --- Create the Data Object ---
        var invoiceData = new
        {
            InvoiceNumber = "INV-2025-001",
            Date = DateTime.Now.ToString("MMMM dd, yyyy"),
            Customer = new { Name = "ACME Corporation" },
            Items = new[]
            {
                new { Name = "Pro Widget", Quantity = 2, Price = 150.00 },
                new { Name = "Standard Gizmo", Quantity = 10, Price = 15.50 },
                new { Name = "Accessory Kit", Quantity = 1, Price = 45.25 }
            },
            Total = (2 * 150.00) + (10 * 15.50) + 45.25
        };

        // --- identify the target printer
        var printer = new RenderAndPrintHTML("PDF Check");
        try
        {
            // --- render the html and print it to the atarget printer
            printer.RenderAndPrint(htmlTemplate, invoiceData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: Could not complete print job. {ex.Message}");
        }

        // Console.WriteLine("\nPress any key to exit...");
        // Console.ReadKey();
    }
}