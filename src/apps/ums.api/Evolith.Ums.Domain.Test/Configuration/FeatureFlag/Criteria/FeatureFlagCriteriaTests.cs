namespace Evolith.Ums.Domain.Test.Configuration.FeatureFlag.Criteria;

using Evolith.Ums.Domain.Configuration.FeatureFlag.Criteria;
using Xunit;

public class FeatureFlagCriteriaTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = FeatureFlagCriteria.Create("UserGroup", "Equals", "admins");

        Assert.True(result.IsSuccess);
        Assert.Equal("UserGroup", result.Value.CriteriaType);
        Assert.Equal("Equals", result.Value.Operator);
        Assert.Equal("admins", result.Value.Value);
    }

    [Fact]
    public void Create_WithEmptyCriteriaType_ReturnsFailure()
    {
        var result = FeatureFlagCriteria.Create("", "Equals", "admins");

        Assert.True(result.IsFailure);
        Assert.Contains("configuration.criteria_type_required", result.Error);
    }

    [Fact]
    public void Create_WithWhitespaceCriteriaType_ReturnsFailure()
    {
        var result = FeatureFlagCriteria.Create("   ", "Equals", "admins");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithEmptyOperator_ReturnsFailure()
    {
        var result = FeatureFlagCriteria.Create("UserGroup", "", "admins");

        Assert.True(result.IsFailure);
        Assert.Contains("configuration.criteria_operator_required", result.Error);
    }

    [Fact]
    public void Create_WithEmptyValue_ReturnsFailure()
    {
        var result = FeatureFlagCriteria.Create("UserGroup", "Equals", "");

        Assert.True(result.IsFailure);
        Assert.Contains("configuration.criteria_value_required", result.Error);
    }

    [Fact]
    public void Load_WithValidData_ReturnsFeatureFlagCriteria()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var criteria = FeatureFlagCriteria.Load(id, "UserGroup", "Contains", "admin", now);

        Assert.Equal("UserGroup", criteria.CriteriaType);
        Assert.Equal("Contains", criteria.Operator);
        Assert.Equal("admin", criteria.Value);
        Assert.Equal(now, criteria.CreatedAtUtc);
    }
}
