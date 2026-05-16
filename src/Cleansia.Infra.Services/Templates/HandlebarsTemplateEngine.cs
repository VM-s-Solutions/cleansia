using System.Net;
using HandlebarsDotNet;

namespace Cleansia.Infra.Services.Templates;

public class HandlebarsTemplateEngine : ITemplateEngine
{
    private readonly IHandlebars _handlebars;

    public HandlebarsTemplateEngine()
    {
        _handlebars = Handlebars.Create();
        RegisterHelpers();
    }

    public Task<string> CompileAsync<T>(string template, T data, CancellationToken cancellationToken = default)
    {
        var compiledTemplate = _handlebars.Compile(template);
        var result = compiledTemplate(data);
        return Task.FromResult(result);
    }

    private void RegisterHelpers()
    {
        _handlebars.RegisterHelper("formatCurrency", (writer, context, parameters) =>
        {
            if (parameters.Length < 2)
            {
                writer.WriteSafeString("0.00");
                return;
            }

            var amount = Convert.ToDecimal(parameters[0]);
            var currency = WebUtility.HtmlEncode(parameters[1]?.ToString() ?? "");
            writer.WriteSafeString($"{amount:N2} {currency}");
        });

        _handlebars.RegisterHelper("formatDate", (writer, context, parameters) =>
        {
            if (parameters.Length < 1)
            {
                writer.WriteSafeString("");
                return;
            }

            if (parameters[0] is DateTime date)
            {
                var format = parameters.Length > 1 ? parameters[1]?.ToString() ?? "dd.MM.yyyy" : "dd.MM.yyyy";
                writer.WriteSafeString(date.ToString(format));
            }
            else
            {
                writer.WriteSafeString(WebUtility.HtmlEncode(parameters[0]?.ToString() ?? ""));
            }
        });

        _handlebars.RegisterHelper("formatDateTime", (writer, context, parameters) =>
        {
            if (parameters.Length < 1)
            {
                writer.WriteSafeString("");
                return;
            }

            if (parameters[0] is DateTime dateTime)
            {
                var format = parameters.Length > 1 ? parameters[1]?.ToString() ?? "dd.MM.yyyy HH:mm" : "dd.MM.yyyy HH:mm";
                writer.WriteSafeString(dateTime.ToString(format));
            }
            else
            {
                writer.WriteSafeString(WebUtility.HtmlEncode(parameters[0]?.ToString() ?? ""));
            }
        });

        _handlebars.RegisterHelper("formatNumber", (writer, context, parameters) =>
        {
            if (parameters.Length < 1)
            {
                writer.WriteSafeString("0");
                return;
            }

            var number = Convert.ToDecimal(parameters[0]);
            var decimals = parameters.Length > 1 ? Convert.ToInt32(parameters[1]) : 2;
            writer.WriteSafeString(number.ToString($"N{decimals}"));
        });

        _handlebars.RegisterHelper("eq", (writer, context, parameters) =>
        {
            var result = parameters.Length >= 2 &&
                         string.Equals(parameters[0]?.ToString(), parameters[1]?.ToString(), StringComparison.OrdinalIgnoreCase);
            writer.WriteSafeString(result ? "true" : "");
        });

        _handlebars.RegisterHelper("add", (writer, context, parameters) =>
        {
            if (parameters.Length < 2)
            {
                writer.WriteSafeString("0");
                return;
            }

            var num1 = Convert.ToDecimal(parameters[0]);
            var num2 = Convert.ToDecimal(parameters[1]);
            writer.WriteSafeString((num1 + num2).ToString());
        });

        _handlebars.RegisterHelper("multiply", (writer, context, parameters) =>
        {
            if (parameters.Length < 2)
            {
                writer.WriteSafeString("0");
                return;
            }

            var num1 = Convert.ToDecimal(parameters[0]);
            var num2 = Convert.ToDecimal(parameters[1]);
            writer.WriteSafeString((num1 * num2).ToString());
        });
    }
}
