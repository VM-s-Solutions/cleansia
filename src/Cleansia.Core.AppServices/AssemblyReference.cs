using System.Reflection;

namespace Cleansia.Core.AppServices;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
