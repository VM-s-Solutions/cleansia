using Cleansia.Core.Domain.Extensions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cleansia.Infra.Database.Converters;

public class PasswordConverter() : ValueConverter<string?, string?>(v => v == null ? null : v.HashAndSaltPassword(), v => v);