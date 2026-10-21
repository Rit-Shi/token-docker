using System;
using System.Threading.Tasks;

// Static description of every supported provider, plus the endpoint routing.
class ProviderInfo {
    public string Id, Label;
    public bool Plan;      // exposes 5h/weekly or monthly quota
    public bool Balance;   // exposes an account balance
    public bool Console;   // needs a browser console session instead of an API key
}

static class Catalog {
    public static readonly ProviderInfo[] All = {
        new ProviderInfo { Id = "glm",      Label = "ZHIPU",    Plan = true,  Balance = false, Console = false },
        new ProviderInfo { Id = "deepseek", Label = "DEEPSEEK", Plan = false, Balance = true,  Console = false },
        new ProviderInfo { Id = "kimi",     Label = "KIMI",     Plan = true,  Balance = true,  Console = false },
        new ProviderInfo { Id = "minimax",  Label = "MINIMAX",  Plan = true,  Balance = true,  Console = false },
        new ProviderInfo { Id = "mimo",     Label = "MIMO",     Plan = true,  Balance = true,  Console = true  }
    };

    public static ProviderInfo Get(string id) {
        foreach (var provider in All) if (provider.Id == id) return provider;
        return null;
    }

    // A provider is shown when it has credentials and was not hidden in the settings.
    public static bool Configured(ProviderInfo info, Config config) {
        if (config.IsHidden(info.Id)) return false;
        switch (info.Id) {
            case "glm": return !string.IsNullOrWhiteSpace(config.Glm);
            case "deepseek": return !string.IsNullOrWhiteSpace(config.DeepSeek) || config.ConsoleConnected;
            case "kimi": return !string.IsNullOrWhiteSpace(config.Kimi);
            case "minimax": return !string.IsNullOrWhiteSpace(config.MiniMax);
            case "mimo": return config.MimoConnected;
        }
        return false;
    }

    public static string Key(ProviderInfo info, Config config) {
        switch (info.Id) {
            case "glm": return config.Glm;
            case "deepseek": return config.DeepSeek;
            case "kimi": return config.Kimi;
            case "minimax": return config.MiniMax;
        }
        return "";
    }
}

static class Fetch {
    const string GlmCn = "https://open.bigmodel.cn/api/monitor/usage/quota/limit";
    const string GlmIntl = "https://api.z.ai/api/monitor/usage/quota/limit";

    // auto: pick the plan endpoint for subscription keys, balance otherwise.
    public static bool WantsPlan(string mode, string key) {
        if (mode == "plan") return true;
        if (mode == "balance") return false;
        key = key ?? "";
        return key.StartsWith("sk-cp", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("sk-kimi", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<Meter> Run(ProviderInfo info, Config config, Func<Task<Meter>> mimo) {
        switch (info.Id) {
            case "glm": {
                if (string.IsNullOrWhiteSpace(config.Glm)) throw new Exception("未填写 GLM 套餐 API Key");
                var json = await Data.Fetch(config.International ? GlmIntl : GlmCn, config.Glm, false);
                return Data.Glm(json);
            }
            case "deepseek": {
                if (string.IsNullOrWhiteSpace(config.DeepSeek)) throw new Exception("未填写 DeepSeek API Key");
                var json = await Data.Fetch("https://api.deepseek.com/user/balance", config.DeepSeek, true);
                return Data.Deep(json);
            }
            case "kimi": {
                if (string.IsNullOrWhiteSpace(config.Kimi)) throw new Exception("未填写 Kimi API Key");
                if (WantsPlan(config.KimiMode, config.Kimi))
                    return Data.KimiUsage(await Data.Fetch(config.KimiInternational
                        ? "https://api.kimi.ai/coding/v1/usages"
                        : "https://api.kimi.com/coding/v1/usages", config.Kimi, true));
                return Data.KimiBalance(await Data.Fetch(config.KimiInternational
                    ? "https://api.moonshot.ai/v1/users/me/balance"
                    : "https://api.moonshot.cn/v1/users/me/balance", config.Kimi, true), config.KimiInternational);
            }
            case "minimax": {
                if (string.IsNullOrWhiteSpace(config.MiniMax)) throw new Exception("未填写 MiniMax API Key");
                string host = config.MiniMaxInternational ? "https://api.minimax.io" : "https://api.minimax.cn";
                if (WantsPlan(config.MiniMaxMode, config.MiniMax))
                    return Data.MiniMaxPlan(await Data.Fetch(host + "/v1/token_plan/remains", config.MiniMax, true));
                return Data.MiniMaxBalance(await Data.Fetch(host + "/account/query_balance", config.MiniMax, true));
            }
            case "mimo": {
                if (mimo == null) throw new Exception("MiMo 控制台未连接");
                return await mimo();
            }
        }
        throw new Exception("未知供应商");
    }
}
