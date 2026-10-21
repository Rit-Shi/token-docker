using System;

// Colour thresholds for the quota rings and today's spend. Defaults reproduce the
// values that used to be hard-coded in Paint.Quota / Paint.Expense.
class Limits {
    public const double DefaultQuotaWarn = 50;
    public const double DefaultCostWarn = 2.5;
    public const double DefaultCostDanger = 5;

    public double QuotaWarn = DefaultQuotaWarn;
    public double CostWarn = DefaultCostWarn;
    public double CostDanger = DefaultCostDanger;

    public static Limits From(Config config) {
        return config == null ? new Limits() : From(config.QuotaWarn, config.CostWarn, config.CostDanger);
    }

    // Missing or unusable stored values fall back to the defaults instead of failing the start-up.
    public static Limits From(double? quotaWarn, double? costWarn, double? costDanger) {
        var limits = new Limits();
        if (quotaWarn.HasValue && quotaWarn.Value >= 1 && quotaWarn.Value <= 100) limits.QuotaWarn = quotaWarn.Value;
        if (costWarn.HasValue && costWarn.Value >= 0) limits.CostWarn = costWarn.Value;
        if (costDanger.HasValue && costDanger.Value > limits.CostWarn) limits.CostDanger = costDanger.Value;
        else limits.CostDanger = Math.Max(limits.CostWarn + 1, DefaultCostDanger);
        return limits;
    }

    public void StoreTo(Config config) {
        config.QuotaWarn = QuotaWarn;
        config.CostWarn = CostWarn;
        config.CostDanger = CostDanger;
    }

    // Validates the three settings fields and parses them; used by the settings dialog.
    public static bool TryParse(string quotaWarn, string costWarn, string costDanger, out Limits limits, out string error) {
        limits = null;
        double quota, warn, danger;
        error = Parse(quotaWarn, "额度警戒线", out quota);
        if (error != null) return false;
        error = Parse(costWarn, "今日花费预警", out warn);
        if (error != null) return false;
        error = Parse(costDanger, "今日花费告警", out danger);
        if (error != null) return false;

        if (quota < 1 || quota > 100) {
            error = "额度警戒线需要 1 到 100 之间的百分比。";
            return false;
        }
        if (warn < 0) {
            error = "今日花费预警不能小于 0。";
            return false;
        }
        if (danger <= warn) {
            error = "今日花费告警需要大于预警金额。";
            return false;
        }
        limits = new Limits { QuotaWarn = quota, CostWarn = warn, CostDanger = danger };
        return true;
    }

    static string Parse(string text, string name, out double value) {
        if (!double.TryParse((text ?? "").Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value))
            return name + "需要一个数字。";
        if (double.IsNaN(value) || double.IsInfinity(value)) return name + "需要一个数字。";
        return null;
    }
}
