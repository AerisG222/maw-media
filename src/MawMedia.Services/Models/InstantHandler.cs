using System.Data;
using System.Runtime.CompilerServices;
using Dapper;
using NodaTime;

[assembly: InternalsVisibleToAttribute("MawMedia.Services.Tests")]

namespace MawMedia.Services.Models;

// https://github.com/mattjohnsonpint/Dapper-NodaTime/blob/master/src/Dapper.NodaTime/InstantHandler.cs
class InstantHandler
    : SqlMapper.TypeHandler<Instant>
{
    public static readonly InstantHandler Default = new();

    private InstantHandler() { }

    public override void SetValue(IDbDataParameter parameter, Instant value)
    {
        parameter.Value = value;
    }

    // This is not necessary since Npgsql alredy provide the correct typed value
    public override Instant Parse(object value)
    {
        return (Instant)value;
    }
}
