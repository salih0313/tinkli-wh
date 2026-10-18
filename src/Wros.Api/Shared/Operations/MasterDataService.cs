using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Audit;
using Wros.Api.Modules.Catalog;
using Wros.Api.Modules.Suppliers;
using Wros.Api.Modules.Warehouses;
using Wros.Api.Modules.Identity;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Shared.Operations;

public sealed record MasterRequest([Required, MaxLength(64)] string Code, [Required, MaxLength(200)] string Name,
    string? Description, bool IsActive, string? RowVersion, Guid? ParentId = null,
    string? ContactPerson = null, string? Phone = null, [EmailAddress] string? Email = null,
    string? Address = null, string? Remarks = null, string? Barcode = null, string? Brand = null,
    bool TrackExpiry = false, string? ExternalProductId = null);
public sealed record MasterDto(Guid Id, string Code, string Name, string? Description, bool IsActive,
    string RowVersion, Guid? ParentId, string? ParentPath, DateTimeOffset CreatedAt,
    string? ContactPerson, string? Phone, string? Email, string? Address, string? Remarks,
    string? Barcode, string? Brand, bool TrackExpiry, string? ExternalProductId);

// Shared maintenance for the six master lists, directly using EF Core (no repository layer).
public sealed class MasterDataService(WrosDbContext db, AuditService audit)
{
    public async Task<PageDto<MasterDto>> List<T>(int page, int pageSize, string? search, bool? active, Guid? parentId) where T : MasterRecord
    {
        var query = Query<T>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = OperationRules.Required(search, 100, "Search");
            query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
        }
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        if (parentId.HasValue)
        {
            if (typeof(T) == typeof(WarehouseArea)) query = query.Where(x => EF.Property<Guid>(x, "WarehouseId") == parentId);
            else if (typeof(T) == typeof(StorageRack)) query = query.Where(x => EF.Property<Guid>(x, "AreaId") == parentId);
            else if (typeof(T) == typeof(StorageLocation)) query = query.Where(x => EF.Property<Guid>(x, "RackId") == parentId);
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new(items.Select(ToDto).ToArray(), page, pageSize, total);
    }
    public async Task<MasterDto> Get<T>(Guid id) where T : MasterRecord => ToDto(await Query<T>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiProblemException(404, "Record not found."));
    public async Task<MasterDto> Save<T>(Guid? id, MasterRequest request) where T : MasterRecord, new()
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var record = id.HasValue ? await Query<T>().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Record not found.") : new T();
        if (id.HasValue) OperationRules.CheckVersion(record, request.RowVersion);
        var before = id.HasValue ? ToDto(record) : null;
        var code = OperationRules.Required(request.Code, 64, "Code / SKU").ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Z0-9][A-Z0-9._/\-]*$"))
            throw new ApiProblemException(400, "Codes must start with a letter or digit and use only letters, digits, dots, slashes, underscores or hyphens.");
        record.Code = code; record.Name = OperationRules.Required(request.Name, 200, "Name");
        record.Description = OperationRules.Optional(request.Description, 2000, "Description"); record.IsActive = request.IsActive;
        if (record is Supplier supplier)
        {
            supplier.ContactPerson = OperationRules.Optional(request.ContactPerson, 200, "Contact person");
            supplier.Phone = OperationRules.Optional(request.Phone, 50, "Phone"); supplier.Email = OperationRules.Optional(request.Email, 254, "Email");
            supplier.Address = OperationRules.Optional(request.Address, 2000, "Address"); supplier.Remarks = OperationRules.Optional(request.Remarks, 2000, "Remarks");
        }
        if (record is Product product)
        {
            product.Barcode = OperationRules.Optional(request.Barcode, 100, "Barcode"); product.Brand = OperationRules.Optional(request.Brand, 200, "Brand");
            product.TrackExpiry = request.TrackExpiry; product.ExternalProductId = OperationRules.Optional(request.ExternalProductId, 100, "External product ID");
        }
        var parent = request.ParentId ?? Guid.Empty;
        if (record is WarehouseArea area)
        {
            if (!await db.Warehouses.AnyAsync(x => x.Id == parent)) throw new ApiProblemException(400, "Choose an existing warehouse.");
            if (id.HasValue && area.WarehouseId != parent) throw new ApiProblemException(400, "An area's warehouse cannot be changed. Create a new area instead.");
            area.WarehouseId = parent;
        }
        if (record is StorageRack rack)
        {
            if (!await db.WarehouseAreas.AnyAsync(x => x.Id == parent)) throw new ApiProblemException(400, "Choose an existing area.");
            if (id.HasValue && rack.AreaId != parent) throw new ApiProblemException(400, "A rack's area cannot be changed. Create a new rack instead.");
            rack.AreaId = parent;
        }
        if (record is StorageLocation location)
        {
            if (!await db.StorageRacks.AnyAsync(x => x.Id == parent)) throw new ApiProblemException(400, "Choose an existing rack.");
            if (id.HasValue && location.RackId != parent) throw new ApiProblemException(400, "A location's rack cannot be changed. Create a new location instead.");
            location.RackId = parent;
        }
        if (before?.IsActive == true && !record.IsActive && record is Warehouse or WarehouseArea or StorageRack or StorageLocation)
        {
            var occupied = db.InventoryBalances.Where(x => x.Quantity > 0);
            if (record is Warehouse) occupied = occupied.Where(x => x.WarehouseId == record.Id);
            if (record is WarehouseArea) occupied = occupied.Where(x => x.Rack.AreaId == record.Id);
            if (record is StorageRack) occupied = occupied.Where(x => x.RackId == record.Id);
            if (record is StorageLocation) occupied = occupied.Where(x => x.LocationId == record.Id);
            if (await occupied.AnyAsync()) throw new ApiProblemException(409, "This location hierarchy holds stock and cannot be deactivated.");
        }
        IQueryable<T> duplicate = db.Set<T>().Where(x => x.Code == record.Code && x.Id != record.Id);
        if (record is WarehouseArea) duplicate = duplicate.Where(x => EF.Property<Guid>(x, "WarehouseId") == parent);
        if (record is StorageRack) duplicate = duplicate.Where(x => EF.Property<Guid>(x, "AreaId") == parent);
        if (record is StorageLocation) duplicate = duplicate.Where(x => EF.Property<Guid>(x, "RackId") == parent);
        if (record is Product mappedProduct && mappedProduct.ExternalProductId is not null && await db.Products.AnyAsync(x => x.Id != record.Id && x.ExternalProductId == mappedProduct.ExternalProductId))
            throw new ApiProblemException(409, "That external product ID is already linked to another product.");
        if (await duplicate.AnyAsync()) throw new ApiProblemException(409, "That code / SKU is already in use within this scope.");
        record.UpdatedAt = DateTimeOffset.UtcNow; record.UpdatedBy = audit.ActorId;
        if (!id.HasValue) { record.CreatedBy = audit.ActorId; db.Set<T>().Add(record); }
        // Only non-contact reference fields are audited; supplier contact PII is deliberately omitted.
        audit.Record(id.HasValue ? "MasterDataUpdated" : "MasterDataCreated", typeof(T).Name, record.Id.ToString(),
            before is null ? null : new { before.Code, before.Name, before.IsActive, before.ParentId, before.TrackExpiry, before.Barcode, before.Brand, before.ExternalProductId },
            new { record.Code, record.Name, record.IsActive, ParentId = request.ParentId, request.TrackExpiry, request.Barcode, request.Brand, request.ExternalProductId });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return await Get<T>(record.Id);
    }
    private IQueryable<T> Query<T>() where T : MasterRecord
    {
        IQueryable<T> query = db.Set<T>();
        if (typeof(T) == typeof(WarehouseArea)) query = query.Include("Warehouse");
        if (typeof(T) == typeof(StorageRack)) query = query.Include("Area.Warehouse");
        if (typeof(T) == typeof(StorageLocation)) query = query.Include("Rack.Area.Warehouse");
        return query;
    }
    private static MasterDto ToDto(MasterRecord x)
    {
        var supplier = x as Supplier; var product = x as Product;
        Guid? parent = x switch { WarehouseArea a => a.WarehouseId, StorageRack r => r.AreaId, StorageLocation l => l.RackId, _ => null };
        string? path = x switch { WarehouseArea a when a.Warehouse is not null => a.Warehouse.Code,
            StorageRack r when r.Area?.Warehouse is not null => $"{r.Area.Warehouse.Code} / {r.Area.Code}",
            StorageLocation l when l.Rack?.Area?.Warehouse is not null => $"{l.Rack.Area.Warehouse.Code} / {l.Rack.Area.Code} / {l.Rack.Code}", _ => null };
        return new(x.Id, x.Code, x.Name, x.Description, x.IsActive, Convert.ToBase64String(x.RowVersion), parent, path, x.CreatedAt,
            supplier?.ContactPerson, supplier?.Phone, supplier?.Email, supplier?.Address, supplier?.Remarks,
            product?.Barcode, product?.Brand, product?.TrackExpiry ?? false, product?.ExternalProductId);
    }
}
