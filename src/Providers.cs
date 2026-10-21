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
    public Meter(string n, string v, string d, double? r = null) { Name=n; Value=v; Detail=d; Remaining=r; }
}
class Config {
    public string Glm="", DeepSeek="";
    public bool International=false;
    public int X=-1, Y=30; public string Theme="dark", Edge="right", UsageFile=""; public bool ConsoleConnected=false;
    // Colour thresholds; null keeps the historical defaults (see Limits).
    public double? QuotaWarn, CostWarn, CostDanger;
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
        if(balances!=null) foreach(var item in balances) {
            string currency=Str(Get(item,"currency")); decimal amount;
            if(!decimal.TryParse(Str(Get(item,"total_balance")),NumberStyles.Number,CultureInfo.InvariantCulture,out amount)) continue;
            values.Add((currency=="CNY"?"¥":currency=="USD"?"$":currency+" ")+amount.ToString("0.00",CultureInfo.InvariantCulture));
            lines.Add(currency+" 可用 "+amount.ToString("0.00")+"；赠金 "+Str(Get(item,"granted_balance"))+"；充值 "+Str(Get(item,"topped_up_balance")));
        }
        if(values.Count==0) throw new Exception("接口未返回有效余额");
        return new Meter("DeepSeek",string.Join(" / ",values),string.Join("\n",lines)+(Get(result,"is_available") is bool && !(bool)Get(result,"is_available")?"\n当前不可用于 API 调用":""));
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

