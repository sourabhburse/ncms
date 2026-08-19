namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

// Cascading filter source for the product hierarchy dropdowns used across index pages:
//   Product Series (ProductCategory)  ->  Product Type (ProductType)  ->  Product Model (Product)
// Types carry their CategoryId and Models their TypeId so the UI can narrow each dependent
// dropdown client-side without additional round-trips.

public sealed record ProductFilterOptionsDto(
    IReadOnlyList<ProductCategoryOptionDto> Categories,
    IReadOnlyList<ProductTypeOptionDto> Types,
    IReadOnlyList<ProductModelOptionDto> Models);

public sealed record ProductCategoryOptionDto(Guid Id, string Name);

public sealed record ProductTypeOptionDto(Guid Id, string Name, Guid CategoryId);

public sealed record ProductModelOptionDto(Guid Id, string Name, Guid TypeId);
