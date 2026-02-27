using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Nocturne.Infrastructure.Data.Interceptors;

/// <summary>
/// EF Core connection interceptor that sets the PostgreSQL session variable
/// for Row-Level Security tenant isolation.
///
/// On connection open: SET app.current_tenant_id = '{tenantId}'
/// On connection close: RESET app.current_tenant_id
///
/// This ensures RLS policies can enforce tenant isolation even if the
/// application-layer global query filters are bypassed.
/// </summary>
public class TenantConnectionInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is NocturneDbContext { TenantId: var tenantId } && tenantId != Guid.Empty)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET app.current_tenant_id = '{tenantId}'";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public override async ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        // Reset the session variable before the connection returns to the pool.
        // This prevents a stale tenant ID from leaking to the next request.
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "RESET app.current_tenant_id";
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Swallow errors during cleanup - the connection may already be broken
        }

        return result;
    }
}
