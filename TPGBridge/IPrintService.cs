using System.Threading.Tasks;

namespace TPGBridge
{
    public interface IPrintService
    {
        /// <summary>
        /// Renders HTML content and sends it to the configured printer.
        /// </summary>
        Task RenderAndPrintHTML(string html);
    }
}