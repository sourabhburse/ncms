namespace NCMS.IoT.Identity.Contracts.Dtos;

public sealed record UserDto(
    Guid Id,
    string UserName,
    string Email,
    string? FirstName,
    string? LastName,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed record CreateUserRequest(
    string UserName,
    string Email,
    string Password,
    string? FirstName,
    string? LastName,
    IReadOnlyList<string>? Roles);

public sealed record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    bool? IsActive,
    IReadOnlyList<string>? Roles);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
