using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Mundialito.Infrastructure.Queries.Common;

/// <summary>
/// Conexiones para el lado Query (Dapper). No pasa por EF Core ni por el UnitOfWork (§3): solo lee.
/// </summary>
internal sealed class DbConnectionFactory(string connectionString)
{
    /// <summary>Cada consulta abre y cierra la suya; el pool de ADO.NET hace que sea barato.</summary>
    public DbConnection Create() => new SqlConnection(connectionString);
}
