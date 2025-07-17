using System.Reflection;

namespace Cleansia.Infra.Database;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}