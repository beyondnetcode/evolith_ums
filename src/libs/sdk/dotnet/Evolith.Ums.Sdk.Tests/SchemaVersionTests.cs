using FluentAssertions;
using Evolith.Ums.Sdk.Contracts;
using Xunit;

namespace Evolith.Ums.Sdk.Tests;

public sealed class SchemaVersionTests
{
    [Theory]
    // Soportado = mismo MAJOR que SchemaVersion.Current (hoy 2.x).
    [InlineData("2.0.0", true)]
    [InlineData("2.0.5", true)]
    [InlineData("2.99.0", true)]
    [InlineData("3.0.0", false)]
    [InlineData("1.5.3", false)]
    [InlineData("0.9.0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("not-a-version", false)]
    public void IsSupported_ReturnsExpected(string? input, bool expected)
    {
        SchemaVersion.IsSupported(input).Should().Be(expected);
    }

    /// <summary>
    /// Los casos se DERIVAN de <see cref="SchemaVersion.Latest"/> en vez de repetirlo. Cuando la
    /// tabla copiaba la constante —«2.4.0 va por delante de 2.3.0»— cada versión nueva rompía la
    /// prueba, y arreglarla consistía en volver a copiar la constante: el pin acababa afirmando la
    /// versión de ayer.
    ///
    /// El techo es <c>Latest</c> y no <c>Current</c>: <c>Current</c> es lo que se EMITE en una
    /// respuesta completa (2.4.0) y <c>Latest</c> lo más alto que el SDK entiende (2.5.0, la
    /// respuesta proyectada). Contra <c>Current</c>, un grafo proyectado contaría como «servidor
    /// por delante del SDK», que es falso. Lo que aquí se verifica es la RELACIÓN —un MINOR superior va por delante,
    /// el mismo y los anteriores no, y un MAJOR distinto no es «por delante» sino incompatible—,
    /// que es lo que no debe cambiar al publicar una versión.
    /// </summary>
    [Fact]
    public void IsMinorAhead_DetectsServerNewer()
    {
        var (major, minor) = ActualDesglosada();

        SchemaVersion.IsMinorAhead($"{major}.{minor + 1}.0").Should().BeTrue();
        SchemaVersion.IsMinorAhead($"{major}.{minor}.0").Should().BeFalse();

        for (var anterior = minor - 1; anterior >= 0; anterior--)
            SchemaVersion.IsMinorAhead($"{major}.{anterior}.0").Should().BeFalse();

        SchemaVersion.IsMinorAhead($"{major + 1}.0.0").Should().BeFalse();
        if (major > 0) SchemaVersion.IsMinorAhead($"{major - 1}.99.0").Should().BeFalse();
    }

    private static (int Major, int Minor) ActualDesglosada()
    {
        var partes = SchemaVersion.Latest.Split('.');
        return (int.Parse(partes[0]), int.Parse(partes[1]));
    }

    [Fact]
    public void Current_IsConsistentWithCompatibilityFloor()
    {
        SchemaVersion.IsSupported(SchemaVersion.Current).Should().BeTrue();
        SchemaVersion.IsSupported(SchemaVersion.Projected).Should().BeTrue();
        SchemaVersion.IsSupported(SchemaVersion.CompatibilityMinInclusive).Should().BeTrue();
        SchemaVersion.IsSupported(SchemaVersion.CompatibilityMaxExclusive).Should().BeFalse();
    }
}
