using System;
using System.Windows;
class GlassChecks {
    static void Check(bool ok){if(!ok)throw new Exception("Glass checks failed");}
    public static void Run(){
        var credit=Data.Glm(Data.Json().DeserializeObject("{\"data\":{\"limits\":[{\"type\":\"CREDIT_LIMIT\",\"unit\":3,\"number\":5,\"percentage\":9},{\"type\":\"CREDIT_LIMIT\",\"unit\":6,\"number\":1,\"percentage\":41}]}}"));Check(credit.FiveHour==91&&credit.Weekly==59);
        var raw=Data.Glm(Data.Json().DeserializeObject("[{\"type\":\"CREDIT_LIMIT\",\"unit\":3,\"number\":5,\"remaining\":80,\"usage\":100}]"));Check(raw.FiveHour==80&&raw.Weekly==null);
        var json="{\"date\":\""+DateTime.Now.ToString("yyyy-MM-dd")+"\",\"costs\":[{\"currency\":\"CNY\",\"amount\":\"0.10\"},{\"currency\":\"CNY\",\"amount\":\"0.20\"}],\"usage\":[{\"inputHit\":100,\"inputMiss\":20,\"output\":10}],\"wallets\":[{\"currency\":\"CNY\",\"amount\":\"12.00\"},{\"currency\":\"CNY\",\"amount\":\"2.00\"}]}";
        var snapshot=ConsoleSnapshot.Parse(Data.Json().DeserializeObject(json));Check(snapshot.Usage.Tokens==130);Check(snapshot.Usage.DisplayCost=="¥0.30");Check(snapshot.Usage.CnyCost==.30m);Check(snapshot.Balance.Value=="¥14.00");
        bool rejected=false;try{ConsoleSnapshot.Parse(Data.Json().DeserializeObject(json.Replace(DateTime.Now.ToString("yyyy-MM-dd"),"2000-01-01")));}catch{rejected=true;}Check(rejected);
        var face=new BarFace{Horizontal=true};Check(face.Hit(new Point(60,60))==1);Check(face.Hit(new Point(200,60))==2);Check(face.Hit(new Point(310,35))==3);face.Horizontal=false;Check(face.Hit(new Point(66,85))==1);Check(face.Hit(new Point(66,230))==2);
        var soloDs=new BarFace{Horizontal=true,ShowGlm=false};Check(soloDs.TargetWidth==238);Check(soloDs.Hit(new Point(60,60))==2);soloDs.Horizontal=false;Check(soloDs.TargetHeight==208);Check(soloDs.Hit(new Point(66,85))==2);
        var soloGlm=new BarFace{ShowDeepSeek=false};Check(soloGlm.TargetHeight==181);Check(soloGlm.Hit(new Point(50,230))==3);
        var empty=new BarFace{ShowGlm=false,ShowDeepSeek=false};Check(empty.TargetHeight==123);Check(empty.Hit(new Point(50,85))==3);empty.Horizontal=true;Check(empty.TargetWidth==139);
        double distance;Check(GlassBar.Nearest(new Rect(-1920,0,1920,1080),-122,200,132,out distance)=="right"&&distance==0);
        Check(GlassBar.Nearest(new Rect(0,0,1920,1080),-7.5,100,99,out distance,.75)=="left"&&distance==0);
        Check(GlassBar.Nearest(new Rect(0,0,1920,1080),200,-5.25,258,out distance,.75)=="top"&&distance==0);
        Check(Paint.Expense(0).G>Paint.Expense(0).R);Check(Paint.Expense(5).R>Paint.Expense(5).G);Check(Paint.Expense(5.01m).R<Paint.Expense(5).R);Check(Paint.Expense(5.01m)==Paint.Expense(10));Check(Paint.Expense(2.5m)!=Paint.Expense(1));
        for(int p=1;p<=100;p++){var a=Paint.Quota(p-1);var b=Paint.Quota(p);Check(Math.Abs(a.R-b.R)<=3&&Math.Abs(a.G-b.G)<=3&&Math.Abs(a.B-b.B)<=3);}
    }
}
