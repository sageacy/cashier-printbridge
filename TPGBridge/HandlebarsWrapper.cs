using HandlebarsDotNet;

namespace TPGBridge
{
    public static class HandlebarsWrapper
    {
        /// <summary>
        /// Renders a Handlebars template with the provided data.
        /// </summary>
        /// <param name="templateString">The Handlebars template string.</param>
        /// <param name="data">The data object to bind to the template.</param>
        /// <returns>The rendered string output.</returns>
        public static string Render(string templateString, object data)
        {
            var template = Handlebars.Compile(templateString);
            return template(data);
        }
    }
}