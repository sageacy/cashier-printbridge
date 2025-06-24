using System.Threading.Tasks;

namespace TPGBridge
{
    public interface IPrintService
    {
        /// <summary>
        /// Merges a Handlebars template with data and prints the rendered HTML to a configured printer.
        /// </summary>
        Task RenderAndPrintHBS(string htmlTemplate, object data);
    }
}