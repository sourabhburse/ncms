using System.Reflection;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.IoT.Identity.Entities;
using NCMS.Shared.Domain;
using NetArchTest.Rules;
using Xunit;

namespace NCMS.Architecture.Tests;

/// <summary>
/// Enforces that composition roots (Api, Host) are only ever depended on going one direction:
/// they wire modules together, nothing should ever need to reference back into them.
/// </summary>
public class CompositionRootIsolationTests
{
    private static readonly Assembly[] NonHostAssemblies =
    [
        typeof(BaseEntity<>).Assembly,  // NCMS.Shared
        typeof(Device).Assembly,        // NCMS.IoT.DeviceManagement
        typeof(AppUser).Assembly,       // NCMS.IoT.Identity
    ];

    [Fact]
    public void Nothing_Outside_Host_Should_Depend_On_Api_Or_Host()
    {
        foreach (var assembly in NonHostAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny("NCMS.IoT.Api", "NCMS.IoT.Host")
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"'{assembly.GetName().Name}' must not depend on a composition root. " +
                $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }
}
