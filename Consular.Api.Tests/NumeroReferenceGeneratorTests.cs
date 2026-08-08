using Consular.Api.Services;
using Xunit;

namespace Consular.Api.Tests;

public class NumeroReferenceGeneratorTests
{
    [Theory]
    [InlineData(2026, "ACTE_NAISSANCE", 1, "DEM-2026-111001")]
    [InlineData(2026, "PASSPORT_RENEWAL", 1, "DEM-2026-222001")]
    [InlineData(2026, "VISA_TOURISTIQUE", 1, "DEM-2026-331001")]
    [InlineData(2026, "VISA_TOURISTIQUE", 2, "DEM-2026-331002")]
    [InlineData(2026, "visa_touristique", 2, "DEM-2026-331002")]
    [InlineData(2026, "SOME_FUTURE_TYPE", 1, "DEM-2026-000001")]
    [InlineData(2026, "ACTE_NAISSANCE", 999, "DEM-2026-111999")]
    public void Format_ProducesExpectedReference(int year, string typeServiceCode, int sequenceForType, string expected)
    {
        var result = NumeroReferenceGenerator.Format(year, typeServiceCode, sequenceForType);

        Assert.Equal(expected, result);
    }
}
