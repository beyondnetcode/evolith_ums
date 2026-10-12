namespace Evolith.Ums.Domain.Test.Authorization.SystemSuite.MenuNode;

using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Xunit;

/// <summary>
/// G-055: <see cref="MenuNodeMetadata"/> es un value object inmutable. Estas pruebas
/// fijan su igualdad por valor sobre los 7 campos normalizados (contenido igual ⇒
/// iguales, hash consistente) y su uso correcto en HashSet/Dictionary.
/// </summary>
public class MenuNodeMetadataEqualityTests
{
    [Fact]
    public void SameContent_AreEqualAndShareHash()
    {
        var a = MenuNodeMetadata.Create(
            responsable: "Ana",
            criticidad: "Alta",
            productoImpactado: "UMS",
            componenteTecnico: "Auth",
            dependencias: "ninguna",
            evidencias: "PR-80",
            trazabilidadSdlc: "ADR-0090");

        var b = MenuNodeMetadata.Create(
            responsable: "Ana",
            criticidad: "Alta",
            productoImpactado: "UMS",
            componenteTecnico: "Auth",
            dependencias: "ninguna",
            evidencias: "PR-80",
            trazabilidadSdlc: "ADR-0090");

        Assert.True(a.Equals(b));
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(ReferenceEquals(a, b));
    }

    [Fact]
    public void NormalizationMakesWhitespaceEquivalent()
    {
        var trimmed = MenuNodeMetadata.Create(responsable: "Ana");
        var padded = MenuNodeMetadata.Create(responsable: "  Ana  ");

        // Normalize() recorta, así que ambos representan el mismo valor.
        Assert.Equal(trimmed, padded);
        Assert.Equal(trimmed.GetHashCode(), padded.GetHashCode());
    }

    [Fact]
    public void EmptyEqualsCreateWithoutArguments()
    {
        Assert.Equal(MenuNodeMetadata.Empty, MenuNodeMetadata.Create());
        Assert.Equal(MenuNodeMetadata.Empty.GetHashCode(), MenuNodeMetadata.Create().GetHashCode());
    }

    [Fact]
    public void DifferentContent_AreNotEqual()
    {
        var a = MenuNodeMetadata.Create(responsable: "Ana");
        var b = MenuNodeMetadata.Create(responsable: "Beto");

        Assert.NotEqual(a, b);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void DifferingOnlyInOneField_AreNotEqual()
    {
        var a = MenuNodeMetadata.Create(responsable: "Ana", criticidad: "Alta");
        var b = MenuNodeMetadata.Create(responsable: "Ana", criticidad: "Media");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void NullAndOtherType_AreNotEqual()
    {
        var a = MenuNodeMetadata.Create(responsable: "Ana");

        Assert.False(a.Equals(null));
        Assert.False(a.Equals("Ana"));
    }

    [Fact]
    public void WorksAsHashSetElementAndDictionaryKey()
    {
        var a1 = MenuNodeMetadata.Create(responsable: "Ana", criticidad: "Alta");
        var a2 = MenuNodeMetadata.Create(responsable: "Ana", criticidad: "Alta");
        var other = MenuNodeMetadata.Create(responsable: "Beto");

        var set = new HashSet<MenuNodeMetadata> { a1, a2, other };
        Assert.Equal(2, set.Count);
        Assert.Contains(a2, set);

        var map = new Dictionary<MenuNodeMetadata, string> { [a1] = "x" };
        Assert.True(map.TryGetValue(a2, out var found));
        Assert.Equal("x", found);
    }
}
