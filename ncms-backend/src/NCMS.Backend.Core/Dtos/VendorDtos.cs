using System;

namespace NCMS.Backend.Core.Dtos;

public record VendorResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt
);

public record CreateVendorRequest(
    string Name
);

public record UpdateVendorRequest(
    string Name
);
