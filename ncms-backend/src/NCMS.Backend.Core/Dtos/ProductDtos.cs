using System;
using System.Collections.Generic;
using NCMS.Backend.Core.Entities;

namespace NCMS.Backend.Core.Dtos;

public record ProductResponse(
    Guid Id,
    Guid VendorId,
    string? VendorName,
    string ModelName,
    string Architecture,
    string ConfigFormat,
    string ConfigSchemaVersion,
    List<ProductIdentityPolicy> SupportedIdentityPolicies,
    DateTime CreatedAt
);

public record CreateProductRequest(
    Guid VendorId,
    string ModelName,
    string Architecture,
    string ConfigFormat,
    string ConfigSchemaVersion,
    List<ProductIdentityPolicy> SupportedIdentityPolicies
);

public record UpdateProductRequest(
    string ModelName,
    string Architecture,
    string ConfigFormat,
    string ConfigSchemaVersion,
    List<ProductIdentityPolicy> SupportedIdentityPolicies
);
