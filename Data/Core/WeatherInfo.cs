using System;
using System.Globalization;
using Memori.Localization;
using TJ;

// Builds a weather's description with the current (possibly mod-overridden) WeatherRuleData values
// injected into the localized template's {0}/{1} placeholders, so the displayed numbers always match
// what the weather actually does. Same shape as RacePassiveInfo.
public static class WeatherInfo
{
    // Falls back to the raw localized string when the template has no {0} placeholder (Snow has no
    // number in its text; a locale not yet converted shows as-is instead of erroring).
    public static string GetDescription(Weather weather)
    {
        string template = LocalizationManager.Instance.GetText(weather.ToString() + "Desc");
        if (string.IsNullOrEmpty(template) || !template.Contains("{0}")) return template;

        try
        {
            return string.Format(template, GetArgs(weather));
        }
        catch (FormatException)
        {
            return template;
        }
    }

    // Placeholder order matches each <Weather>Desc localization string. Values are shown as the
    // percent reduction a player reads, not the multiplier the systems apply.
    private static object[] GetArgs(Weather weather)
    {
        switch (weather)
        {
            case Weather.Rain:
                return new object[] { Percent(1f - WeatherRuleData.Rain.LargeUnitSpeedModifier) };
            case Weather.Fog:
                return new object[] { Percent(1f - WeatherRuleData.Fog.RangeModifier), Percent(1f - WeatherRuleData.Fog.AccuracyModifier) };
            default:
                return Array.Empty<object>();
        }
    }

    private static string Percent(float fraction)
    {
        float value = fraction * 100f;
        return value.ToString(value % 1f == 0f ? "0" : "0.##", CultureInfo.InvariantCulture);
    }
}
