using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Xml;
using WinForms=System.Windows.Forms;

static class Paint {
    public static Color C(string hex){return (Color)ColorConverter.ConvertFromString(hex);}
    public static Brush B(string hex){return new SolidColorBrush(C(hex));}
    public static Color Mix(Color a,Color b,double t){t=Math.Max(0,Math.Min(1,t));return Color.FromRgb((byte)Math.Round(a.R+(b.R-a.R)*t),(byte)Math.Round(a.G+(b.G-a.G)*t),(byte)Math.Round(a.B+(b.B-a.B)*t));}
    public static Color Quota(double? p){if(!p.HasValue)return C("#7F8B9E");return p<=50?Mix(C("#F27179"),C("#F3C469"),p.Value/50):Mix(C("#F3C469"),C("#65DDB0"),(p.Value-50)/50);}
    public static Color Expense(decimal? cost){if(!cost.HasValue)return C("#7F8B9E");if(cost>5)return C("#BB304B");double p=(double)Math.Max(0,cost.Value);return p<=2.5?Mix(C("#65DDB0"),C("#F3C469"),p/2.5):Mix(C("#F3C469"),C("#F27179"),(p-2.5)/2.5);}
    public static void Text(DrawingContext d,string text,double size,Brush ink,Point at,bool bold=false,double max=300){var f=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI, Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,ink);f.MaxTextWidth=max;f.Trimming=TextTrimming.CharacterEllipsis;d.DrawText(f,at);}
    public static Geometry Icon(string name){using(var s=typeof(Paint).Assembly.GetManifestResourceStream(name+".svg")){var xml=new XmlDocument{XmlResolver=null};xml.Load(s);var group=new GeometryGroup{FillRule=FillRule.EvenOdd};foreach(XmlNode p in xml.GetElementsByTagName("path"))group.Children.Add(Geometry.Parse(p.Attributes["d"].Value));group.Freeze();return group;}}
    public static void Logo(DrawingContext d,Geometry shape,Brush brush,Rect r){d.PushTransform(new TranslateTransform(r.X,r.Y));d.PushTransform(new ScaleTransform(r.Width/24,r.Height/24));d.DrawGeometry(brush,null,shape);d.Pop();d.Pop();}
    public static void Arc(DrawingContext d,Point center,double radius,double? value,Brush track){var pen=new Pen(track,5);d.DrawEllipse(null,pen,center,radius,radius);if(!value.HasValue || value<=0)return;pen=new Pen(new SolidColorBrush(Quota(value)),5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};if(value>=100){d.DrawEllipse(null,pen,center,radius,radius);return;}double angle=value.Value*2*Math.PI/100;var geo=new StreamGeometry();using(var c=geo.Open()){c.BeginFigure(new Point(center.X,center.Y-radius),false,false);c.ArcTo(new Point(center.X+radius*Math.Sin(angle),center.Y-radius*Math.Cos(angle)),new Size(radius,radius),0,value>50,SweepDirection.Clockwise,true,false);}d.DrawGeometry(null,pen,geo);}
}
class TodayUsage {
    public string Date, Currency, UpdatedAt, Scope="today", Source,DisplayCost;
    public decimal? Cost,CnyCost;
    public long? Tokens;
    public static TodayUsage Load(string file){if(string.IsNullOrWhiteSpace(file)||!File.Exists(file))return null;var x=Data.Json().Deserialize<TodayUsage>(File.ReadAllText(file));if(x==null||x.Date!=DateTime.Now.ToString("yyyy-MM-dd")||x.Scope!="today"||x.Cost<0||x.Tokens<0||(x.Currency!="CNY"&&x.Currency!="USD"))return null;return x;}
}
class BarFace:FrameworkElement {
    public bool Light,Horizontal; public Meter Plan=new Meter("GLM","未配置","请在设置中连接 GLM 套餐"),Balance=new Meter("DeepSeek","未配置","请在设置中连接 DeepSeek");
    public TodayUsage Usage; public string Edge="right";public double Pull;public int Hover=-1;public bool Busy;
    static Geometry zhipu=Paint.Icon("zhipu"),deepseek=Paint.Icon("deepseek");
    public Brush Ink {get{return Paint.B(Light?"#20314A":"#F0F5FF");}}
    public Brush Muted {get{return Paint.B(Light?"#5E718A":"#9CADC7");}}
    public string Spend {get{return Usage!=null&&Usage.Date==DateTime.Now.ToString("yyyy-MM-dd")?(Usage.DisplayCost??(Usage.Cost.HasValue?(Usage.Currency=="CNY"?"¥":"$")+Usage.Cost.Value.ToString("0.00"):"—")):"—";}}
    public Brush SpendInk {get{return new SolidColorBrush(Paint.Expense(Usage!=null&&Usage.Date==DateTime.Now.ToString("yyyy-MM-dd")?(Usage.CnyCost??(Usage.Currency=="CNY"?Usage.Cost:null)):null));}}
    public int Hit(Point p){if(Horizontal){if(p.Y<18)return 0;if(p.X<115)return 1;if(p.X<282)return 2;return p.Y<58?3:4;}if(p.Y<37)return 0;if(p.Y<135)return 1;if(p.Y<260)return 2;if(p.Y<303)return p.X<66?3:4;return 0;}
    public Geometry Shell(){double r=27,l=10,t=7,w=112,h=293,right=l+w,bottom=t+h;double pull=Math.Min(8,Math.Abs(Pull));var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new Point(l+r,t),true,true);c.LineTo(new Point(right-r,t),true,false);c.ArcTo(new Point(right,t+r),new Size(r,r),0,false,SweepDirection.Clockwise,true,false);c.LineTo(new Point(right,bottom-r),true,false);c.ArcTo(new Point(right-r,bottom),new Size(r,r),0,false,SweepDirection.Clockwise,true,false);c.LineTo(new Point(l+r,bottom),true,false);c.ArcTo(new Point(l,bottom-r),new Size(r,r),0,false,SweepDirection.Clockwise,true,false);c.LineTo(new Point(l,t+r),true,false);c.ArcTo(new Point(l+r,t),new Size(r,r),0,false,SweepDirection.Clockwise,true,false);}return g;}
    protected override void OnRender(DrawingContext d){base.OnRender(d);Render(d);}
    public void Render(DrawingContext d){
        if(Horizontal){RenderHorizontal(d);return;}
        var shell=Shell();var glass=new LinearGradientBrush();glass.StartPoint=new Point(0,0);glass.EndPoint=new Point(1,1);glass.GradientStops.Add(new GradientStop(Paint.C(Light?"#DDF8FCFF":"#DF253247"),0));glass.GradientStops.Add(new GradientStop(Paint.C(Light?"#BCE3EFFA":"#BC111E30"),.65));glass.GradientStops.Add(new GradientStop(Paint.C(Light?"#DCF6F8FF":"#DE27354A"),1));
        d.PushTransform(new TranslateTransform(0,3));d.DrawGeometry(Paint.B("#25000000"),null,shell);d.Pop();d.DrawGeometry(glass,new Pen(Paint.B(Light?"#EFFFFFFF":"#707F96B3"),1),shell);
        d.PushClip(shell);d.DrawEllipse(Paint.B(Light?"#38FFFFFF":"#117FC6FF"),null,new Point(36,10),88,70);d.Pop();
        var grip=new Pen(Muted,2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};d.DrawLine(grip,new Point(55,23),new Point(77,23));
        Brush track=Paint.B(Light?"#284A657C":"#445F718B");Paint.Arc(d,new Point(66,77),35,Plan.FiveHour,track);Paint.Arc(d,new Point(66,77),27,Plan.Weekly,track);Paint.Logo(d,zhipu,Ink,new Rect(49,60,34,34));
        Paint.Text(d,"ZHIPU",9,Muted,new Point(48,115),true);
        d.DrawLine(new Pen(Paint.B(Light?"#30466182":"#405D7696"),1),new Point(31,135),new Point(101,135));
        Paint.Logo(d,deepseek,Paint.B(Light?"#466AFF":"#91ADFF"),new Rect(46,148,40,32));
        Paint.Text(d,"余额",9,Muted,new Point(26,195));Paint.Text(d,Balance.Value, Balance.Value.Length>9?10:14,Ink,new Point(52,189),true,65);
        Paint.Text(d,"今日",9,Muted,new Point(26,222));Paint.Text(d,Spend,12,SpendInk,new Point(53,216),true,65);
        if(Usage==null)Paint.Text(d,"用量待接入",8,Muted,new Point(43,241),false,70);
        d.DrawLine(new Pen(Paint.B(Light?"#30466182":"#405D7696"),1),new Point(31,260),new Point(101,260));
        // Theme switch uses vector sun/moon rather than font glyphs.
        if(Light){d.DrawEllipse(null,new Pen(Muted,1.4),new Point(45,283),4,4);for(int i=0;i<8;i++){double a=i*Math.PI/4;d.DrawLine(new Pen(Muted,1),new Point(45+7*Math.Cos(a),283+7*Math.Sin(a)),new Point(45+9*Math.Cos(a),283+9*Math.Sin(a)));}}else{d.DrawEllipse(Muted,null,new Point(45,283),7,7);d.DrawEllipse(Paint.B("#FF243247"),null,new Point(48,280),6,6);}
        for(int i=0;i<3;i++)d.DrawEllipse(Muted,null,new Point(84,277+i*6),1.3,1.3);
        if(Busy)d.DrawEllipse(Paint.B("#74E0BD"),null,new Point(95,25),2.5,2.5);
        if(Math.Abs(Pull)>.05){double strength=Math.Min(1,Math.Abs(Pull)/8);var pen=new Pen(new SolidColorBrush(Color.FromArgb((byte)(70+150*strength),140,199,255)),1.4);var g=new StreamGeometry();using(var c=g.Open()){if(Edge=="left"){c.BeginFigure(new Point(10,115),false,false);c.BezierTo(new Point(10-7*strength,139),new Point(10-7*strength,195),new Point(10,222),true,false);}else if(Edge=="right"){c.BeginFigure(new Point(122,115),false,false);c.BezierTo(new Point(122+7*strength,139),new Point(122+7*strength,195),new Point(122,222),true,false);}else{c.BeginFigure(new Point(35,7),false,false);c.BezierTo(new Point(49,7-6*strength),new Point(83,7-6*strength),new Point(97,7),true,false);}}d.DrawGeometry(null,pen,g);}
    }
    void RenderHorizontal(DrawingContext d){var rect=new Rect(7,7,330,104);var gradient=new LinearGradientBrush(Paint.C(Light?"#DDF8FCFF":"#DF253247"),Paint.C(Light?"#BCE3EFFA":"#BC111E30"),65);d.DrawRoundedRectangle(gradient,new Pen(Paint.B(Light?"#EFFFFFFF":"#707F96B3"),1),rect,27,27);Brush track=Paint.B(Light?"#284A657C":"#445F718B");Paint.Arc(d,new Point(64,59),35,Plan.FiveHour,track);Paint.Arc(d,new Point(64,59),27,Plan.Weekly,track);Paint.Logo(d,zhipu,Ink,new Rect(47,42,34,34));d.DrawLine(new Pen(Paint.B(Light?"#30466182":"#405D7696"),1),new Point(113,30),new Point(113,88));Paint.Logo(d,deepseek,Paint.B(Light?"#466AFF":"#91ADFF"),new Rect(126,40,38,34));Paint.Text(d,"余额",10,Muted,new Point(174,35));Paint.Text(d,Balance.Value,14,Ink,new Point(209,30),true,78);Paint.Text(d,"今日",10,Muted,new Point(174,63));Paint.Text(d,Spend,13,SpendInk,new Point(209,59),true,78);d.DrawEllipse(null,new Pen(Muted,1.3),new Point(310,39),6,6);for(int i=0;i<3;i++)d.DrawEllipse(Muted,null,new Point(304+i*6,77),1.2,1.2);if(Math.Abs(Pull)>.05){var pen=new Pen(Paint.B("#BCA0D4FF"),1.5);var geo=new StreamGeometry();using(var c=geo.Open()){c.BeginFigure(new Point(112,7),false,false);c.BezierTo(new Point(142,7-Math.Abs(Pull)),new Point(202,7-Math.Abs(Pull)),new Point(232,7),true,false);}d.DrawGeometry(null,pen,geo);}}
}
class GlassBar:Window {
    const double UiScale=.75; Viewbox compact=new Viewbox{Stretch=Stretch.Uniform};
    static string Root=AppDomain.CurrentDomain.BaseDirectory;static string SettingsPath=Path.Combine(Root,"settings.dat");
    Config config=new Config();BarFace face=new BarFace();DispatcherTimer refresh=new DispatcherTimer(),spring=new DispatcherTimer();Popup hover=new Popup{AllowsTransparency=true,StaysOpen=true};DeepSeekConsole console;
    bool busy,dragging,moved,preview;Point start,origin;double velocity;DateTime last=DateTime.UtcNow;string state="未刷新";
    public GlassBar(bool demo=false){preview=demo;Title="Token Bar · Glass";Width=132*UiScale;Height=306*UiScale;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;Topmost=true;ShowInTaskbar=false;
        if(!demo)try{if(File.Exists(SettingsPath))config=Data.Json().Deserialize<Config>(System.Text.Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(File.ReadAllBytes(SettingsPath),null,System.Security.Cryptography.DataProtectionScope.CurrentUser)));}catch{state="无法读取设置，请重新连接";}
        face.Width=132;face.Height=306;compact.Child=face;Content=compact;face.Light=config.Theme=="light";face.Edge=config.Edge??"right";Orient(face.Edge);
        refresh.Interval=TimeSpan.FromMinutes(5);refresh.Tick+=async(s,e)=>await Refresh();spring.Interval=TimeSpan.FromMilliseconds(16);spring.Tick+=(s,e)=>Spring();
        MouseLeftButtonDown+=(s,e)=>{hover.IsOpen=false;dragging=true;moved=false;start=Pointer();origin=new Point(Left,Top);CaptureMouse();};
        MouseMove+=(s,e)=>{var p=e.GetPosition(face);if(dragging){var delta=Pointer()-start;if(delta.Length>4)moved=true;if(moved){Left=origin.X+delta.X;Top=origin.Y+delta.Y;Magnet(false);}}else{int h=face.Hit(p);if(h!=face.Hover){face.Hover=h;ShowHover(h);}}};
        MouseLeave+=(s,e)=>{face.Hover=-1;hover.IsOpen=false;};
        MouseLeftButtonUp+=(s,e)=>{if(!dragging)return;dragging=false;ReleaseMouseCapture();if(moved){Magnet(true);velocity=-face.Pull*.8;spring.Start();Save();}else{int hit=face.Hit(e.GetPosition(face));if(hit==3)ToggleTheme();else if(hit==4)Menu();else ShowHover(hit);}};
        MouseRightButtonUp+=(s,e)=>Menu();LostMouseCapture+=(s,e)=>{if(dragging){dragging=false;Magnet(true);spring.Start();}};
        Loaded+=async(s,e)=>{if(!preview)CreateTray();var a=Area();Left=config.X<0?a.Right-Width+10*UiScale:config.X;Top=config.X<0?a.Top+120:config.Y;Clamp();Magnet(true);if(!demo){refresh.Start();if(config.ConsoleConnected)await ConnectConsole(false);await Refresh();}};
        Closed+=(s,e)=>{DisposeTray();hover.IsOpen=false;refresh.Stop();spring.Stop();if(console!=null)console.Shutdown();if(!preview)Save();};
    }
    WinForms.NotifyIcon tray;
    System.Drawing.Icon trayIcon;
    void CreateTray(){
        using(var stream=typeof(GlassBar).Assembly.GetManifestResourceStream("tokenbar.ico"))
        using(var original=new System.Drawing.Icon(stream)){trayIcon=(System.Drawing.Icon)original.Clone();}
        Icon=Imaging.CreateBitmapSourceFromHIcon(trayIcon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
        tray=new WinForms.NotifyIcon{Icon=trayIcon,Text="Token Bar",Visible=true};
        var menu=new WinForms.ContextMenuStrip();
        menu.Items.Add("显示浮条",null,(s,e)=>{Show();WindowState=WindowState.Normal;Activate();});
        menu.Items.Add("隐藏浮条",null,(s,e)=>{hover.IsOpen=false;Hide();});
        menu.Items.Add("连接设置",null,(s,e)=>{Show();Settings();});
        menu.Items.Add("刷新",null,async(s,e)=>await Refresh());
        menu.Items.Add("切换深色 / 浅色",null,(s,e)=>ToggleTheme());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("退出",null,(s,e)=>Close());
        tray.ContextMenuStrip=menu;
        tray.DoubleClick+=(s,e)=>{Show();WindowState=WindowState.Normal;Activate();};
    }
    void DisposeTray(){if(tray!=null){tray.Visible=false;var menu=tray.ContextMenuStrip;tray.Dispose();if(menu!=null)menu.Dispose();tray=null;}if(trayIcon!=null){trayIcon.Dispose();trayIcon=null;}}
    Point Pointer(){var p=WinForms.Control.MousePosition;var src=PresentationSource.FromVisual(this);return src!=null?src.CompositionTarget.TransformFromDevice.Transform(new Point(p.X,p.Y)):new Point(p.X,p.Y);}
    Rect Area(){var p=WinForms.Control.MousePosition;var a=WinForms.Screen.FromPoint(p).WorkingArea;var src=PresentationSource.FromVisual(this);var t=src==null?Matrix.Identity:src.CompositionTarget.TransformFromDevice;return new Rect(t.Transform(new Point(a.Left,a.Top)),t.Transform(new Point(a.Right,a.Bottom)));}
    void Clamp(){var a=Area();Left=Math.Max(a.Left-10*UiScale,Math.Min(Left,a.Right-Width+10*UiScale));Top=Math.Max(a.Top-7*UiScale,Math.Min(Top,a.Bottom-Height));}
    public static string Nearest(Rect area,double x,double y,double width,out double distance,double scale=1){double l=Math.Abs(x+10*scale-area.Left),r=Math.Abs(x+width-10*scale-area.Right),t=Math.Abs(y+7*scale-area.Top);distance=Math.Min(l,Math.Min(r,t));return distance==t?"top":distance==l?"left":"right";}
    void Orient(string edge){bool horizontal=edge=="top";if(face.Horizontal!=horizontal){double center=Left+Width/2;face.Horizontal=horizontal;face.Width=horizontal?344:132;face.Height=horizontal?118:306;Width=face.Width*UiScale;Height=face.Height*UiScale;if(!double.IsNaN(center))Left=center-Width/2;}face.InvalidateMeasure();face.InvalidateVisual();}
    void Magnet(bool snap){var a=Area();double distance;string edge=Nearest(a,Left,Top,Width,out distance,UiScale);if(distance<60){face.Edge=edge;face.Pull=(60-distance)/60*8;if(snap){Orient(edge);if(edge=="left")Left=a.Left-10*UiScale;else if(edge=="right")Left=a.Right-Width+10*UiScale;else Top=a.Top-7*UiScale;config.Edge=edge;}}else face.Pull=0;if(snap)Clamp();face.InvalidateVisual();}
    void Spring(){velocity+=-face.Pull*.18;velocity*=.72;face.Pull+=velocity;face.RenderTransformOrigin=new Point(face.Edge=="left"?0:face.Edge=="right"?1:.5,face.Edge=="top"?0:.5);face.RenderTransform=new ScaleTransform(face.Edge=="top"?1+face.Pull*.002:1+face.Pull*.006,face.Edge=="top"?1+face.Pull*.006:1-face.Pull*.002);face.InvalidateVisual();if(Math.Abs(face.Pull)+Math.Abs(velocity)<.035){face.Pull=0;face.RenderTransform=Transform.Identity;spring.Stop();}}
    bool Save(){if(preview)return true;config.X=(int)Left;config.Y=(int)Top;config.Theme=face.Light?"light":"dark";try{File.WriteAllBytes(SettingsPath,System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(Data.Json().Serialize(config)),null,System.Security.Cryptography.DataProtectionScope.CurrentUser));return true;}catch{state="设置保存失败";return false;}}
    void ToggleTheme(){face.Light=!face.Light;face.InvalidateVisual();hover.IsOpen=false;Save();}
    void Menu(){hover.IsOpen=false;var menu=new ContextMenu();Action<string,Action> add=(label,action)=>{var item=new MenuItem{Header=label};item.Click+=(s,e)=>action();menu.Items.Add(item);};add("连接设置",()=>Settings());add("连接 DeepSeek 控制台",async()=>await ConnectConsole(true));add("刷新",async()=>await Refresh());add(face.Light?"切换 Dark":"切换 Light",()=>ToggleTheme());add(Topmost?"取消置顶":"保持置顶",()=>Topmost=!Topmost);menu.Items.Add(new Separator());add("退出",()=>Close());menu.PlacementTarget=face;menu.IsOpen=true;}
    async Task ConnectConsole(bool show){try{config.ConsoleConnected=true;face.Usage=null;face.Balance=new Meter("DeepSeek","待登录","请在控制台窗口登录 DeepSeek");if(console==null){console=new DeepSeekConsole(this,snapshot=>{face.Balance=snapshot.Balance;face.Usage=snapshot.Usage;state="控制台已同步 "+DateTime.Now.ToString("HH:mm");face.InvalidateVisual();},reason=>{face.Usage=null;if(reason.StartsWith("usage:") && (face.Balance.Value.StartsWith("¥")||face.Balance.Value.StartsWith("$")))face.Balance.Detail+="\n用量查询失败："+reason;else face.Balance=new Meter("DeepSeek",reason.StartsWith("auth:")?"待登录":"读取失败",reason);face.InvalidateVisual();});console.ShowActivated=show;console.Show();await console.Start();if(!show)console.Hide();}else{if(show){console.Show();console.Activate();}await console.Refresh();}Save();}catch{if(console!=null){console.Shutdown();console=null;}face.Balance=new Meter("DeepSeek","未连接","控制台组件未能启动。请确认已安装 Microsoft Edge WebView2 Runtime。");if(show)MessageBox.Show(this,face.Balance.Detail,"控制台连接");}face.InvalidateVisual();}
    void ShowHover(int hit){if(dragging||hit<0){hover.IsOpen=false;return;}string text=hit==1?"ZHIPU · PLAN\n\n外圈 / 5 小时   "+Data.Pct(face.Plan.FiveHour)+" 剩余\n内圈 / 周额度   "+Data.Pct(face.Plan.Weekly)+" 剩余\n\n"+face.Plan.Detail:hit==2?"DEEPSEEK · 今日累计\n\n余额   "+face.Balance.Value+"\n当日花费   "+face.Spend+"\nToken 总消耗   "+(face.Usage!=null&&face.Usage.Tokens.HasValue?face.Usage.Tokens.Value.ToString("N0"):"暂无数据")+"\n\n"+(face.Usage==null?"余额接口不含当日花费与 token 用量。\n请在设置中连接用量数据。":face.Usage.Source+"\n"+face.Usage.UpdatedAt)+"\n\n"+face.Balance.Detail:hit==3?"切换深色 / 浅色":hit==4?"连接、刷新与退出":"拖动浮条 · 靠近上 / 左 / 右边缘吸附\n"+state;
        var box=new Border{Background=Paint.B(face.Light?"#F3F7FBFF":"#F0202D40"),BorderBrush=Paint.B(face.Light?"#FFE0E7F0":"#FF455570"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(18),Child=new TextBlock{Text=text,Foreground=face.Ink,FontSize=12,FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI"),MaxWidth=300,TextWrapping=TextWrapping.Wrap}};hover.Child=box;hover.PlacementTarget=face;hover.Placement=face.Horizontal?PlacementMode.Bottom:face.Edge=="right"?PlacementMode.Left:PlacementMode.Right;hover.HorizontalOffset=8;hover.VerticalOffset=face.Horizontal?8:hit==2?140*UiScale:35*UiScale;hover.IsOpen=true;
    }
    async Task Refresh(){if(busy)return;busy=true;face.Busy=true;if(face.Usage!=null&&face.Usage.Date!=DateTime.Now.ToString("yyyy-MM-dd"))face.Usage=null;face.InvalidateVisual();var jobs=new List<Task>();jobs.Add(Update(true,config.Glm,config.International));if(!config.ConsoleConnected)jobs.Add(Update(false,config.DeepSeek,false));else if(console!=null)jobs.Add(console.Refresh());try{await Task.WhenAll(jobs);}catch{state="刷新失败，请重连控制台";}if(!config.ConsoleConnected)try{face.Usage=TodayUsage.Load(config.UsageFile);}catch{face.Usage=null;}state="更新于 "+DateTime.Now.ToString("HH:mm")+" · 每 5 分钟刷新";busy=false;face.Busy=false;face.InvalidateVisual();if(hover.IsOpen)ShowHover(face.Hover);}
    async Task Update(bool plan,string key,bool international){Meter result;if(string.IsNullOrEmpty(key))result=new Meter(plan?"GLM":"DeepSeek","未连接","请在设置中填写 API Key");else try{var json=await Data.Fetch(plan?(international?"https://api.z.ai/api/monitor/usage/quota/limit":"https://open.bigmodel.cn/api/monitor/usage/quota/limit"):"https://api.deepseek.com/user/balance",key,!plan);result=plan?Data.Glm(json):Data.Deep(json);}catch{result=new Meter(plan?"GLM":"DeepSeek","读取失败","请检查网络、Key 或套餐地区后刷新");}if(plan)face.Plan=result;else face.Balance=result;}
    void Settings(){var win=new Window{Title="Token Bar · 连接",ShowInTaskbar=false,Icon=Icon,Width=450,Height=400,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=this,Topmost=true,Background=Paint.B(face.Light?"#F4F7FC":"#202C3D")};var panel=new StackPanel{Margin=new Thickness(24)};win.Content=panel;Action<string> label=t=>panel.Children.Add(new TextBlock{Text=t,Foreground=face.Ink,Margin=new Thickness(0,8,0,5)});label("GLM 个人套餐 API Key");var glm=new PasswordBox{Password=config.Glm,Padding=new Thickness(6)};panel.Children.Add(glm);label("DeepSeek API Key");var deep=new PasswordBox{Password=config.DeepSeek,Padding=new Thickness(6)};panel.Children.Add(deep);var region=new CheckBox{Content="GLM 使用国际站 Z.ai",IsChecked=config.International,Foreground=face.Ink,Margin=new Thickness(0,12,0,8)};panel.Children.Add(region);label("今日用量文件（可选；当前余额接口不提供）");var usage=new TextBox{Text=config.UsageFile,Padding=new Thickness(6)};panel.Children.Add(usage);var button=new Button{Content="保存并刷新",Margin=new Thickness(0,20,0,0),Padding=new Thickness(8)};button.Click+=async(s,e)=>{config.Glm=glm.Password.Trim();config.DeepSeek=deep.Password.Trim();config.International=region.IsChecked==true;config.UsageFile=usage.Text.Trim();Save();win.Close();await Refresh();};panel.Children.Add(button);win.ShowDialog();}
    static void Check(bool ok){if(!ok)throw new Exception("Check failed");}
    static void Preview(){var face=new BarFace();face.Plan=new Meter("GLM","","示例"){FiveHour=82,Weekly=43};face.Balance=new Meter("DeepSeek","¥28.60","示例");face.Usage=new TodayUsage{Date=DateTime.Now.ToString("yyyy-MM-dd"),Cost=.86m,Tokens=124800,Currency="CNY",Source="示例数据"};face.Measure(new Size(132,306));face.Arrange(new Rect(0,0,132,306));
        var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRectangle(new LinearGradientBrush(Paint.C("#26364C"),Paint.C("#91A1B9"),30),null,new Rect(0,0,700,420));Paint.Text(d,"COMPACT / GLASS",20,Brushes.White,new Point(28,20),true);Paint.Text(d,"示例数据 · 0–5 元连续渐变 · 超过 5 元深红",11,Brushes.White,new Point(29,52));
            decimal[] costs={0m,2.5m,5m,6.8m};for(int i=0;i<4;i++){face.Light=i%2==1;face.Usage.Cost=costs[i];d.PushTransform(new TranslateTransform(32+i*164,96));d.PushTransform(new ScaleTransform(UiScale,UiScale));face.Render(d);d.Pop();d.Pop();Paint.Text(d,"¥"+costs[i].ToString("0.00"),12,Brushes.White,new Point(66+i*164,341),true);}
            Paint.Text(d,"竖向 99×230  /  顶部横向 258×89   ·   显示大小随系统缩放变化",11,Brushes.White,new Point(29,388));}
        var bmp=new RenderTargetBitmap(1400,840,192,192,PixelFormats.Pbgra32);bmp.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(Path.Combine(Root,"compact-preview.png")))encoder.Save(file);
    }
    [STAThread] static void Main(string[] args){System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;var app=new Application();if(args.Length>0&&args[0]=="--test"){try{GlassChecks.Run();Check(Paint.Quota(100)!=Paint.Quota(75));double delta;Check(Nearest(new Rect(0,0,1920,1080),-10,300,132,out delta)=="left"&&delta==0);Check(Nearest(new Rect(0,0,1920,1080),500,-7,132,out delta)=="top"&&delta==0);Preview();File.WriteAllText(Path.Combine(Root,"glass-test.txt"),"PASS: vector icons, dark/light render, nested rings, top/left docking geometry, continuous colors. Live transparency and pointer animation require desktop verification.");}catch(Exception e){File.WriteAllText(Path.Combine(Root,"glass-test.txt"),e.ToString());Environment.ExitCode=1;}return;}bool created;using(var mutex=new System.Threading.Mutex(true,"Local\\TokenBar_Desktop_v1",out created)){if(!created){MessageBox.Show("请先退出正在运行的旧版 Token Bar。");return;}app.Run(new GlassBar());}}
}


