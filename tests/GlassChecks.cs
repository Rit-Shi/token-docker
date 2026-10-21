using System;
using System.Windows;
class GlassChecks {
    static int step;
    static void Check(bool ok){if(!ok)throw new Exception("Glass checks failed at #"+step);step++;}
    public static void Run(){
        var credit=Data.Glm(Data.Json().DeserializeObject("{\"data\":{\"limits\":[{\"type\":\"CREDIT_LIMIT\",\"unit\":3,\"number\":5,\"percentage\":9},{\"type\":\"CREDIT_LIMIT\",\"unit\":6,\"number\":1,\"percentage\":41}]}}"));Check(credit.FiveHour==91&&credit.Weekly==59);
        var raw=Data.Glm(Data.Json().DeserializeObject("[{\"type\":\"CREDIT_LIMIT\",\"unit\":3,\"number\":5,\"remaining\":80,\"usage\":100}]"));Check(raw.FiveHour==80&&raw.Weekly==null);
        var json="{\"date\":\""+DateTime.Now.ToString("yyyy-MM-dd")+"\",\"costs\":[{\"currency\":\"CNY\",\"amount\":\"0.10\"},{\"currency\":\"CNY\",\"amount\":\"0.20\"}],\"usage\":[{\"inputHit\":100,\"inputMiss\":20,\"output\":10}],\"wallets\":[{\"currency\":\"CNY\",\"amount\":\"12.00\"},{\"currency\":\"CNY\",\"amount\":\"2.00\"}]}";
        var snapshot=ConsoleSnapshot.Parse(Data.Json().DeserializeObject(json));Check(snapshot.Usage.Tokens==130);Check(snapshot.Usage.DisplayCost=="¥0.30");Check(snapshot.Usage.CnyCost==.30m);Check(snapshot.Balance.Value=="¥14.00");
        bool rejected=false;try{ConsoleSnapshot.Parse(Data.Json().DeserializeObject(json.Replace(DateTime.Now.ToString("yyyy-MM-dd"),"2000-01-01")));}catch{rejected=true;}Check(rejected);
        var face=new BarFace{Horizontal=true};Check(face.Hit(new Point(60,60))==1);Check(face.Hit(new Point(200,60))==2);Check(face.Hit(new Point(310,35))==3);face.Horizontal=false;Check(face.Hit(new Point(66,85))==1);Check(face.Hit(new Point(66,230))==2);
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
        var legacy=Data.Json().Deserialize<Config>("{\"Glm\":\"k\"}");
        Check(legacy.QuotaWarn==null&&Limits.From(legacy).QuotaWarn==50);
        Limits parsed;string error;
        Check(Limits.TryParse("80","1","10",out parsed,out error)&&error==null&&parsed.CostDanger==10);
        Check(!Limits.TryParse("0","1","10",out parsed,out error)&&error!=null);
        Check(!Limits.TryParse("50","5","5",out parsed,out error)&&error!=null);
        Check(!Limits.TryParse("abc","1","2",out parsed,out error)&&error!=null);
        var strict=new BarFace{Usage=new TodayUsage{Date=DateTime.Now.ToString("yyyy-MM-dd"),Currency="CNY",Cost=4m}};
        var normalInk=((System.Windows.Media.SolidColorBrush)strict.SpendInk).Color;
        strict.Limits=new Limits{CostWarn=1,CostDanger=2};
        Check(((System.Windows.Media.SolidColorBrush)strict.SpendInk).Color!=normalInk);
    }
}
