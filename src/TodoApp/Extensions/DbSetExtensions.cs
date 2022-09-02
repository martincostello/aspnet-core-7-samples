namespace Microsoft.EntityFrameworkCore;

public static class DbSetExtensions
{
    public static ValueTask<TEntity?> FindItemAsync<TEntity, TKey>(
        this DbSet<TEntity> set,
        TKey keyValue,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(keyValue);
        return set.FindAsync(new object[] { keyValue }, cancellationToken);
    }
}
