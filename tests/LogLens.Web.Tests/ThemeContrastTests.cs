using LogLens.Web.Layout;
using MudBlazor;
using MudBlazor.Utilities;

namespace LogLens.Web.Tests;

/// <summary>
/// Kontraste der App-Paletten nach WCAG 2.2 (SPEC 9, Barrierefreiheit): Text mindestens
/// 4,5:1, Symbole und Rahmen mindestens 3:1 – im hellen wie im dunklen Modus.
/// </summary>
public sealed class ThemeContrastTests
{
    private const double TextContrast = 4.5;

    private const double GraphicsContrast = 3.0;

    public static TheoryData<string> Modes => ["hell", "dunkel"];

    [Theory]
    [MemberData(nameof(Modes))]
    public void Fliesstext_ist_auf_allen_Flaechen_lesbar(string mode)
    {
        var palette = PaletteFor(mode);

        foreach (var surface in new[] { palette.Background, palette.Surface, palette.BackgroundGray })
        {
            AssertContrast(palette.TextPrimary, surface, TextContrast, $"{mode}: TextPrimary auf {surface}");
            AssertContrast(palette.TextSecondary, surface, TextContrast, $"{mode}: TextSecondary auf {surface}");
            AssertContrast(palette.Primary, surface, TextContrast, $"{mode}: Primary (Links) auf {surface}");
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Kopfzeile_Navigation_und_Schaltflaechen_sind_lesbar(string mode)
    {
        var palette = PaletteFor(mode);

        AssertContrast(palette.AppbarText, palette.AppbarBackground, TextContrast, $"{mode}: Kopfzeile");
        AssertContrast(palette.DrawerText, palette.DrawerBackground, TextContrast, $"{mode}: Navigation");
        AssertContrast(palette.Primary, palette.DrawerBackground, TextContrast, $"{mode}: aktiver Navigationspunkt");
        AssertContrast(palette.PrimaryContrastText, palette.Primary, TextContrast, $"{mode}: gefüllte Schaltfläche");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Hinweise_sind_lesbar(string mode)
    {
        var palette = PaletteFor(mode);

        // Umrandete Hinweise (MudAlert Outlined) schreiben in der Darken-Variante,
        // ihr Symbol in der Grundfarbe.
        var statuses = new (string Name, MudColor Color, string Text)[]
        {
            ("Info", palette.Info, palette.InfoDarken),
            ("Success", palette.Success, palette.SuccessDarken),
            ("Warning", palette.Warning, palette.WarningDarken),
            ("Error", palette.Error, palette.ErrorDarken),
        };

        foreach (var (name, color, text) in statuses)
        {
            foreach (var surface in new[] { palette.Background, palette.Surface })
            {
                AssertContrast(new MudColor(text), surface, TextContrast, $"{mode}: {name}-Text auf {surface}");
                AssertContrast(color, surface, GraphicsContrast, $"{mode}: {name}-Symbol auf {surface}");
            }
        }
    }

    private static Palette PaletteFor(string mode) =>
        mode == "dunkel" ? AppTheme.Theme.PaletteDark : AppTheme.Theme.PaletteLight;

    private static void AssertContrast(MudColor foreground, MudColor background, double minimum, string what)
    {
        var ratio = Contrast(foreground, background);
        Assert.True(ratio >= minimum, $"{what}: {ratio:N2}:1, verlangt {minimum:N1}:1.");
    }

    /// <summary>Kontrastverhältnis nach WCAG; halbtransparenter Text wird vorher auf den Hintergrund gelegt.</summary>
    private static double Contrast(MudColor foreground, MudColor background)
    {
        var alpha = foreground.APercentage;
        double Blend(byte fg, byte bg) => (fg * alpha) + (bg * (1 - alpha));

        var fg = Luminance(Blend(foreground.R, background.R), Blend(foreground.G, background.G), Blend(foreground.B, background.B));
        var bg = Luminance(background.R, background.G, background.B);

        return (Math.Max(fg, bg) + 0.05) / (Math.Min(fg, bg) + 0.05);
    }

    private static double Luminance(double r, double g, double b) =>
        (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));

    private static double Channel(double value)
    {
        var c = value / 255d;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
