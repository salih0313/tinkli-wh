using System.ComponentModel.DataAnnotations;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Catalog;
public sealed class Product : MasterRecord
{
    // Code is the canonical SKU; product names never identify stock or external products.
    [MaxLength(100)] public string? Barcode { get; set; }
    [MaxLength(200)] public string? Brand { get; set; }
    public bool TrackExpiry { get; set; }
    [MaxLength(100)] public string? ExternalProductId { get; set; }
}
