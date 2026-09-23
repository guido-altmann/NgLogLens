using MudBlazor;

namespace LogLens.Web.Layout;

/// <summary>
/// Farben der Oberfläche. Ausgangspunkt sind die MudBlazor-Vorgaben; geändert ist nur,
/// was die Kontrastgrenzen nach WCAG 2.2 verfehlt (SPEC 9): Text 4,5:1, Symbole 3:1.
/// Die Prüfung steht in <c>ThemeContrastTests</c>.
/// </summary>
/// <remarks>
/// Umrandete Hinweise schreiben in der <c>…Darken</c>-Variante. Im dunklen Modus wäre
/// eine abgedunkelte Farbe auf dunklem Grund schlechter lesbar, deshalb steht dort eine
/// aufgehellte. Die Diagrammfarben haben eigene, geprüfte Sätze (<c>Charts/</c>).
/// </remarks>
public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            // Vorgabe 0.54: auf dem Grau der Code-Schnipsel knapp unter 4,5:1.
            TextSecondary = "rgba(0,0,0,0.6)",

            Info = "#1976d2",
            InfoDarken = "#1565c0",
            Success = "#2e7d32",
            SuccessDarken = "#1b5e20",
            Warning = "#ed6c02",
            WarningDarken = "#9a5b00",
            Error = "#d32f2f",
            ErrorDarken = "#b71c1c",
        },
        PaletteDark = new PaletteDark
        {
            // Vorgabe #776be7: als Linkfarbe auf dunklem Grund nur 2,8:1.
            Primary = "#a59cf2",
            PrimaryContrastText = "#1b1b22",

            // Vorgabe 0.5: auf der Kartenfläche 4,3:1.
            TextSecondary = "rgba(255,255,255,0.62)",
            DrawerText = "rgba(255,255,255,0.7)",
            DrawerIcon = "rgba(255,255,255,0.7)",

            InfoDarken = "#5cadff",
            SuccessDarken = "#0dde9c",
            WarningDarken = "#ffb624",
            Error = "#f7707f",
            ErrorDarken = "#fa8c99",
        },
    };
}
