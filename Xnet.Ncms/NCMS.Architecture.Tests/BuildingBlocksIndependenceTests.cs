using System.Reflection;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence.Pagination;
using NCMS.Shared.Domain;
using NetArchTest.Rules;
using Xunit;

namespace NCMS.Architecture.Tests;

/// <summary>
/// Enforces that the BuildingBlocks projects (NCMS.Shared, NCMS.Persistence) stay
/// independent of any module. Regression coverage for a mistake made and fixed earlier in
/// this project's history: EF Core was briefly added directly to NCMS.Shared to support a
/// generic pagination helper, before that helper was relocated into NCMS.Persistence — a
/// dedicated EF-Core-specific BuildingBlock, kept separate from the framework-agnostic
/// NCMS.Shared kernel. NCMS.Persistence itself is intentionally Postgres-aware (via
/// AddNcmsDbContext) since this solution is single-provider by design — see
/// Persistence_Should_Not_Depend_On_A_Second_Database_Provider below.
/// </summary>
public class BuildingBlocksIndependenceTests
{
    private static readonly Assembly SharedAssembly = typeof(BaseEntity<>).Assembly;
    private static readonly Assembly PersistenceAssembly = typeof(IPagedQuery).Assembly;

    [Fact]
    public void Shared_Should_Not_Depend_On_EntityFrameworkCore()
    {
        var result = Types.InAssembly(SharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NCMS.Shared must stay EF-Core-free — persistence helpers belong in the module " +
            $"that owns a DbContext, or in a dedicated NCMS.Persistence once a second module " +
            $"needs one. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Shared_Should_Not_Depend_On_DeviceManagement()
    {
        var result = Types.InAssembly(SharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("NCMS.IoT.DeviceManagement")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NCMS.Shared is the shared kernel — it must never depend on a module. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Shared_Should_Not_Depend_On_Identity()
    {
        var result = Types.InAssembly(SharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("NCMS.IoT.Identity")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NCMS.Shared is the shared kernel — it must never depend on a module. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Persistence_Should_Not_Depend_On_DeviceManagement()
    {
        var result = Types.InAssembly(PersistenceAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("NCMS.IoT.DeviceManagement")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NCMS.Persistence is a BuildingBlock — it must stay reusable by any future " +
            $"module, never coupled to DeviceManagement specifically. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Persistence_Should_Not_Depend_On_Identity()
    {
        var result = Types.InAssembly(PersistenceAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("NCMS.IoT.Identity")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NCMS.Persistence is a BuildingBlock — it must stay reusable by any future " +
            $"module, never coupled to Identity specifically. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Persistence_Should_Not_Depend_On_A_Second_Database_Provider()
    {
        // NCMS.Persistence deliberately bakes in Postgres (AddNcmsDbContext) rather than
        // staying provider-agnostic — this solution is single-provider by design, and every
        // module shares the same database. This guards against silently growing multi-provider
        // support (e.g. SqlServer) without a deliberate architectural decision to revisit.
        var result = Types.InAssembly(PersistenceAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore.SqlServer")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NCMS.Persistence is single-provider (PostgreSQL) by design. " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void DeviceManagement_Entities_Should_Not_Depend_On_AspNetCore()
    {
        // Entities are the domain model — they should stay persistence/transport-agnostic
        // even though the module as a whole (handlers, endpoints) is framework-specific.
        var result = Types.InAssembly(typeof(Device).Assembly)
            .That()
            .ResideInNamespace("NCMS.IoT.DeviceManagement.Entities")
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Entities must stay framework-agnostic. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
