namespace Evolith.Ums.Domain.Test.Configuration.Parameter;

using Evolith.Ums.Domain.Configuration.Parameter;
using Xunit;

/// <summary>
/// G-055: los identificadores de los agregados de Parameter son value objects de
/// identidad. Estas pruebas fijan que su igualdad es por valor (mismo Guid ⇒ iguales,
/// hash consistente) y que sirven en HashSet/Dictionary, no por referencia.
/// </summary>
public class ParameterIdEqualityTests
{
    private static readonly Guid GuidA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GuidB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ----- ParameterDefinitionId -----

    [Fact]
    public void ParameterDefinitionId_SameValue_AreEqualAndShareHash()
    {
        var a = ParameterDefinitionId.Load(GuidA);
        var b = ParameterDefinitionId.Create(GuidA);

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(ReferenceEquals(a, b));
    }

    [Fact]
    public void ParameterDefinitionId_DifferentValue_AreNotEqual()
    {
        var a = ParameterDefinitionId.Load(GuidA);
        var b = ParameterDefinitionId.Load(GuidB);

        Assert.NotEqual(a, b);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void ParameterDefinitionId_WorksAsHashSetElementAndDictionaryKey()
    {
        var a1 = ParameterDefinitionId.Load(GuidA);
        var a2 = ParameterDefinitionId.Load(GuidA);
        var b = ParameterDefinitionId.Load(GuidB);

        var set = new HashSet<ParameterDefinitionId> { a1, a2, b };
        Assert.Equal(2, set.Count);
        Assert.Contains(a2, set);

        var map = new Dictionary<ParameterDefinitionId, string> { [a1] = "uno" };
        Assert.True(map.TryGetValue(a2, out var found));
        Assert.Equal("uno", found);
    }

    [Fact]
    public void ParameterDefinitionId_NullAndOtherType_AreNotEqual()
    {
        var a = ParameterDefinitionId.Load(GuidA);

        Assert.False(a.Equals(null));
        Assert.False(a.Equals("11111111-1111-1111-1111-111111111111"));
    }

    // ----- ParameterGlobalValueId -----

    [Fact]
    public void ParameterGlobalValueId_SameValue_AreEqualAndShareHash()
    {
        var a = ParameterGlobalValueId.Load(GuidA);
        var b = ParameterGlobalValueId.Create(GuidA);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ParameterGlobalValueId_DifferentValue_AreNotEqual()
    {
        Assert.NotEqual(ParameterGlobalValueId.Load(GuidA), ParameterGlobalValueId.Load(GuidB));
    }

    [Fact]
    public void ParameterGlobalValueId_WorksAsDictionaryKey()
    {
        var map = new Dictionary<ParameterGlobalValueId, int>
        {
            [ParameterGlobalValueId.Load(GuidA)] = 7,
        };

        Assert.True(map.ContainsKey(ParameterGlobalValueId.Load(GuidA)));
        Assert.False(map.ContainsKey(ParameterGlobalValueId.Load(GuidB)));
    }

    // ----- ParameterTenantValueId -----

    [Fact]
    public void ParameterTenantValueId_SameValue_AreEqualAndShareHash()
    {
        var a = ParameterTenantValueId.Load(GuidA);
        var b = ParameterTenantValueId.Create(GuidA);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ParameterTenantValueId_DifferentValue_AreNotEqual()
    {
        Assert.NotEqual(ParameterTenantValueId.Load(GuidA), ParameterTenantValueId.Load(GuidB));
    }

    [Fact]
    public void ParameterTenantValueId_HashSetDeduplicatesByValue()
    {
        var set = new HashSet<ParameterTenantValueId>
        {
            ParameterTenantValueId.Load(GuidA),
            ParameterTenantValueId.Load(GuidA),
        };

        Assert.Single(set);
    }
}
