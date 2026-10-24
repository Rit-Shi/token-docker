using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class Meter {
    public string Name, Value, Detail;
    public double? Remaining;
    public double? FiveHour, Weekly;
    // Numeric balance and its currency, when the provider exposes one. Used to estimate
    // today's spend from the balance difference for providers without a usage endpoint.
    public decimal? Amount;
    public string Currency;
    public Meter(string n, string v, string d, double? r = null) { Name=n; Value=v; Detail=d; Remaining=r; }
}
class Config {
    public string Glm="", DeepSeek="";
    public bool International=false;
    public int X=-1, Y=30; public string Theme="dark", Edge="right", UsageFile=""; public bool ConsoleConnected=false;
    // Colour thresholds; null keeps the historical defaults (see Limits).
    public double? QuotaWarn, CostWarn, CostDanger;
    // Additional providers. Mode: auto | balance | plan.
    public string Kimi="", MiniMax="";
    public string KimiMode="auto", MiniMaxMode="auto";
    public bool KimiInternational=false, MiniMaxInternational=false;
    public bool MimoConnected=false;
    // Display: list | cycle. Hidden holds comma separated provider ids.
    public string Display="list", Primary="glm", Hidden="";
    // Taskbar strip: enabled, plus the remembered horizontal position (-1 = right aligned).
    public bool Taskbar=false;
    public int TaskbarX=-1;
    public bool IsHidden(string id){
        if(string.IsNullOrWhiteSpace(Hidden)) return false;
        foreach(var part in Hidden.Split(',')) if(string.Equals(part.Trim(),id,StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
static class Data {
    public static JavaScriptSerializer Json() { return new JavaScriptSerializer(); }
    public static Dictionary<string,object> Map(object o) { return o as Dictionary<string,object> ?? new Dictionary<string,object>(); }
    public static object Get(object o,string k) { object v; return Map(o).TryGetValue(k,out v)?v:null; }
    public static string Str(object o) { return o==null?"":Convert.ToString(o,CultureInfo.InvariantCulture); }
    public static double? Num(object o) { double n; return o!=null && double.TryParse(Str(o),NumberStyles.Float,CultureInfo.InvariantCulture,out n) && !double.IsNaN(n) && !double.IsInfinity(n)?(double?)n:null; }
    public static double? Remaining(object used) { var n=Num(used); return n.HasValue?(double?)Math.Max(0,Math.Min(100,100-n.Value)):null; }
    public static string Pct(double? p) { return p.HasValue?p.Value.ToString("0.#",CultureInfo.InvariantCulture)+"%":"未知"; }
    public static string Reset(object value) {
        var n=Num(value); if(!n.HasValue) return "未提供重置时间";
        try { return "重置于 "+new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(n.Value).ToLocalTime().ToString("MM-dd HH:mm"); } catch { return "重置时间未知"; }
    }
    public static Meter Glm(object result) {
        if(Get(result,"success") is bool && !(bool)Get(result,"success")) throw new Exception("套餐查询失败，请检查 Key 和地区");
        var payload=Get(result,"data")??result; var limits=(Get(payload,"limits") as object[])??(payload as object[]);
        var lines=new List<string>(); double? five=null, week=null;
        if(limits!=null) foreach(var item in limits) {
            string type=Str(Get(item,"type")).ToUpperInvariant(); var left=Remaining(Get(item,"percentage"));
            if(!left.HasValue){var total=Num(Get(item,"usage"));var remaining=Num(Get(item,"remaining"));var used=Num(Get(item,"currentValue"));if(total>0){if(remaining.HasValue)left=Math.Max(0,Math.Min(100,remaining.Value/total.Value*100));else if(used.HasValue)left=Remaining(used.Value/total.Value*100);}}
            double? unit=Num(Get(item,"unit")), number=Num(Get(item,"number"));
            bool modelQuota=type=="TOKENS_LIMIT"||type=="CREDIT_LIMIT";
            bool isFive=modelQuota && unit==3 && (number==5 || !number.HasValue);
            bool isWeek=modelQuota && unit==6 && (number==1 || number==7 || !number.HasValue);
            string label=isFive?"5 小时":isWeek?"周额度":modelQuota?"未识别周期":type=="TIME_LIMIT"?"MCP 月额度":type;
            lines.Add(label+" 剩余 "+Pct(left));
            var reset=Num(Get(item,"nextResetTime"));
            if(reset.HasValue) lines.Add(Reset(reset.Value/1000));
            if(isFive && left.HasValue) five=five.HasValue?Math.Min(five.Value,left.Value):left;
            if(isWeek && left.HasValue) week=week.HasValue?Math.Min(week.Value,left.Value):left;
        }
        return new Meter("GLM","5h "+Pct(five)+" · 周 "+Pct(week),lines.Count==0?"接口未返回可识别的套餐额度":string.Join("\n",lines)) {FiveHour=five,Weekly=week};
    }
    public static Meter Deep(object result) {
        var balances=Get(result,"balance_infos") as IEnumerable; var lines=new List<string>(); var values=new List<string>();
        decimal? total=null; string mainCurrency=null;
        if(balances!=null) foreach(var item in balances) {
            string currency=Str(Get(item,"currency")); decimal amount;
            if(!decimal.TryParse(Str(Get(item,"total_balance")),NumberStyles.Number,CultureInfo.InvariantCulture,out amount)) continue;
            values.Add((currency=="CNY"?"¥":currency=="USD"?"$":currency+" ")+amount.ToString("0.00",CultureInfo.InvariantCulture));
            lines.Add(currency+" 可用 "+amount.ToString("0.00")+"；赠金 "+Str(Get(item,"granted_balance"))+"；充值 "+Str(Get(item,"topped_up_balance")));
            if(!total.HasValue || currency=="CNY") { total=amount; mainCurrency=currency; }
        }
        if(values.Count==0) throw new Exception("接口未返回有效余额");
        return new Meter("DeepSeek",string.Join(" / ",values),string.Join("\n",lines)+(Get(result,"is_available") is bool && !(bool)Get(result,"is_available")?"\n当前不可用于 API 调用":"")) {Amount=total,Currency=mainCurrency};
    }
    public static string Money(string currency,double amount) {
        string number=amount.ToString("0.00",CultureInfo.InvariantCulture);
        return currency=="CNY"?"¥"+number:currency=="USD"?"$"+number:(currency==""?"":currency+" ")+number;
    }
    static double? Share(object remaining,object limit) {
        var left=Num(remaining); var total=Num(limit);
        return left.HasValue&&total.HasValue&&total.Value>0?(double?)Math.Max(0,Math.Min(100,left.Value/total.Value*100)):null;
    }
    static double? RatioLeft(object entry) {
        var used=Num(Get(entry,"used_ratio"));
        if(used.HasValue) return Math.Max(0,Math.Min(100,100-used.Value*100));
        return Share(Get(entry,"remaining"),Get(entry,"limit"));
    }
    public static string IsoReset(object value,string prefix) {
        string text=Str(value); DateTime time;
        if(!DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out time)) return "";
        return prefix+time.ToLocalTime().ToString("MM-dd HH:mm",CultureInfo.InvariantCulture);
    }
    static string ResetMs(object value,string prefix) {
        var n=Num(value); if(!n.HasValue) return "";
        try { return prefix+new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddMilliseconds(n.Value).ToLocalTime().ToString("MM-dd HH:mm"); } catch { return ""; }
    }
    static void ApiOk(object result) {
        var baseResponse=Get(result,"base_resp");
        var code=Num(Get(baseResponse,"status_code"));
        if(code.HasValue&&code.Value!=0) throw new Exception(Str(Get(baseResponse,"status_msg")));
    }
    public static Meter KimiBalance(object result,bool international) {
        var data=Get(result,"data");
        var available=Num(Get(data,"available_balance"));
        if(!available.HasValue) throw new Exception("接口未返回可用余额");
        string currency=international?"USD":"CNY"; var lines=new List<string>();
        lines.Add("代金券余额与现金余额之和");
        var voucher=Num(Get(data,"voucher_balance"));
        var cash=Num(Get(data,"cash_balance"));
        if(voucher.HasValue) lines.Add("代金券 "+Money(currency,voucher.Value));
        if(cash.HasValue) lines.Add("现金 "+Money(currency,cash.Value));
        if(available.Value<=0) lines.Add("余额不足，API 调用会被拒绝");
        lines.Add("更新于 "+DateTime.Now.ToString("HH:mm:ss"));
        return new Meter("Kimi",Money(currency,available.Value),string.Join("\n",lines)) {Amount=(decimal)available.Value,Currency=currency};
    }
    public static Meter KimiUsage(object result) {
        double? five=null, week=null, month=null; var lines=new List<string>();
        var ratio=Get(result,"usages") as Dictionary<string,object>;
        if(ratio!=null) {
            five=RatioLeft(Get(ratio,"limit_5h"));
            week=RatioLeft(Get(ratio,"limit_7d"));
            month=RatioLeft(Get(ratio,"limit_month_total"));
            string fiveReset=IsoReset(Get(Get(ratio,"limit_5h"),"reset_time"),"5 小时重置 ");
            if(fiveReset!="") lines.Add(fiveReset);
            string weekReset=IsoReset(Get(Get(ratio,"limit_7d"),"reset_time"),"周重置 ");
            if(weekReset!="") lines.Add(weekReset);
        }
        var limits=Get(result,"limits") as object[];
        if(limits!=null) foreach(var item in limits) {
            double? minutes=Num(Get(Get(item,"window"),"duration"));
            bool weekly=minutes.HasValue&&minutes.Value>=10080;
            var detail=Get(item,"detail");
            double? left=Share(Get(detail,"remaining"),Get(detail,"limit"));
            if(left.HasValue) { if(weekly) week=week??left; else five=five??left; }
            string reset=IsoReset(Get(detail,"resetTime"),weekly?"周重置 ":"5 小时重置 ");
            if(reset!="") lines.Add(reset);
        }
        var legacy=Get(result,"usage");
        if(legacy!=null) {
            double? left=Share(Get(legacy,"remaining"),Get(legacy,"limit"));
            if(left.HasValue) week=week??left;
            string reset=IsoReset(Get(legacy,"resetTime"),"周重置 ");
            if(reset!="") lines.Add(reset);
        }
        if(month.HasValue) lines.Add("月额度剩余 "+Pct(month));
        if(!five.HasValue&&!week.HasValue&&!month.HasValue) throw new Exception("接口未返回可识别的订阅额度");
        string value=five.HasValue?"5h "+Pct(five):week.HasValue?"周 "+Pct(week):"月 "+Pct(month);
        lines.Insert(0,"Kimi Code 订阅额度");
        return new Meter("Kimi",value,string.Join("\n",lines)) {FiveHour=five,Weekly=week};
    }
    public static Meter MiniMaxPlan(object result) {
        ApiOk(result);
        var items=Get(result,"model_remains") as object[];
        if(items==null||items.Length==0) throw new Exception("接口未返回套餐额度");
        object general=null;
        foreach(var item in items) if(Str(Get(item,"model_name")).ToLowerInvariant()=="general") { general=item; break; }
        if(general==null) general=items[0];
        double? five=Num(Get(general,"current_interval_remaining_percent"));
        double? week=Num(Get(general,"current_weekly_remaining_percent"));
        var lines=new List<string>();
        string fiveReset=ResetMs(Get(general,"end_time"),"5 小时重置 ");
        string weekReset=ResetMs(Get(general,"weekly_end_time"),"周重置 ");
        if(five.HasValue) lines.Add("5 小时剩余 "+Pct(five));
        if(fiveReset!="") lines.Add(fiveReset);
        if(week.HasValue) lines.Add("周额度剩余 "+Pct(week));
        if(weekReset!="") lines.Add(weekReset);
        if(items.Length>1) foreach(var item in items) {
            if(item==general) continue;
            string name=Str(Get(item,"model_name"));
            double? other=Num(Get(item,"current_interval_remaining_percent"));
            double? otherWeek=Num(Get(item,"current_weekly_remaining_percent"));
            if(other.HasValue||otherWeek.HasValue) lines.Add(name+" 5h "+Pct(other)+" / 周 "+Pct(otherWeek));
        }
        if(!five.HasValue&&!week.HasValue) throw new Exception("接口未返回可识别的套餐额度");
        string value=five.HasValue?"5h "+Pct(five):"周 "+Pct(week);
        return new Meter("MiniMax",value,string.Join("\n",lines)) {FiveHour=five,Weekly=week};
    }
    public static Meter MiniMaxBalance(object result) {
        ApiOk(result);
        var data=Get(result,"data")??result;
        var available=Num(Get(data,"available_amount"));
        if(!available.HasValue) throw new Exception("接口未返回可用余额");
        string currency="CNY"; var lines=new List<string>();
        var cash=Num(Get(data,"cash_balance"));
        var voucher=Num(Get(data,"voucher_balance"));
        var credit=Num(Get(data,"credit_balance"));
        if(cash.HasValue) lines.Add("现金 "+Money(currency,cash.Value));
        if(voucher.HasValue) lines.Add("代金券 "+Money(currency,voucher.Value));
        if(credit.HasValue) lines.Add("赠送 "+Money(currency,credit.Value));
        if(available.Value<=0) lines.Add("余额不足，API 调用会被拒绝");
        lines.Add("更新于 "+DateTime.Now.ToString("HH:mm:ss"));
        return new Meter("MiniMax",Money(currency,available.Value),string.Join("\n",lines)) {Amount=(decimal)available.Value,Currency=currency};
    }
    public static Meter Mimo(object balance,object usage,object detail) {
        var balanceData=Get(balance,"data");
        var usageData=Get(usage,"data");
        var detailData=Get(detail,"data");
        string currency=Str(Get(balanceData,"currency")); if(currency=="") currency="CNY";
        double? amount=Num(Get(balanceData,"balance"));
        double? used=Num(Get(Get(usageData,"monthUsage"),"percent"));
        double? month=used.HasValue?(double?)Math.Max(0,Math.Min(100,100-used.Value*100)):null;
        var lines=new List<string>();
        var items=Get(Get(usageData,"monthUsage"),"items") as object[];
        if(items!=null) foreach(var item in items) {
            string name=Str(Get(item,"name"));
            var usedTokens=Num(Get(item,"used")); var limit=Num(Get(item,"limit"));
            if(name=="month_total_token"&&usedTokens.HasValue&&limit.HasValue&&limit.Value>0) {
                lines.Add("本月 token "+usedTokens.Value.ToString("N0")+" / "+limit.Value.ToString("N0"));
                if(!month.HasValue) month=Math.Max(0,100-usedTokens.Value/limit.Value*100);
            }
        }
        if(month.HasValue) lines.Add("月度额度剩余 "+Pct(month));
        string plan=Str(Get(detailData,"planName")); if(plan=="") plan=Str(Get(detailData,"planCode"));
        if(plan!="") lines.Add("套餐 "+plan);
        string end=Str(Get(detailData,"currentPeriodEnd"));
        if(end!="") lines.Add("周期至 "+end);
        if(Get(detailData,"expired") is bool && (bool)Get(detailData,"expired")) lines.Add("套餐已过期");
        if(amount.HasValue) {
            lines.Add("余额 "+Money(currency,amount.Value));
            var cash=Num(Get(balanceData,"cashBalance")); if(cash.HasValue) lines.Add("现金 "+Money(currency,cash.Value));
            var gift=Num(Get(balanceData,"giftBalance")); if(gift.HasValue) lines.Add("赠金 "+Money(currency,gift.Value));
        }
        if(!month.HasValue&&!amount.HasValue) throw new Exception("接口未返回可识别的额度");
        string value=month.HasValue?"月 "+Pct(month):Money(currency,amount.Value);
        return new Meter("MiMo",value,string.Join("\n",lines)) {Weekly=month,Amount=amount.HasValue?(decimal?)(decimal)amount.Value:null,Currency=currency};
    }
    public static async Task<object> Fetch(string url,string key,bool bearer) {
        using(var handler=new HttpClientHandler { AllowAutoRedirect=false })
        using(var client=new HttpClient(handler)) {
            client.Timeout=TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization",(bearer?"Bearer ":"")+key);
            using(var response=await client.GetAsync(url)) {
                if(!response.IsSuccessStatusCode) throw new Exception("HTTP "+(int)response.StatusCode+((int)response.StatusCode==401?"：请检查 API Key":"：稍后重试"));
                return Json().DeserializeObject(await response.Content.ReadAsStringAsync());
            }
        }
    }
}

