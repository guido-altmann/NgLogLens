using ApexCharts;

namespace LogLens.Web.Charts;

/// <summary>Achsen für Zählwerte: Anfragen gibt es nur ganzzahlig.</summary>
public static class ChartAxes
{
    /// <summary>Bis zu diesem Höchstwert bekommt jede ganze Zahl einen eigenen Tick.</summary>
    private const int MaxTicksPerUnit = 10;

    /// <summary>
    /// Wertachse eines liegenden Balkendiagramms. Ohne Vorgabe verteilt ApexCharts
    /// Ticks bei kleinen Werten auf 0,5er-Schritte und rundet sie zu „1, 1, 2, 2".
    /// </summary>
    public static XAxis CountAxis(int maxValue) => new()
    {
        TickAmount = maxValue is > 0 and <= MaxTicksPerUnit ? maxValue : null,

        // DecimalsInFloat greift bei liegenden Balken nicht auf die Beschriftung.
        Labels = new XAxisLabels { Formatter = "function (value) { return Math.round(value).toString(); }" },
    };

    /// <summary>Kategorienamen der liegenden Balken nicht abschneiden.</summary>
    public static List<YAxis> CategoryLabels(int maxWidth) =>
        [new YAxis { Labels = new YAxisLabels { MaxWidth = maxWidth } }];
}
