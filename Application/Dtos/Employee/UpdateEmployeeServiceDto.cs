using System.Text.Json.Serialization;

namespace Application.Dtos.Employee;

// Omitted properties leave overrides unchanged; explicit null resets to Service defaults.
public sealed class UpdateEmployeeServiceDto
{
    private decimal? _priceOverride;
    private int? _durationMinutesOverride;

    public decimal? PriceOverride
    {
        get => _priceOverride;
        set
        {
            _priceOverride = value;
            PriceOverrideSpecified = true;
        }
    }

    public int? DurationMinutesOverride
    {
        get => _durationMinutesOverride;
        set
        {
            _durationMinutesOverride = value;
            DurationMinutesOverrideSpecified = true;
        }
    }

    [JsonIgnore]
    public bool PriceOverrideSpecified { get; private set; }

    [JsonIgnore]
    public bool DurationMinutesOverrideSpecified { get; private set; }
}
