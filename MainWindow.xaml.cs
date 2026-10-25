using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace SpeakerMesh;
public partial class MainWindow:Window {
 readonly ObservableCollection<SpeakerView> speakers=[];
 readonly OutputLease outputLease=new();
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(180)};
 AudioSession? session;List<Endpoint> endpoints=[];
 bool busy,ready,refreshing,closing;int ticks;
 CalibrationResult? lastCalibration;
 string wavPath="";SpeakerView? inspected;
 string? configuredInput;
 static string ProfilePath=>System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SpeakerMesh","setup.json");
 static Brush Brush(string hex)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
 public MainWindow() {
  InitializeComponent();ChannelBox.ItemsSource=Enum.GetValues<ChannelMode>();
  ready=true;RefreshDevices();UpdateMode();
  if(File.Exists(ProfilePath))LoadClick(this,new RoutedEventArgs());
  timer.Tick+=async(_,_)=> {
   if(session!=null) {
    var d=session.Diagnostics;InputLevel.Value=d.HasRecentAudio?Math.Min(d.Peak,1):0;
    MeterLabel.Text=ModeBox.SelectedIndex==2?(d.HasRecentAudio?"Audio is coming in":"Waiting for computer audio"):"Playing local audio";
    if(session.Finished && !busy) await StopAudio(false);
   }
   if(++ticks%16==0 && !busy && !refreshing) {
    RefreshDevices();
    if(session!=null && KeepDefault.IsChecked==true && outputLease.Owned) try {outputLease.Maintain();}catch(Exception ex){SetStatus("Could not keep the default output: "+ex.Message);}
   }
  };timer.Start();
  Closing+=OnClosing;
 }
 void SetStatus(string text)=>Status.Text=text;
 void Report(string text) {if(!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(()=>SetStatus(text));}
 void RefreshDevices() {
  if(!ready || refreshing) return;refreshing=true;
  try {
   string? old=(SourceBox.SelectedItem as Endpoint)?.Id;
   endpoints=NativeAudio.Devices();
   foreach(var speaker in speakers) speaker.Connected=endpoints.Any(e=>e.Id==speaker.Id && !DevicePolicy.IsVirtual(e.Name));
   foreach(var ep in endpoints.Where(e=>!DevicePolicy.IsVirtual(e.Name))) {
    var existing=speakers.FirstOrDefault(x=>x.Id==ep.Id);
    if(existing!=null) {existing.Name=ep.Name;existing.Connected=true;}
    else {var view=new SpeakerView(ep);view.PropertyChanged+=SpeakerChanged;speakers.Add(view);}
   }
   var inputs=endpoints.Where(x=>DevicePolicy.IsVirtual(x.Name)).OrderByDescending(x=>DevicePolicy.IsDedicated(x.Name)).ToList();
   SourceBox.ItemsSource=inputs;
   SourceBox.SelectedItem=inputs.FirstOrDefault(x=>x.Id==old)??inputs.FirstOrDefault();
   ConnectionCount.Text=$"{speakers.Count(x=>x.Connected)} connected  /  {speakers.Count(x=>x.Connected&&x.Selected)} selected";
   UpdateSetupBanner();UpdateMode();DrawMesh();
   if(inspected==null && speakers.Count>0) Inspect(speakers[0]);
  } catch(Exception ex) {SetStatus("Cannot read Windows outputs: "+ex.Message);}
  finally {refreshing=false;}
 }
 void SpeakerChanged(object? sender,PropertyChangedEventArgs e) {if(e.PropertyName is nameof(SpeakerView.Delay) or nameof(SpeakerView.Channel) or nameof(SpeakerView.Selected) or nameof(SpeakerView.Connected)) lastCalibration=null;if(e.PropertyName is nameof(SpeakerView.Selected) or nameof(SpeakerView.Connected)) {ConnectionCount.Text=$"{speakers.Count(x=>x.Connected)} connected  /  {speakers.Count(x=>x.Connected&&x.Selected)} selected";DrawMesh();}}
 void Inspect(SpeakerView speaker) {
  inspected=speaker;Inspector.DataContext=speaker;SelectedName.Text=speaker.Title;SelectedHint.Text=speaker.ConnectionText;
  Inspector.IsEnabled=session==null && speaker.Connected;DrawMesh();
 }
 void ModeChanged(object sender,SelectionChangedEventArgs e) {if(ready) UpdateMode();}
 void SourceChanged(object sender,SelectionChangedEventArgs e) {if(ready && !refreshing) {UpdateSetupBanner();UpdateMode();DrawMesh();}}
 void UpdateMode() {
  SourceBox.Visibility=ModeBox.SelectedIndex==2?Visibility.Visible:Visibility.Collapsed;
  KeepDefault.Visibility=ModeBox.SelectedIndex==2?Visibility.Visible:Visibility.Collapsed;
  FileButton.Visibility=ModeBox.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;
  SourceDetail.Text=ModeBox.SelectedIndex switch {0=>"A soft pulse every second. Use it to align your speakers.",1=>string.IsNullOrEmpty(wavPath)?"Uncompressed mono or stereo WAV.":System.IO.Path.GetFileName(wavPath),_=>"Computer audio enters here. This input is never a speaker destination."};
  KeepDefault.IsEnabled=session==null && SourceBox.SelectedItem is Endpoint ep && DevicePolicy.IsDedicated(ep.Name);
  if(ModeBox.SelectedIndex==2 && SourceBox.SelectedItem is Endpoint input && !DevicePolicy.IsDedicated(input.Name)) SourceDetail.Text="Existing test input. Route music here manually, or set up the dedicated output above.";
  DrawMesh();
 }
 void UpdateSetupBanner() {
  var dedicated=endpoints.FirstOrDefault(x=>DevicePolicy.IsDedicated(x.Name));
  if(dedicated==null) {
   SetupHeadline.Text="One setup step, then all your sound lands here.";
   SetupHint.Text="Install VB-CABLE to create a dedicated Windows output. Your existing Oculus input is optional for testing.";
   DefaultButton.Content="Set up output";SetupBanner.Background=Brush("#FFF1D9");
  } else {
   bool isDefault=NativeAudio.DefaultOutputId()==dedicated.Id;
   SetupHeadline.Text=isDefault?"Computer audio is pointed at your virtual input.":"Your virtual input is ready.";
   SetupHint.Text=isDefault?"Select your speakers and start listening. Previous defaults are restored when you stop or close.":"Finish setup to name it SpeakerMesh and use it as Windows' output.";
   DefaultButton.Content=isDefault?"Output settings":"Use SpeakerMesh";SetupBanner.Background=Brush("#F0F5FF");
  }
 }
 void MeshResized(object sender,SizeChangedEventArgs e) {if(ready) DrawMesh();}
 void DrawMesh() {
  if(!ready || MeshCanvas.ActualWidth<20) return;
  MeshCanvas.Children.Clear();double w=MeshCanvas.ActualWidth,h=MeshCanvas.ActualHeight,cx=w/2,cy=h/2;
  var items=speakers.ToList();double rx=Math.Max(140,(w-188)/2-4),ry=Math.Max(95,(h-92)/2-5);
  if(items.Count>6) { // Keep every discovered device usable, even in larger setups.
   for(int i=0;i<items.Count;i++) AddCard(items[i],i%2==0?0:w-176,10+(i/2)*98);
  } else for(int i=0;i<items.Count;i++) {
   double angle=items.Count==1?0:(2*Math.PI*i/items.Count-Math.PI/4);
   double x=cx+rx*Math.Cos(angle)-88,y=cy+ry*Math.Sin(angle)-43;
   var line=new Line{X1=cx,Y1=cy,X2=x+88,Y2=y+43,Stroke=Brush(items[i].Selected&&items[i].Connected?"#8ABEFF":"#E8EDF3"),StrokeThickness=items[i].Selected?2.5:1.5,IsHitTestVisible=false};
   MeshCanvas.Children.Add(line);AddCard(items[i],x,y);
  }
  var glow=new Ellipse{Width=174,Height=174,Fill=Brush("#EDF5FF"),IsHitTestVisible=false};Canvas.SetLeft(glow,cx-87);Canvas.SetTop(glow,cy-87);MeshCanvas.Children.Add(glow);
  var hub=new Border{Width=142,Height=142,CornerRadius=new CornerRadius(71),Background=Brush("#007AFF"),BorderBrush=Brush("#66AFFF"),BorderThickness=new Thickness(1)};
  var stack=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
  stack.Children.Add(new TextBlock{Text="\u223F",FontSize=35,Foreground=Brush("#FFFFFF"),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,-6,0,0)});
  stack.Children.Add(new TextBlock{Text="SpeakerMesh",FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Brush("#FFFFFF"),HorizontalAlignment=HorizontalAlignment.Center});
  stack.Children.Add(new TextBlock{Text=session!=null?"RELAYING AUDIO":"YOUR AUDIO HUB",FontSize=8,Foreground=Brush("#DBEBFF"),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,6,0,0)});
  hub.Child=stack;Canvas.SetLeft(hub,cx-71);Canvas.SetTop(hub,cy-71);MeshCanvas.Children.Add(hub);
  EmptyNotice.Visibility=items.Count==0?Visibility.Visible:Visibility.Collapsed;
 }
 void AddCard(SpeakerView speaker,double x,double y) {
  var card=new Border{Width=176,Height=86,CornerRadius=new CornerRadius(20),Padding=new Thickness(13,10,11,10),Background=Brush(speaker.Selected?"#EFF6FF":"#F7F7F9"),BorderBrush=Brush(inspected?.Id==speaker.Id?"#8ABEFF":"#E3EAF0"),BorderThickness=new Thickness(inspected?.Id==speaker.Id?2:1),Opacity=speaker.Connected?1:.55};
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(26)});
  var text=new StackPanel();text.Children.Add(new TextBlock{Text=speaker.Title,FontSize=12,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,ToolTip=speaker.Name});
  text.Children.Add(new TextBlock{Text=speaker.Connected?"Connected":"Disconnected",FontSize=10,Foreground=Brush(speaker.Connected?"#6E6E73":"#8994A3"),Margin=new Thickness(0,7,0,0)});
  text.Children.Add(new TextBlock{Text=speaker.Selected?(lastCalibration?.Delays.ContainsKey(speaker.Id)==true?$"Auto Sync  {speaker.Delay:0.0} ms":"Included in your sound"):"Tap + to include",FontSize=9,Foreground=Brush("#7B8C9D"),Margin=new Thickness(0,4,0,0)});
  grid.Children.Add(text);
  var toggle=new Button{Content=speaker.Selected?"\u2713":"+",Padding=new Thickness(0),Width=25,Height=25,VerticalAlignment=VerticalAlignment.Top,Background=Brush(speaker.Selected?"#D6E8FF":"#EBEBF0"),FontSize=16,IsEnabled=speaker.Connected && session==null && !busy};
  toggle.Click+=(_,e)=> {e.Handled=true;speaker.Selected=!speaker.Selected;Inspect(speaker);};Grid.SetColumn(toggle,1);grid.Children.Add(toggle);card.Child=grid;
  card.MouseLeftButtonUp+=(_,_)=>Inspect(speaker);Canvas.SetLeft(card,x);Canvas.SetTop(card,y);MeshCanvas.Children.Add(card);
 }
 async void AutoSyncClick(object sender,RoutedEventArgs e) {
  if(busy)return;
  var targets=DevicePolicy.Destinations(speakers.Where(x=>x.Selected&&x.Connected).Select(x=>x.Config()),endpoints,(SourceBox.SelectedItem as Endpoint)?.Id);
  if(targets.Count<2){SetStatus("Select at least two connected speakers, then choose Auto Sync.");return;}
  int playbackRate=session?.SampleRate??48000;bool resume=session!=null;string outcome="Auto Sync cancelled. Your previous delays are unchanged.";
  busy=true;SetControls(false);StartButton.IsEnabled=false;
  try {
   // Keep the virtual Windows route during the sweep so background music is not redirected into a physical speaker.
   if(session!=null){await session.StopAsync();session=null;}DrawMesh();
   var dialog=new CalibrationWindow(this,targets,playbackRate);
   if(dialog.ShowDialog()==true && dialog.Result is CalibrationResult result) {
    foreach(var speaker in speakers)if(result.Delays.TryGetValue(speaker.Id,out double delay))speaker.Delay=delay;
    lastCalibration=result;AutoSyncButton.ToolTip=$"Last check: {result.ResidualMs:0.0} ms measured separation at your microphone.";
    SaveClick(this,new RoutedEventArgs());
    outcome=$"Auto Sync checked: {result.ResidualMs:0.0} ms measured separation. New delays applied and saved. Recalibrate after moving or reconnecting speakers.";
   }
  }catch(Exception ex){outcome="Auto Sync stopped: "+ex.Message;}
  finally{busy=false;SetControls(true);StartButton.Content="Start listening";LiveBadge.Text="READY";DrawMesh();}
  if(resume){await StartAudio();if(session==null){SetStatus(outcome+" Playback did not resume: "+Status.Text);return;}}
  else try{outputLease.Restore();}catch(Exception ex){outcome+=" Restore Windows audio manually: "+ex.Message;}
  SetStatus(outcome);
 }
 async void StartClick(object sender,RoutedEventArgs e) {if(session==null) await StartAudio();else await StopAudio();}
 async Task StartAudio() {
  if(busy) return;busy=true;SetControls(false);
  try {
   IAudioSource? local=ModeBox.SelectedIndex switch {0=>new PulseSource(),1=>await Task.Run(()=>new WavSource(wavPath)),_=>null};
   string? sourceId=ModeBox.SelectedIndex==2?(SourceBox.SelectedItem as Endpoint)?.Id:null;
   if(local==null && sourceId==null) throw new InvalidOperationException("Set up a virtual audio input first, or choose WAV song / Test pulses.");
   var targets=DevicePolicy.Destinations(speakers.Where(x=>x.Selected&&x.Connected).Select(x=>x.Config()),endpoints,sourceId);
   if(targets.Count==0) throw new InvalidOperationException("Tap + on at least one connected speaker card.");
   session=new(targets,local,sourceId,Report);await session.StartAsync();
   // Switch Windows only after the streams have successfully opened.
   if(sourceId!=null && DevicePolicy.IsDedicated(endpoints.First(x=>x.Id==sourceId).Name) && KeepDefault.IsChecked==true) {outputLease.Acquire(sourceId);configuredInput=sourceId;}
   LiveBadge.Text="LISTENING";StartButton.Content="Stop listening";SetStatus("Relaying to "+targets.Count+" speaker(s). Stop to adjust routing or delay.");DrawMesh();
  } catch(Exception ex) {
   if(session!=null) await session.StopAsync();session=null;
   try{outputLease.Restore();}catch(Exception restore){SetStatus("Restore your previous output in Windows Sound settings: "+restore.Message);}
   SetStatus(ex.Message);SetControls(true);DrawMesh();
  } finally {busy=false;}
 }
 async Task StopAudio(bool notify=true) {
  if(busy) return;busy=true;
  try {if(session!=null) await session.StopAsync();session=null;outputLease.Restore();if(notify) SetStatus("Stopped. Your previous Windows output is restored where available.");}
  catch(Exception ex){SetStatus(ex.Message);}
  finally {busy=false;SetControls(true);LiveBadge.Text="READY";StartButton.Content="Start listening";InputLevel.Value=0;DrawMesh();UpdateSetupBanner();}
 }
 void SetControls(bool enabled) {ModeBox.IsEnabled=SourceBox.IsEnabled=FileButton.IsEnabled=KeepDefault.IsEnabled=enabled;Inspector.IsEnabled=enabled && inspected?.Connected==true;KeepDefault.IsEnabled=enabled && SourceBox.SelectedItem is Endpoint input && DevicePolicy.IsDedicated(input.Name);StartButton.IsEnabled=true;}
 async void OnClosing(object? sender,CancelEventArgs e) {
  if(closing) return;e.Cancel=true;if(busy) return;
  await StopAudio(false);timer.Stop();closing=true;Close();
 }
 void RefreshClick(object sender,RoutedEventArgs e)=>RefreshDevices();
 void BrowseClick(object sender,RoutedEventArgs e) {var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Uncompressed WAV|*.wav"};if(dialog.ShowDialog(this)==true){wavPath=dialog.FileName;UpdateMode();}}
 void SetupClick(object sender,RoutedEventArgs e)=>ShowSetup();
 void DefaultClick(object sender,RoutedEventArgs e)=>ShowSetup();
 void ShowSetup() {
  var dialog=new SetupWindow(this,endpoints);dialog.ShowDialog();RefreshDevices();
  if(dialog.ChosenId!=null){configuredInput=dialog.ChosenId;SourceBox.SelectedItem=endpoints.FirstOrDefault(x=>x.Id==dialog.ChosenId);ModeBox.SelectedIndex=2;}
 }
 void SaveClick(object sender,RoutedEventArgs e) {
  try {var profile=new Profile((SourceBox.SelectedItem as Endpoint)?.Id??"",wavPath,ModeBox.SelectedIndex,speakers.Where(x=>x.Selected).Select(x=>x.Config()).ToList());Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ProfilePath)!);File.WriteAllText(ProfilePath,JsonSerializer.Serialize(profile));SetStatus("Your sound space is saved.");}
  catch(Exception ex){SetStatus(ex.Message);}
 }
 void LoadClick(object sender,RoutedEventArgs e) {
  if(session!=null || busy){SetStatus("Stop audio before loading a setup.");return;}
  try {
   var profile=JsonSerializer.Deserialize<Profile>(File.ReadAllText(ProfilePath))??throw new InvalidDataException("Invalid setup.");
   if(profile.SourceMode<0||profile.SourceMode>2||profile.Outputs==null||profile.Outputs.Any(x=>!double.IsFinite(x.Gain)||x.Gain<0||x.Gain>1||!double.IsFinite(x.DelayMs)||x.DelayMs<0||x.DelayMs>2000||!Enum.IsDefined(x.Channel))) throw new InvalidDataException("Saved setup contains invalid audio settings.");
   RefreshDevices();ModeBox.SelectedIndex=profile.SourceMode;wavPath=profile.FilePath;
   SourceBox.SelectedItem=endpoints.FirstOrDefault(x=>x.Id==profile.SourceId && DevicePolicy.IsVirtual(x.Name));
   foreach(var speaker in speakers){var saved=profile.Outputs.FirstOrDefault(x=>x.Id==speaker.Id);speaker.Selected=saved!=null;if(saved!=null){speaker.Volume=saved.Gain*100;speaker.Delay=saved.DelayMs;speaker.Channel=saved.Channel;}}
   UpdateMode();DrawMesh();SetStatus("Setup restored. Virtual inputs are excluded from destinations automatically.");
  }catch(Exception ex){SetStatus(ex.Message);}
 }
 void DiagnosticsClick(object sender,RoutedEventArgs e) {
  try {
   var d=session?.Diagnostics;
   Clipboard.SetText("SpeakerMesh 0.4\nWindows default: "+NativeAudio.DefaultOutputId()+"\nSource: "+(SourceBox.SelectedItem as Endpoint)?.Name+"\nMode: "+ModeBox.SelectedIndex+"\nWindows route owned: "+outputLease.Owned+"\nOutputs: "+string.Join(" | ",speakers.Select(x=>$"{x.Name}; connected={x.Connected}; selected={x.Selected}; volume={x.Volume}; delay={x.Delay}"))+"\nStatus: "+Status.Text+"\nCaptured="+d?.CapturedFrames+"; rendered="+d?.RenderedFrames+"; starvations="+d?.Underruns+"; capture gaps="+d?.Discontinuities);
   SetStatus("Diagnostics copied. Paste them into the chat if something sounds wrong.");
  }catch(Exception ex){SetStatus(ex.Message);}
 }
 public void CheckInterface(string reportPath) {
  timer.Stop();Root.Measure(new Size(1156,766));Root.Arrange(new Rect(0,0,1156,766));Root.UpdateLayout();DrawMesh();Root.UpdateLayout();
  var checks=new List<string>();
  if(MeshCanvas.Children.OfType<Border>().Count()!=speakers.Count+1) throw new Exception("A device card or hub is missing.");checks.Add("PASS real device cards plus central hub");
  if(SourceBox.Items.Cast<Endpoint>().Any(x=>!DevicePolicy.IsVirtual(x.Name))) throw new Exception("Physical speaker listed as virtual source.");checks.Add("PASS source selector contains only virtual inputs");
  if(speakers.Count>0) {
   var view=speakers.First();bool before=view.Selected;
   var card=MeshCanvas.Children.OfType<Border>().First();
   var toggle=((Grid)card.Child).Children.OfType<Button>().Single();toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   if(view.Selected==before || Inspector.DataContext!=view) throw new Exception("Card toggle did not update routing and inspector.");
   view.Selected=before;checks.Add("PASS card switch updates speaker selection and inspector");
  }
  var dedicated=endpoints.Any(x=>DevicePolicy.IsDedicated(x.Name));
  if(!dedicated && KeepDefault.IsEnabled) throw new Exception("Default output toggle enabled before dedicated setup.");checks.Add("PASS default-output option gated by installed input");
  if(StartButton.Content?.ToString()!="Start listening") throw new Exception("Unexpected playback state.");checks.Add("PASS interface starts without audio or default-device changes");
  if(AutoSyncButton.Content?.ToString()!="Auto Sync") throw new Exception("Auto Sync control is missing.");checks.Add("PASS Auto Sync button is present without starting a microphone capture");
  File.WriteAllLines(reportPath,checks);
 }
 public void RenderCalibrationPreview(string path) {
  timer.Stop();var targets=endpoints.Where(x=>!DevicePolicy.IsVirtual(x.Name)).Take(2).Select(x=>new OutputConfig(x.Id,x.Name)).ToList();
  var dialog=new CalibrationWindow(this,targets);dialog.RenderPreview(path);
 }
 public void RenderPreview(string path) {
  timer.Stop();Width=1220;Height=850;
  Root.Measure(new Size(1156,766));Root.Arrange(new Rect(0,0,1156,766));Root.UpdateLayout();DrawMesh();Root.UpdateLayout();
  var bitmap=new RenderTargetBitmap(1156,766,96,96,PixelFormats.Pbgra32);bitmap.Render(Root);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
 }
}







