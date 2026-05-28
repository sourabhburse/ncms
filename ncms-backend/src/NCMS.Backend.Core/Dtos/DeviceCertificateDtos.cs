using System;

namespace NCMS.Backend.Core.Dtos;

public record DeviceCertificateResponse(
    Guid Id,
    Guid DeviceId,
    string Thumbprint,
    string SubjectName,
    DateTime ExpiresAt,
    bool IsActive,
    DateTime IssuedAt
);
