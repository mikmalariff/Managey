namespace Managey.Api.Services;

/// <summary>
/// Official PERKESO contribution schedules, wage ceiling RM6,000.
/// SOCSO (Act 4) and SKBBK / LINDUNG 24 Jam: PERKESO FAQ "Text File Format for Employee Contributions", Appendix 1
/// (Third Schedule, in force from 1 June 2026). EIS (Act 800): Second Schedule.
/// Each row covers wages above the previous row's limit up to and including <see cref="Band.UpTo"/>;
/// wages above RM6,000 use the last row.
/// </summary>
public static class PerkesoTables
{
    public record Band(decimal UpTo, decimal SocsoEmployer, decimal SocsoEmployee, decimal SkbbkEmployee, decimal SocsoEmployerCategory2, decimal Eis);

    public static readonly Band[] Bands =
    [
        new(30m, 0.40m, 0.10m, 0.20m, 0.30m, 0.05m),
        new(50m, 0.70m, 0.20m, 0.30m, 0.50m, 0.10m),
        new(70m, 1.10m, 0.30m, 0.50m, 0.80m, 0.15m),
        new(100m, 1.50m, 0.40m, 0.65m, 1.10m, 0.20m),
        new(140m, 2.10m, 0.60m, 0.90m, 1.50m, 0.25m),
        new(200m, 2.95m, 0.85m, 1.25m, 2.10m, 0.35m),
        new(300m, 4.35m, 1.25m, 1.85m, 3.10m, 0.50m),
        new(400m, 6.15m, 1.75m, 2.65m, 4.40m, 0.70m),
        new(500m, 7.85m, 2.25m, 3.35m, 5.60m, 0.90m),
        new(600m, 9.65m, 2.75m, 4.15m, 6.90m, 1.10m),
        new(700m, 11.35m, 3.25m, 4.85m, 8.10m, 1.30m),
        new(800m, 13.15m, 3.75m, 5.65m, 9.40m, 1.50m),
        new(900m, 14.85m, 4.25m, 6.35m, 10.60m, 1.70m),
        new(1000m, 16.65m, 4.75m, 7.15m, 11.90m, 1.90m),
        new(1100m, 18.35m, 5.25m, 7.85m, 13.10m, 2.10m),
        new(1200m, 20.15m, 5.75m, 8.65m, 14.40m, 2.30m),
        new(1300m, 21.85m, 6.25m, 9.35m, 15.60m, 2.50m),
        new(1400m, 23.65m, 6.75m, 10.15m, 16.90m, 2.70m),
        new(1500m, 25.35m, 7.25m, 10.85m, 18.10m, 2.90m),
        new(1600m, 27.15m, 7.75m, 11.65m, 19.40m, 3.10m),
        new(1700m, 28.85m, 8.25m, 12.35m, 20.60m, 3.30m),
        new(1800m, 30.65m, 8.75m, 13.15m, 21.90m, 3.50m),
        new(1900m, 32.35m, 9.25m, 13.85m, 23.10m, 3.70m),
        new(2000m, 34.15m, 9.75m, 14.65m, 24.40m, 3.90m),
        new(2100m, 35.85m, 10.25m, 15.35m, 25.60m, 4.10m),
        new(2200m, 37.65m, 10.75m, 16.15m, 26.90m, 4.30m),
        new(2300m, 39.35m, 11.25m, 16.85m, 28.10m, 4.50m),
        new(2400m, 41.15m, 11.75m, 17.65m, 29.40m, 4.70m),
        new(2500m, 42.85m, 12.25m, 18.35m, 30.60m, 4.90m),
        new(2600m, 44.65m, 12.75m, 19.15m, 31.90m, 5.10m),
        new(2700m, 46.35m, 13.25m, 19.85m, 33.10m, 5.30m),
        new(2800m, 48.15m, 13.75m, 20.65m, 34.40m, 5.50m),
        new(2900m, 49.85m, 14.25m, 21.35m, 35.60m, 5.70m),
        new(3000m, 51.65m, 14.75m, 22.15m, 36.90m, 5.90m),
        new(3100m, 53.35m, 15.25m, 22.85m, 38.10m, 6.10m),
        new(3200m, 55.15m, 15.75m, 23.65m, 39.40m, 6.30m),
        new(3300m, 56.85m, 16.25m, 24.35m, 40.60m, 6.50m),
        new(3400m, 58.65m, 16.75m, 25.15m, 41.90m, 6.70m),
        new(3500m, 60.35m, 17.25m, 25.85m, 43.10m, 6.90m),
        new(3600m, 62.15m, 17.75m, 26.65m, 44.40m, 7.10m),
        new(3700m, 63.85m, 18.25m, 27.35m, 45.60m, 7.30m),
        new(3800m, 65.65m, 18.75m, 28.15m, 46.90m, 7.50m),
        new(3900m, 67.35m, 19.25m, 28.85m, 48.10m, 7.70m),
        new(4000m, 69.15m, 19.75m, 29.65m, 49.40m, 7.90m),
        new(4100m, 70.85m, 20.25m, 30.35m, 50.60m, 8.10m),
        new(4200m, 72.65m, 20.75m, 31.15m, 51.90m, 8.30m),
        new(4300m, 74.35m, 21.25m, 31.85m, 53.10m, 8.50m),
        new(4400m, 76.15m, 21.75m, 32.65m, 54.40m, 8.70m),
        new(4500m, 77.85m, 22.25m, 33.35m, 55.60m, 8.90m),
        new(4600m, 79.65m, 22.75m, 34.15m, 56.90m, 9.10m),
        new(4700m, 81.35m, 23.25m, 34.85m, 58.10m, 9.30m),
        new(4800m, 83.15m, 23.75m, 35.65m, 59.40m, 9.50m),
        new(4900m, 84.85m, 24.25m, 36.35m, 60.60m, 9.70m),
        new(5000m, 86.65m, 24.75m, 37.15m, 61.90m, 9.90m),
        new(5100m, 88.35m, 25.25m, 37.85m, 63.10m, 10.10m),
        new(5200m, 90.15m, 25.75m, 38.65m, 64.40m, 10.30m),
        new(5300m, 91.85m, 26.25m, 39.35m, 65.60m, 10.50m),
        new(5400m, 93.65m, 26.75m, 40.15m, 66.90m, 10.70m),
        new(5500m, 95.35m, 27.25m, 40.85m, 68.10m, 10.90m),
        new(5600m, 97.15m, 27.75m, 41.65m, 69.40m, 11.10m),
        new(5700m, 98.85m, 28.25m, 42.35m, 70.60m, 11.30m),
        new(5800m, 100.65m, 28.75m, 43.15m, 71.90m, 11.50m),
        new(5900m, 102.35m, 29.25m, 43.85m, 73.10m, 11.70m),
        new(6000m, 104.15m, 29.75m, 44.65m, 74.40m, 11.90m),
    ];
}
