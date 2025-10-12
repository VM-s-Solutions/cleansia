namespace Cleansia.Infra.Services.Templates;

public interface ITemplateEngine
{
    Task<string> CompileAsync<T>(string template, T data, CancellationToken cancellationToken = default);
}
