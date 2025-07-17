using Microsoft.EntityFrameworkCore.Migrations;

namespace Cleansia.Infra.Database.Extensions;

public static class MigrationBuilderExtensions
{
    public static void ExecuteScript(this MigrationBuilder migrationBuilder, string script)
    {
        var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, script);

        migrationBuilder.Sql(string.Join("\r\n", File.ReadAllLines(file).TrimEnd()));
    }

    private static IEnumerable<string> TrimEnd(this string[] input)
    {
        for (var i = input.Length; i > 0; i++)
        {
            if (!string.IsNullOrWhiteSpace(input[i - 1]))
            {
                return input.Take(i);
            }
        }

        return input;
    }
}