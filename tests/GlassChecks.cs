using System;
using System.Windows;
using System.Windows.Media;

class GlassChecks {
    static int step;
    static void Check(bool ok){if(!ok)throw new Exception("Glass checks failed at #"+step);step++;}

    public static void Run(){
        var credit=Data.Glm(Data.Json().DeserializeObject("{\"data\":{\"limits\":[{\"type\":\"CREDIT_LIMIT\",\"unit\":3,\"number\":5,\"percentage\":9},{\"type\":\"CREDIT_LIMIT\",\"unit\":6,\"number\":1,\"percentage\":41}]}}"));Check(credit.FiveHour==91&&credit.Weekly==59);
        var raw=Data.Glm(Data.Json().DeserializeObject("[{\"type\":\"CREDIT_LIMIT\",\"unit\":3,\"number\":5,\"remaining\":80,\"usage\":100}]"));Check(raw.FiveHour==80&&raw.Weekly==null);
        var json="{\"date\":\""+DateTime.Now.ToString("yyyy-MM-dd")+"\",\"costs\":[{\"currency\":\"CNY\",\"amount\":\"0.10\"},{\"currency\":\"CNY\",\"amount\":\"0.20\"}],\"usage\":[{\"inputHit\":100,\"inputMiss\":20,\"output\":10}],\"wallets\":[{\"currency\":\"CNY\",\"amount\":\"12.00\"},{\"currency\":\"CNY\",\"amount\":\"2.00\"}]}";
        var snapshot=ConsoleSnapshot.Parse(Data.Json().DeserializeObject(json));Check(snapshot.Usage.Tokens==130);Check(snapshot.Usage.DisplayCost=="¥0.30");Check(snapshot.Usage.CnyCost==.30m);Check(snapshot.Balance.Value=="¥14.00");
        bool rejected=false;try{ConsoleSnapshot.Parse(Data.Json().DeserializeObject(json.Replace(DateTime.Now.ToString("yyyy-MM-dd"),"2000-01-01")));}catch{rejected=true;}Check(rejected);
        double distance;Check(GlassBar.Nearest(new Rect(-1920,0,1920,1080),-122,200,132,out distance)=="right"&&distance==0);
        Check(GlassBar.Nearest(new Rect(0,0,1920,1080),-7.5,100,99,out distance,.75)=="left"&&distance==0);
        Check(GlassBar.Nearest(new Rect(0,0,1920,1080),200,-5.25,258,out distance,.75)=="top"&&distance==0);
        Check(Paint.Expense(0).G>Paint.Expense(0).R);Check(Paint.Expense(5).R>Paint.Expense(5).G);Check(Paint.Expense(5.01m).R<Paint.Expense(5).R);Check(Paint.Expense(5.01m)==Paint.Expense(10));Check(Paint.Expense(2.5m)!=Paint.Expense(1));
        for(int p=1;p<=100;p++){var a=Paint.Quota(p-1);var b=Paint.Quota(p);Check(Math.Abs(a.R-b.R)<=3&&Math.Abs(a.G-b.G)<=3&&Math.Abs(a.B-b.B)<=3);}
        var defaults=Limits.From(null,null,null);
        Check(defaults.QuotaWarn==50&&defaults.CostWarn==2.5&&defaults.CostDanger==5);
        var tuned=Limits.From(80,1,10);
        Check(tuned.QuotaWarn==80&&tuned.CostWarn==1&&tuned.CostDanger==10);
        var repaired=Limits.From(0,-1,0);
        Check(repaired.QuotaWarn==50&&repaired.CostWarn==2.5&&repaired.CostDanger>=repaired.CostWarn+1);
        var widened=Limits.From(30,8,null);
        Check(widened.CostDanger==9);
        Check(Paint.Quota(100,80)==Paint.Quota(100));
        Check(Paint.Quota(60,80)!=Paint.Quota(60,50));
        Check(Paint.Quota(80,80)==Paint.Quota(50,50));
        Check(Paint.Quota(70,80).G<Paint.Quota(90,80).G);
        Check(Paint.Quota(70,80).R>Paint.Quota(90,80).R);
        Check(Paint.Expense(1,1,10)!=Paint.Expense(1));
        Check(Paint.Expense(9,2,10)!=Paint.Expense(9,2,5));
        Check(Paint.Expense(0,0,5)==Paint.Expense(0));
        var config=Data.Json().Deserialize<Config>(Data.Json().Serialize(new Config{QuotaWarn=80,CostWarn=1.5,CostDanger=4}));
        Check(config.QuotaWarn==80&&config.CostWarn==1.5&&config.CostDanger==4);
        var legacyConfig=Data.Json().Deserialize<Config>("{\"Glm\":\"k\"}");
        Check(legacyConfig.QuotaWarn==null&&Limits.From(legacyConfig).QuotaWarn==50);
        Limits parsed;string error;
        Check(Limits.TryParse("80","1","10",out parsed,out error)&&error==null&&parsed.CostDanger==10);
        Check(!Limits.TryParse("0","1","10",out parsed,out error)&&error!=null);
        Check(!Limits.TryParse("50","5","5",out parsed,out error)&&error!=null);
        Check(!Limits.TryParse("abc","1","2",out parsed,out error)&&error!=null);

        var kimi=Data.KimiBalance(Data.Json().DeserializeObject("{\"code\":0,\"data\":{\"available_balance\":49.58894,\"voucher_balance\":46.58893,\"cash_balance\":3.00001},\"status\":true}"),false);
        Check(kimi.Value=="¥49.59");
        var kimiUsage=Data.KimiUsage(Data.Json().DeserializeObject("{\"limits\":[{\"window\":{\"duration\":300,\"timeUnit\":\"TIME_UNIT_MINUTE\"},\"detail\":{\"limit\":\"100\",\"remaining\":\"100\",\"resetTime\":\"2026-10-17T10:25:53Z\"}}],\"usages\":{\"limit_5h\":{\"used_ratio\":0.25,\"reset_time\":\"2026-10-17T10:25:53Z\"},\"limit_7d\":{\"used_ratio\":0.1,\"reset_time\":\"2026-10-23T08:25:53Z\"}}}"));
        Check(kimiUsage.FiveHour==75&&kimiUsage.Weekly==90);
        var kimiLegacy=Data.KimiUsage(Data.Json().DeserializeObject("{\"usage\":{\"limit\":\"2048\",\"used\":\"214\",\"remaining\":\"1834\"},\"limits\":[{\"window\":{\"duration\":300},\"detail\":{\"limit\":\"200\",\"used\":\"139\",\"remaining\":\"61\"}}]}"));
        Check(kimiLegacy.FiveHour.HasValue&&Math.Abs(kimiLegacy.FiveHour.Value-30.5)<.01);
        Check(kimiLegacy.Weekly.HasValue&&Math.Abs(kimiLegacy.Weekly.Value-89.55)<.01);
        var minimax=Data.MiniMaxPlan(Data.Json().DeserializeObject("{\"base_resp\":{\"status_code\":0},\"model_remains\":[{\"model_name\":\"general\",\"current_interval_remaining_percent\":30,\"current_weekly_remaining_percent\":61,\"end_time\":1787641200000,\"weekly_end_time\":1788200000000}]}"));
        Check(minimax.FiveHour==30&&minimax.Weekly==61);
        var minimaxBalance=Data.MiniMaxBalance(Data.Json().DeserializeObject("{\"base_resp\":{\"status_code\":0},\"available_amount\":\"12.34\",\"cash_balance\":\"10.00\"}"));
        Check(minimaxBalance.Value=="¥12.34");
        var mimo=Data.Mimo(
            Data.Json().DeserializeObject("{\"code\":0,\"data\":{\"balance\":\"50.00\",\"cashBalance\":\"30.00\",\"giftBalance\":\"20.00\",\"currency\":\"CNY\"}}"),
            Data.Json().DeserializeObject("{\"code\":0,\"data\":{\"monthUsage\":{\"percent\":0.1,\"items\":[{\"name\":\"month_total_token\",\"used\":100,\"limit\":1000,\"percent\":0.1}]}}}"),
            Data.Json().DeserializeObject("{\"code\":0,\"data\":{\"planName\":\"Standard\",\"currentPeriodEnd\":\"2027-09-04 23:59:59\",\"expired\":false}}"));
        Check(mimo.Weekly.HasValue&&Math.Abs(mimo.Weekly.Value-90)<.01);
        Check(mimo.Value=="月 90%");

        Check(Fetch.WantsPlan("auto","sk-kimi-abc")&&Fetch.WantsPlan("auto","sk-cp-abc"));
        Check(!Fetch.WantsPlan("auto","sk-api-abc")&&!Fetch.WantsPlan("balance","sk-cp-abc"));
        var hiddenConfig=new Config{Hidden="kimi, mimo"};
        Check(hiddenConfig.IsHidden("kimi")&&hiddenConfig.IsHidden("MIMO")&&!hiddenConfig.IsHidden("glm"));
        Check(!Catalog.Configured(Catalog.Get("kimi"),new Config{Kimi="sk-test",Hidden="kimi"}));
        Check(Catalog.Configured(Catalog.Get("kimi"),new Config{Kimi="sk-test"}));
        Check(Paint.HasSvg("kimi")&&Paint.HasSvg("kimi-color")&&Paint.HasSvg("minimax")&&Paint.HasSvg("minimax-color"));

        var face=new BarFace();
        face.Primary=new Row{Id="glm",Label="ZHIPU",Meter=new Meter("GLM","",""){FiveHour=80,Weekly=50}};
        face.Rows.Add(new Row{Id="kimi",Label="KIMI",Meter=new Meter("Kimi","¥9.00","")});
        face.Width=132;face.Height=face.ContentHeight;
        Check(face.Hit(new Point(66,90))==1);
        Check(face.Hit(new Point(66,150))==100);
        Check(face.Hit(new Point(30,face.Height-20))==3);
        Check(face.Hit(new Point(100,face.Height-20))==4);
        face.Horizontal=true;face.Width=face.ContentWidth;
        Check(face.Hit(new Point(60,60))==1);
        Check(face.Hit(new Point(130,60))==100);
        face.Horizontal=false;face.Width=132;

        var stripFace=new TaskbarFace();
        stripFace.Rows.Add(new Row{Id="glm",Label="ZHIPU",Meter=new Meter("GLM","5h 82%",""){FiveHour=82}});
        stripFace.Rows.Add(new Row{Id="kimi",Label="KIMI",Meter=new Meter("Kimi","¥9.00","")});
        Check(Math.Abs(stripFace.ContentWidth-(10+2*TaskbarFace.TileWidth))<.01);
        Check(stripFace.Hit(new Point(20,20))==0);
        Check(stripFace.Hit(new Point(10+TaskbarFace.TileWidth+20,20))==1);
        Check(stripFace.Hit(new Point(4,20))==-1);
        Check(stripFace.Hit(new Point(10+2*TaskbarFace.TileWidth+10,20))==-1);

        var deepRow=new Row{Id="deepseek",Label="DEEPSEEK",Meter=new Meter("DeepSeek","¥1.00",""),Usage=new TodayUsage{Date=DateTime.Now.ToString("yyyy-MM-dd"),Currency="CNY",Cost=4m}};
        var inkFace=new BarFace();
        var normal=((SolidColorBrush)inkFace.SpendInk(deepRow)).Color;
        inkFace.Limits=new Limits{CostWarn=1,CostDanger=2};
        Check(((SolidColorBrush)inkFace.SpendInk(deepRow)).Color!=normal);
        Check(BarFace.SpendText(deepRow)=="今日 ¥4.00");

        var estRow=new Row{Id="kimi",Label="KIMI",Meter=new Meter("Kimi","¥48.00",""),Spend="今日 ≈¥1.50",SpendCost=1.5m};
        Check(BarFace.SpendText(estRow)=="今日 ≈¥1.50");
        Check(BarFace.SpendText(new Row{Id="kimi",Label="KIMI",Meter=new Meter("Kimi","","")})=="今日 —");

        string logPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"tb-dailylog-"+Guid.NewGuid().ToString("N")+".json");
        var log=DailyLog.Load(logPath);
        Check(log.Sample("kimi",100m)==0m);
        Check(log.Sample("kimi",98.5m)==1.5m);
        Check(log.Sample("kimi",108.5m)==1.5m);
        Check(log.Sample("kimi",107m)==3m);
        var reloaded=DailyLog.Load(logPath);
        Check(reloaded.Sample("kimi",107m)==3m);
        System.IO.File.Delete(logPath);

        var kimiBalance=Data.KimiBalance(Data.Json().DeserializeObject("{\"code\":0,\"data\":{\"available_balance\":49.5,\"voucher_balance\":46.5,\"cash_balance\":3.0}}"),false);
        Check(kimiBalance.Amount.HasValue&&Math.Abs((double)kimiBalance.Amount.Value-49.5)<.001&&kimiBalance.Currency=="CNY");
    }
}
