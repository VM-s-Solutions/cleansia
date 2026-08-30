using Cleansia.Core.AppServices.Features.PropertySizes;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.PropertySizes;

/// <summary>
/// The per-country size catalogue (T-0675 / ADR-0056).
///
/// <para>The thing worth pinning is what happens when a label is missing, because
/// that is the only way this can fail visibly. Everything else about a preset —
/// the two integers — is what the platform already prices on and cannot drift.</para>
/// </summary>
public class GetPropertySizePresetsTests
{
    private readonly Mock<IPropertySizePresetRepository> _repository = new();

    private GetPropertySizePresets.Handler CreateHandler() => new(_repository.Object);

    private void Arrange(params PropertySizePreset[] presets) =>
        _repository
            .Setup(r => r.GetForCountryIsoCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(presets);

    private static PropertySizePreset Preset(
        string code, int sortOrder, int rooms, int bathrooms, params (string Lang, string Name)[] labels)
    {
        var preset = PropertySizePreset.Create("country-1", code, sortOrder, rooms, bathrooms);
        foreach (var (lang, name) in labels)
        {
            preset.SetTranslation(lang, name);
        }
        return preset;
    }

    [Fact]
    public async Task A_preset_is_labelled_in_the_requested_language()
    {
        Arrange(Preset("CZ_3KK", 3, 3, 1, ("en", "3 rooms"), ("cs", "3+kk")));

        var result = await CreateHandler().Handle(
            new GetPropertySizePresets.Request("CZE", "cs"), CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal("3+kk", only.Label);
        Assert.Equal("CZ_3KK", only.Code);
        Assert.Equal(3, only.Rooms);
        Assert.Equal(1, only.Bathrooms);
    }

    [Fact]
    public async Task A_missing_language_falls_back_to_english()
    {
        Arrange(Preset("CZ_3KK", 3, 3, 1, ("en", "3 rooms"), ("cs", "3+kk")));

        var result = await CreateHandler().Handle(
            new GetPropertySizePresets.Request("CZE", "uk"), CancellationToken.None);

        Assert.Equal("3 rooms", Assert.Single(result).Label);
    }

    [Fact]
    public async Task With_no_label_at_all_the_code_is_shown_rather_than_nothing()
    {
        // A blank chip is unpickable and tells nobody anything is wrong. The code
        // is ugly on purpose: the visitor can still choose, and we can see the gap.
        Arrange(Preset("CZ_3KK", 3, 3, 1));

        var result = await CreateHandler().Handle(
            new GetPropertySizePresets.Request("CZE", "cs"), CancellationToken.None);

        Assert.Equal("CZ_3KK", Assert.Single(result).Label);
    }

    [Fact]
    public async Task An_empty_label_is_treated_as_missing_not_as_a_label()
    {
        Arrange(Preset("CZ_3KK", 3, 3, 1, ("cs", "   "), ("en", "3 rooms")));

        var result = await CreateHandler().Handle(
            new GetPropertySizePresets.Request("CZE", "cs"), CancellationToken.None);

        Assert.Equal("3 rooms", Assert.Single(result).Label);
    }

    [Fact]
    public async Task An_unserved_country_returns_an_empty_list_rather_than_failing()
    {
        // The caller is a price calculator on a public page. A market we hold no
        // presets for is one we do not serve yet, not an error to render.
        Arrange();

        var result = await CreateHandler().Handle(
            new GetPropertySizePresets.Request("POL", "en"), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task The_repository_order_is_preserved()
    {
        Arrange(
            Preset("CZ_1KK", 1, 1, 1, ("en", "1 room")),
            Preset("CZ_2KK", 2, 2, 1, ("en", "2 rooms")),
            Preset("CZ_HOUSE", 5, 5, 2, ("en", "House")));

        var result = await CreateHandler().Handle(
            new GetPropertySizePresets.Request("CZE", "en"), CancellationToken.None);

        Assert.Equal(new[] { "CZ_1KK", "CZ_2KK", "CZ_HOUSE" }, result.Select(p => p.Code));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void A_negative_room_count_is_refused_at_construction(int rooms, int bathrooms)
    {
        Assert.Throws<ArgumentException>(
            () => PropertySizePreset.Create("country-1", "BAD", 1, rooms, bathrooms));
    }
}
