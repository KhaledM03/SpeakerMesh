using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SpeakerMesh;
public sealed class CalibrationWindow:Window {
 readonly Grid layout=new(){Margin=new Thickness(30)};
 readonly int playbackRate;
 readonly List<OutputConfig> outputs;readonly ComboBox microphone=new(){DisplayMemberPath="Name",Margin=new Thickness(0,10,0,18)};
 readonly TextBlock progress=new(){TextWrapping=TextWrapping.Wrap,FontSize=13,Foreground=Brushes.SlateGray,LineHeight=21};
 readonly Button run=new(){Content="Sync speakers",Background=new SolidColorBrush(Color.FromRgb(0,122,255)),Foreground=Brushes.White,Margin=new Thickness(0,14,0,0)};
 readonly Button cancel=new(){Content="Cancel",Margin=new Thickness(0,10,0,0)};
 CancellationTokenSource? cancellation;bool running,allowClose;
 public CalibrationResult? Result {get;private set;}
 static string MicPreference=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SpeakerMesh","calibration-microphone.txt");
 public CalibrationWindow(MainWindow owner,List<OutputConfig> outputs,int playbackRate=48000) {
  this.playbackRate=playbackRate;this.outputs=outputs;if(owner.IsVisible)Owner=owner;Title="Auto Sync";Width=570;Height=650;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
  FontFamily=new FontFamily("Segoe UI Variable Text");Background=new SolidColorBrush(Color.FromRgb(246,246,248));Foreground=new SolidColorBrush(Color.FromRgb(29,29,31));Resources.MergedDictionaries.Add(owner.Resources);
  var panel=new StackPanel();Content=layout;layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  layout.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
  panel.Children.Add(new TextBlock{Text="Let your speakers find their rhythm.",FontSize=25,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
  panel.Children.Add(new TextBlock{Text="Place a PC or USB microphone at your listening position. Pause music and keep the room quiet. Auto Sync plays short chirps through each selected speaker, measures their arrival, then checks the correction.",TextWrapping=TextWrapping.Wrap,FontSize=13,LineHeight=21,Margin=new Thickness(0,16,0,12)});
  panel.Children.Add(new TextBlock{Text="MICROPHONE",FontSize=10,FontWeight=FontWeights.SemiBold,Foreground=Brushes.SlateGray});panel.Children.Add(microphone);
  panel.Children.Add(new TextBlock{Text="Microphone audio is used locally during this test only. Nothing is saved or uploaded. Use a PC/USB mic; Bluetooth headset microphones can change playback mode.",TextWrapping=TextWrapping.Wrap,FontSize=12,LineHeight=19,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,14)});
  panel.Children.Add(progress);var footer=new StackPanel();footer.Children.Add(run);footer.Children.Add(cancel);Grid.SetRow(footer,1);layout.Children.Add(footer);
  try {
   var devices=NativeAudio.Devices(1).Where(x=>!DevicePolicy.IsVirtual(x.Name)).OrderByDescending(x=>x.Name.Contains("USB",StringComparison.OrdinalIgnoreCase)).ToList();microphone.ItemsSource=devices;
   string? remembered=File.Exists(MicPreference)?File.ReadAllText(MicPreference):null;
   var saved=devices.FirstOrDefault(x=>x.Id==remembered);microphone.SelectedItem=saved??devices.FirstOrDefault();run.IsEnabled=devices.Count>0;
   if(saved!=null)Loaded+=(_,_)=>Dispatcher.BeginInvoke(()=>{if(IsVisible&&!running)Run(run,new RoutedEventArgs());});
   progress.Text=devices.Count>0?$"{outputs.Count} speakers selected. Allow roughly {2*(2+outputs.Count*4)+6} seconds. Delays are replaced only after the check pass succeeds.":"No physical microphone is available. Connect one and reopen Auto Sync. Manual speaker delays still work.";
  }catch(Exception ex){progress.Text=ex.Message;run.IsEnabled=false;}
  run.Click+=Run;
  cancel.Click+=(_,_)=> {if(running){cancellation?.Cancel();progress.Text="Stopping test tones and microphone...";}else Close();};
  Closing+=(_,e)=> {if(running&&!allowClose){e.Cancel=true;cancellation?.Cancel();progress.Text="Stopping test tones and microphone...";}};
 }
 public void RenderPreview(string path) {
  layout.Background=Background;layout.Measure(new Size(510,560));layout.Arrange(new Rect(0,0,510,560));layout.UpdateLayout();
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(510,560,96,96,PixelFormats.Pbgra32);bitmap.Render(layout);
  var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
 }
 async void Run(object sender,RoutedEventArgs e) {
  if(running || microphone.SelectedItem is not Endpoint mic)return;
  running=true;run.IsEnabled=false;microphone.IsEnabled=false;cancellation=new();
  try {
   Result=await AcousticCalibrator.RunAsync(outputs,mic.Id,new Progress<string>(text=>progress.Text=text),cancellation.Token,playbackRate);
   cancellation.Token.ThrowIfCancellationRequested();
   try{Directory.CreateDirectory(Path.GetDirectoryName(MicPreference)!);File.WriteAllText(MicPreference,mic.Id);}catch{} // Remembering a choice must not invalidate a completed acoustic test.
   progress.Text=$"Check pass: {Result.ResidualMs:0.0} ms measured separation. Applying the verified delays.";
   running=false;allowClose=true;DialogResult=true;
  }catch(OperationCanceledException){progress.Text="Cancelled. Your previous delays are unchanged.";allowClose=true;DialogResult=false;}
  catch(Exception ex){progress.Text=ex.Message;}
  finally{running=false;run.IsEnabled=true;microphone.IsEnabled=true;cancellation.Dispose();cancellation=null;}
 }
}





