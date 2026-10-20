using System.Reflection;
using Northstar.Agent.Actions;
using Northstar.Agent.Core;
using Northstar.Agent.Model;
using Northstar.Agent.Orders.Fake;
using Northstar.Agent.State;

namespace Core.Tests;

/// <summary>Keeps the channel edge honest: only the Host project may know about the Agents SDK.</summary>
public class ArchitectureTests
{
    public static TheoryData<Type> SdkFreeAssemblies => new()
    {
        typeof(SupportResponder),
        typeof(OrderActions),
        typeof(InMemoryStateStore),
        typeof(FakeModelClient),
        typeof(InMemoryOrderBackend),
    };

    [Theory]
    [MemberData(nameof(SdkFreeAssemblies))]
    public void Assembly_does_not_reference_the_agents_sdk(Type marker)
    {
        var references = marker.Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(references, n => n.StartsWith("Microsoft.Agents", StringComparison.Ordinal));
    }

    [Fact]
    public void Actions_assembly_does_not_depend_on_model_or_state()
    {
        var references = typeof(OrderActions).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();

        Assert.DoesNotContain("Northstar.Agent.Model", references);
        Assert.DoesNotContain("Northstar.Agent.State", references);
    }
}
