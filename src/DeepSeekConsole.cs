using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

class ConsoleSnapshot {
    public Meter Balance;public TodayUsage Usage;
    static IEnumerable Rows(object obj){var rows=obj as IEnumerable;if(rows==null || obj is string)throw new Exception("Missing array");return rows;}
    static decimal Money(object obj){decimal n;if(!decimal.TryParse(Data.Str(obj),NumberStyles.Number,CultureInfo.InvariantCulture,out n))throw new Exception("Invalid cost");return n;}
    public static ConsoleSnapshot Parse(object message){if(Data.Str(Data.Get(message,"date"))!=DateTime.Now.ToString("yyyy-MM-dd"))throw new Exception("Not today");bool balanceOnly=Data.Str(Data.Get(message,"type"))=="tokenbar-balance";
        var balances=new Dictionary<string,decimal>();var costs=new Dictionary<string,decimal>();long tokens=0;
        foreach(var wallet in Rows(Data.Get(message,"wallets"))){string c=Data.Str(Data.Get(wallet,"currency"));if(c!="CNY"&&c!="USD")throw new Exception("Unknown currency");if(!balances.ContainsKey(c))balances[c]=0;balances[c]+=Money(Data.Get(wallet,"amount"));}
        foreach(var cost in Rows(balanceOnly?new object[0]:Data.Get(message,"costs"))){string c=Data.Str(Data.Get(cost,"currency"));if(c!="CNY"&&c!="USD")throw new Exception("Unknown currency");if(!costs.ContainsKey(c))costs[c]=0;costs[c]+=Money(Data.Get(cost,"amount"));}
        foreach(var usage in Rows(balanceOnly?new object[0]:Data.Get(message,"usage")))foreach(string k in new[]{"inputHit","inputMiss","output"}){long n;if(!long.TryParse(Data.Str(Data.Get(usage,k)),out n)||n<0)throw new Exception("Invalid tokens");tokens=checked(tokens+n);}
        if(balances.Count==0)throw new Exception("Missing balances");
        var balanceLines=new List<string>();foreach(var entry in balances)balanceLines.Add((entry.Key=="CNY"?"¥":"$")+entry.Value.ToString("0.00"));
        var costLines=new List<string>();foreach(var currency in balances.Keys)costLines.Add((currency=="CNY"?"¥":"$")+(costs.ContainsKey(currency)?costs[currency]:0).ToString("0.00"));
        foreach(var currency in costs.Keys)if(!balances.ContainsKey(currency))costLines.Add((currency=="CNY"?"¥":"$")+costs[currency].ToString("0.00"));
        return new ConsoleSnapshot{Balance=new Meter("DeepSeek",string.Join(" / ",balanceLines),"DeepSeek 控制台账号 · 含赠金和充值余额\n更新于 "+DateTime.Now.ToString("HH:mm:ss")),Usage=balanceOnly?null:new TodayUsage{Date=DateTime.Now.ToString("yyyy-MM-dd"),Tokens=tokens,CnyCost=costs.ContainsKey("CNY")?costs["CNY"]:balances.ContainsKey("CNY")?(decimal?)0:null,DisplayCost=string.Join(" / ",costLines),Source="DeepSeek 控制台 · 所有 API Key · 本地时区",UpdatedAt=Data.Str(Data.Get(message,"updatedAt"))}};
    }
}
class DeepSeekConsole:Window {
    WebView2 browser=new WebView2();Action<ConsoleSnapshot> receive;Action<string> failure;bool disposing;
    public bool Ready {get;private set;}
    public DeepSeekConsole(Window owner,Action<ConsoleSnapshot> onData,Action<string> onFailure){Owner=owner;ShowInTaskbar=false;Icon=owner.Icon;receive=onData;failure=onFailure;Title="连接 DeepSeek 控制台 — 请在官网登录，完成后可关闭此窗口";Width=1040;Height=760;WindowStartupLocation=WindowStartupLocation.CenterOwner;Content=browser;Closing+=(s,e)=>{if(!disposing){e.Cancel=true;Hide();}};}
    public async Task Start(){var root=AppDomain.CurrentDomain.BaseDirectory;var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"console-profile"));await browser.EnsureCoreWebView2Async(env);
        browser.CoreWebView2.NewWindowRequested+=(s,e)=>{e.Handled=true;};
        browser.CoreWebView2.WebMessageReceived+=(s,e)=>{try{if(new Uri(e.Source).Host!="platform.deepseek.com")return;var obj=Data.Json().DeserializeObject(e.WebMessageAsJson);string type=Data.Str(Data.Get(obj,"type"));if(type=="tokenbar-usage"||type=="tokenbar-balance"){receive(ConsoleSnapshot.Parse(obj));Log(type+" OK");}else if(type=="tokenbar-error"){string reason=Data.Str(Data.Get(obj,"reason"));Log(reason);failure(reason);}}catch(Exception error){string reason="parse: "+error.Message;Log(reason);failure(reason);}};
        using(var s=typeof(DeepSeekConsole).Assembly.GetManifestResourceStream("console-bridge.js"))using(var reader=new StreamReader(s))await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(reader.ReadToEnd());
        browser.CoreWebView2.NavigationCompleted+=(s,e)=>{if(!e.IsSuccess&&e.WebErrorStatus!=CoreWebView2WebErrorStatus.OperationCanceled)failure("navigation: "+e.WebErrorStatus);};Ready=true;browser.Source=new Uri("https://platform.deepseek.com/usage");}
    static void Log(string message){try{string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"connection-status.txt");if(File.Exists(path)&&new FileInfo(path).Length>32000)File.WriteAllText(path,"");File.AppendAllText(path,DateTime.Now.ToString("HH:mm:ss")+" "+message+Environment.NewLine);}catch{}}
    public async Task Refresh(){if(Ready)await browser.ExecuteScriptAsync("window.__tokenbarRefresh && window.__tokenbarRefresh()");}
    public void Shutdown(){disposing=true;browser.Dispose();Close();}
}
