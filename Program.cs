using System;
using System.IO;
using System.Windows;
namespace SpeakerMesh;
static class Program {
 [STAThread] static void Main(string[] args) {
  if(args.Contains("--devices")) { Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(NativeAudio.Devices()));return; }
  if(args.Contains("--microphones")){Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(NativeAudio.Devices(1)));return;}
  var app=new Application();
  app.DispatcherUnhandledException+=(_,e)=> { if(args.Length==0){MessageBox.Show(e.Exception.Message,"SpeakerMesh");e.Handled=true;} };
  bool preview=args.Length==2 && (args[0]=="--render-preview" || args[0]=="--ui-check" || args[0]=="--render-calibration-preview");
  using var single=new Mutex(true,"Local\\SpeakerMesh-0.4",out bool first);
  if(!preview && !first){MessageBox.Show("SpeakerMesh 0.4 is already open.","SpeakerMesh");return;}
  var window=new MainWindow();
  if(args.Length==2 && args[0]=="--ui-check") {window.CheckInterface(Path.GetFullPath(args[1]));return;}
  if(args.Length==2 && args[0]=="--render-calibration-preview"){window.RenderCalibrationPreview(Path.GetFullPath(args[1]));return;}
  if(args.Length==2 && args[0]=="--render-preview") {window.RenderPreview(Path.GetFullPath(args[1]));return;}
  app.Run(window);
 }
}




