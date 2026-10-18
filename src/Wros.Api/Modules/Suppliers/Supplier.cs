using System.ComponentModel.DataAnnotations;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Suppliers;
public sealed class Supplier : MasterRecord
{
    [MaxLength(200)] public string? ContactPerson { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(254)] public string? Email { get; set; }
    [MaxLength(2000)] public string? Address { get; set; }
    [MaxLength(2000)] public string? Remarks { get; set; }
}
