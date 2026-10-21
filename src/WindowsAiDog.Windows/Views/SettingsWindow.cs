using System.Windows;
using System.Windows.Controls;
using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Views;
public sealed class SettingsWindow:Window {
 public SettingsWindow(PetWindow pet) {
  Title="강아지 설정";Width=510;Height=600;Background=System.Windows.Media.Brushes.White;ResizeMode=ResizeMode.NoResize;
  var s=pet.Settings;var stack=new StackPanel{Background=System.Windows.Media.Brushes.White,Margin=new Thickness(20)};
  var tabs=new TabControl();Content=tabs;tabs.Items.Add(new TabItem{Header="강아지 · 소리",Content=stack});
  var ai=new TabItem{Header="AI · 연결 · 모델",Content=pet.Conversation.ManagementPanel};tabs.Items.Add(ai);Closed+=(sender,e)=>ai.Content=null;
  stack.Children.Add(new TextBlock{Text="강아지 · 8동작\n소리와 교감은 로그인 없이 동작합니다.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,15)});
  stack.Children.Add(new TextBlock{Text="이름 (1~20글자)"});var name=new TextBox{MaxLength=160,Text=s.Name,Margin=new Thickness(0,5,0,10)};stack.Children.Add(name);
  stack.Children.Add(new TextBlock{Text="친밀도: "+s.Affinity+" / 100 · 방치로 감소하지 않아요",Margin=new Thickness(0,0,0,10)});
  var focus=new CheckBox{Content="집중 모드 · 이동과 소리 멈추기",IsChecked=s.FocusMode,Margin=new Thickness(0,0,0,10)};stack.Children.Add(focus);
  var mute=new CheckBox{Content="음소거",IsChecked=s.Muted,Margin=new Thickness(0,0,0,10)};stack.Children.Add(mute);
  // Slider with its current value shown on the right, updated while dragging.
  Slider Labeled(string title,Slider slider,Func<double,string> format,string? note=null){
   stack.Children.Add(new TextBlock{Text=title,Margin=new Thickness(0,10,0,0)});
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(64)});
   var value=new TextBlock{Text=format(slider.Value),FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(value,1);
   slider.ValueChanged+=(sender,e)=>value.Text=format(e.NewValue);slider.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(slider);row.Children.Add(value);stack.Children.Add(row);
   if(note is not null)stack.Children.Add(new TextBlock{Text=note,TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=System.Windows.Media.Brushes.DimGray});
   return slider;
  }
  var volume=Labeled("강아지 효과음 음량",new Slider{Minimum=0,Maximum=100,Value=s.Volume,TickFrequency=10,IsSnapToTickEnabled=true},v=>$"{v:0}%");
  var interval=Labeled("효과음 최소 간격",new Slider{Minimum=1,Maximum=60,Value=s.SoundIntervalSeconds,TickFrequency=1,IsSnapToTickEnabled=true},v=>v>=60?"1분":$"{v:0}초","소리가 한 번 나면 이 시간이 지나야 다음 소리가 납니다(1초~1분). 간식을 먹은 뒤 짖는 소리는 예외입니다.");
  var info=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)};stack.Children.Add(info);
  var save=new Button{Content="저장",Padding=new Thickness(10)};stack.Children.Add(save);
  save.Click+=(sender,e)=>{var value=pet.Settings with{Name=name.Text,FocusMode=focus.IsChecked==true,Muted=mute.IsChecked==true,Volume=(int)volume.Value,SoundIntervalSeconds=(int)interval.Value};if(!SettingsValidator.IsValid(value)){info.Text="이름과 설정 범위를 확인하세요.";return;}pet.ApplySettings(value);Close();};
 }
}
