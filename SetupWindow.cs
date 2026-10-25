using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SpeakerMesh;
public sealed class SetupWindow:Window {
 readonly MainWindow owner;readonly TextBlock message;readonly Button finish;
 public string? ChosenId {get;private set;}
 public SetupWindow(MainWindow owner,List<Endpoint> endpoints) {
  this.owner=owner;Owner=owner;Resources.MergedDictionaries.Add(owner.Resources);Title="Set up SpeakerMesh output";Width=570;Height=520;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(245,247,250));FontFamily=new FontFamily("Segoe UI");Foreground=new SolidColorBrush(Color.FromRgb(36,52,72));
  var root=new StackPanel{Margin=new Thickness(28)};Content=root;
  root.Children.Add(new TextBlock{Text="Give your PC one place to play.",FontSize=23,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
  root.Children.Add(new TextBlock{Text="Computer audio → SpeakerMesh input → your speakers",FontSize=13,Margin=new Thickness(0,12,0,22)});
  message=new TextBlock{FontSize=13,TextWrapping=TextWrapping.Wrap,LineHeight=21};root.Children.Add(message);
  var driver=new Button{Content="1. Get VB-CABLE from the official site",Padding=new Thickness(15,12,15,12),Margin=new Thickness(0,20,0,10),HorizontalContentAlignment=HorizontalAlignment.Left};
  driver.Click+=(_,_)=> {Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/"){UseShellExecute=true});message.Text="On the official site, download VB-CABLE. Extract it, run the vendor's setup as administrator, and restart Windows as directed. Then reopen SpeakerMesh and finish setup. The driver is not bundled in this app.";};root.Children.Add(driver);
  var settings=new Button{Content="Open Windows Sound settings",Padding=new Thickness(15,12,15,12),Margin=new Thickness(0,0,0,10),HorizontalContentAlignment=HorizontalAlignment.Left};settings.Click+=(_,_)=>Process.Start(new ProcessStartInfo("ms-settings:sound"){UseShellExecute=true});root.Children.Add(settings);
  finish=new Button{Content="2. Detect input and finish setup",Padding=new Thickness(15,12,15,12),Margin=new Thickness(0,0,0,12),HorizontalContentAlignment=HorizontalAlignment.Left};finish.Click+=Finish;root.Children.Add(finish);
  root.Children.Add(new TextBlock{Text="No microphone or recording-device default is changed. SpeakerMesh starts relaying before it takes over Windows playback, and restores previous playback defaults on stop/exit where available. Closing the playing window keeps audio running in the tray.",FontSize=11,Foreground=Brushes.SlateGray,TextWrapping=TextWrapping.Wrap,LineHeight=17});
  ShowDetected(endpoints);
 }
 void ShowDetected(List<Endpoint> endpoints) {
  var input=endpoints.FirstOrDefault(x=>DevicePolicy.IsDedicated(x.Name));
  message.Text=input==null?"The dedicated audio driver is not installed yet. Install VB-CABLE once using the official vendor installer. It creates a playback endpoint that can be named SpeakerMesh in Windows Sound settings.":"Found: "+input.Name+". Finish setup to select this input and request the SpeakerMesh name. Press Start listening in the main window to make it the Windows playback output.";
 }
 void Finish(object sender,RoutedEventArgs e) {
  try {
   var endpoint=NativeAudio.Devices().FirstOrDefault(x=>DevicePolicy.IsDedicated(x.Name));
   if(endpoint==null){ShowDetected(NativeAudio.Devices());return;}
   if(!endpoint.Name.StartsWith("SpeakerMesh",StringComparison.OrdinalIgnoreCase)) {
    try {
     if(!WindowsOutput.RenameDedicated(endpoint)) {message.Text="The driver is ready, but Windows did not confirm its new name. Open Sound settings, rename CABLE Input to SpeakerMesh, then press Finish again.";return;}
    }catch(Exception ex){message.Text="The virtual driver is present. Windows declined the name change: "+ex.Message+". Open Sound settings and rename CABLE Input to SpeakerMesh, then finish setup again.";return;}
   }
   ChosenId=endpoint.Id;DialogResult=true;
  }catch(Exception ex){message.Text=ex.Message;}
 }
}


