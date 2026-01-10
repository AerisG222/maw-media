using System.Data;
using System.Runtime.CompilerServices;
using Dapper;
using NodaTime;

[assembly: InternalsVisibleTo("MawMedia.Services.Tests")]

namespace MawMedia.Services.Models;

class LocalDateHandler
    : SqlMapper.TypeHandler<LocalDate>
{
    public static readonly LocalDateHandler Default = new LocalDateHandler();

    private LocalDateHandler() { }

    public override void SetValue(IDbDataParameter parameter, LocalDate value)
    {
        parameter.Value = value;
    }

    // Npgsql already provides the correct typed value
    public override LocalDate Parse(object value)
    {
        return (LocalDate)value;
    }
}
