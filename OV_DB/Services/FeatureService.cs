using Microsoft.Extensions.Configuration;
using System;

namespace OV_DB.Services;

/// <summary>Who can see an optional feature.</summary>
public enum FeatureVisibility
{
    Off,
    Admin,
    On
}

public interface IFeatureService
{
    FeatureVisibility Achievements { get; }
    bool IsVisible(FeatureVisibility visibility, bool isAdmin);
}

/// <summary>
/// Optional features, configured per environment (so the deployment can flip one without a code
/// change, e.g. Features__Achievements=On). Anything unrecognised falls back to admin-only, which
/// is the safe direction: a typo hides a feature rather than exposing it to everyone.
/// </summary>
public class FeatureService(IConfiguration configuration) : IFeatureService
{
    public FeatureVisibility Achievements => Parse(configuration["Features:Achievements"]);

    public bool IsVisible(FeatureVisibility visibility, bool isAdmin) => visibility switch
    {
        FeatureVisibility.On => true,
        FeatureVisibility.Admin => isAdmin,
        _ => false
    };

    internal static FeatureVisibility Parse(string value)
    {
        // Fully qualified: the project has its own OV_DB.Enum namespace that shadows System.Enum.
        return System.Enum.TryParse<FeatureVisibility>(value, ignoreCase: true, out var parsed)
            ? parsed
            : FeatureVisibility.Admin;
    }
}
