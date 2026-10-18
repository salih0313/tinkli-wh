using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Warehouses;
public sealed class Warehouse : MasterRecord;
public sealed class WarehouseArea : MasterRecord
{
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
}
public sealed class StorageRack : MasterRecord
{
    public Guid AreaId { get; set; }
    public WarehouseArea Area { get; set; } = null!;
}
public sealed class StorageLocation : MasterRecord
{
    public Guid RackId { get; set; }
    public StorageRack Rack { get; set; } = null!;
    // A rack can itself be a placement (no bin/drawer required); optional positions are children of it.
}
