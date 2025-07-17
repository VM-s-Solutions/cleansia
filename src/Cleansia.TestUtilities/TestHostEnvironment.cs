using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Cleansia.TestUtilities;

public class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "TestApp";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetCurrentDirectory());
}