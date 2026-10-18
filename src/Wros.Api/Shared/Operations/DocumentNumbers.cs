using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Shared.Operations;
public sealed class DocumentCounter
{
    [MaxLength(16)] public string Key { get; set; } = "";
    public long LastNumber { get; set; }
}
public sealed class DocumentNumbers(WrosDbContext db)
{
    // Caller owns the transaction. Update/range locks serialize the year's first and subsequent numbers.
    public async Task<string> Next(string prefix)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Document numbering requires a transaction.");
        var year = DateTimeOffset.UtcNow.Year;
        var key = $"{prefix}:{year}";
        var counter = await db.DocumentCounters.FromSqlInterpolated($"SELECT * FROM [DocumentCounters] WITH (UPDLOCK, HOLDLOCK) WHERE [Key]={key}").SingleOrDefaultAsync();
        if (counter is null) { counter = new DocumentCounter { Key = key }; db.DocumentCounters.Add(counter); }
        counter.LastNumber = checked(counter.LastNumber + 1);
        await db.SaveChangesAsync();
        return $"{prefix}-{year}-{counter.LastNumber:D6}";
    }
}
