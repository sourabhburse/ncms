using System;

namespace NCMS.Backend.Core.Dtos;

public record ProductResponse(
    Guid Id,
    Guid VendorId,
    string? VendorName,
    string ModelName,
    string Architecture,
    string ConfigFormat,
    string ConfigSchemaVersion,
    DateTime CreatedAt
);

public record CreateProductRequest(
    Guid VendorId,
    string ModelName,
    string Architecture,
    string ConfigFormat,
    string ConfigSchemaVersion
);

public record UpdateProductRequest(
    string ModelName,
    string Architecture,
    string ConfigFormat,
    string ConfigSchemaVersion
);
