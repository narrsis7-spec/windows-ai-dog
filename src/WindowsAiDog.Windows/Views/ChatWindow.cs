using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using System.IO;
using System.Diagnostics;
using WindowsAiDog.Windows.Chat;
namespace WindowsAiDog.Windows.Views;
public sealed class ChatWindow:Window {
 private readonly PetWindow pet;
 private readonly TextBox transcript=new(){IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
 private readonly TextBox input=new(){MaxLength=4000,MinWidth=260};
 private readonly ComboBox models=new(){MinWidth=180};
 private readonly Button connect=new(){Content="Continue with ChatGPT",Margin=new Thickness(0,0,8,0)};
 private readonly Button send=new(){Content="보내기",Margin=new Thickness(8,0,0,0)};
 private readonly TextBlock connectionInfo=new(){TextWrapping=TextWrapping.Wrap};
 private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
 private CancellationTokenSource? pending;
 private readonly List<ConversationTurn> history=new();
 private string ModelPath()=>Path.Combine(pet.DataDirectory,"selected-model-"+pet.Provider.Id+".txt");
 private void ReadSavedModel(){try{var path=ModelPath();if(File.Exists(path)&&new FileInfo(path).Length<=256){var slug=File.ReadAllText(path);if(slug.Length>0&&!slug.Any(char.IsControl)){models.ItemsSource=new[]{new ModelChoice(slug,slug)};models.SelectedIndex=0;}}}catch(Exception e) when(e is IOException or UnauthorizedAccessException){}}
 public FrameworkElement ManagementPanel {get;}
 public bool MovementLocked=>IsVisible&&(IsKeyboardFocusWithin||!string.IsNullOrWhiteSpace(input.Text)||pending is not null);
 public void FocusInput(){Activate();input.Focus();}
 public ChatWindow(PetWindow pet) {
  Background=System.Windows.Media.Brushes.White;this.pet=pet;Title="강아지 입력";Width=336;Height=70;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=System.Windows.Media.Brushes.Transparent;Topmost=true;ShowActivated=false;ShowInTaskbar=false;
  var root=new DockPanel{Background=System.Windows.Media.Brushes.White,Margin=new Thickness(16)};ManagementPanel=root;
  var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  header.Children.Add(connectionInfo);var cancelRequest=new Button{Content="진행 중 요청 취소",Margin=new Thickness(0,8,0,0)};cancelRequest.Click+=(s,e)=>pending?.Cancel();header.Children.Add(cancelRequest);
  var providerSettings=new Button{Content="AI 제공자 / 연결 설정",Margin=new Thickness(0,8,0,0)};header.Children.Add(providerSettings);providerSettings.Click+=(s,e)=>new ProviderSettingsWindow(pet.Providers){Owner=this}.ShowDialog();
  var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,8)};row.Children.Add(connect);
  var disconnect=new Button{Content="연결 해제",Margin=new Thickness(0,0,8,0)};row.Children.Add(disconnect);row.Children.Add(models);header.Children.Add(row);header.Children.Add(status);
  var usage=new Button{Content="ChatGPT 사용량 / 연결 관리",Margin=new Thickness(0,8,0,8)};usage.IsEnabled=pet.Provider.Billing==BillingMode.SubscriptionSharing;usage.Click+=(s,e)=>Process.Start(new ProcessStartInfo("https://chatgpt.com/#settings/Usage"){UseShellExecute=true});header.Children.Add(usage);
  Content=BuildInputBar();
  status.Text=pet.Auth.Connected?"저장된 연결이 있습니다. 모델 목록을 확인하세요.":"ChatGPT를 연결하면 텍스트 대화를 사용할 수 있습니다.";
  connect.Click+=async(s,e)=>await Connect();
  send.Click+=async(s,e)=>await Send();
  input.KeyDown+=async(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;CloseInput();return;}if(e.Key==System.Windows.Input.Key.Enter){e.Handled=true;await Send();}};
  disconnect.Click+=async(s,e)=>{if(pending is not null){pending.Cancel();status.Text="진행 중 요청을 먼저 취소하세요.";return;}pending=new CancellationTokenSource(TimeSpan.FromSeconds(15));SetBusy(true);providerSettings.IsEnabled=false;disconnect.IsEnabled=false;try{bool revoked=await pet.Providers.DisconnectAsync(pending.Token);models.ItemsSource=null;status.Text=revoked?"연결이 해제됐습니다.":"로컬 연결 해제됨. 원격 철회는 확인하지 못했습니다. ChatGPT 설정에서 연결을 확인하세요.";}catch(OperationCanceledException){status.Text="연결 해제가 취소됐습니다. 연결 상태를 다시 확인하세요.";}catch(Exception ex) when(ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidOperationException){status.Text="연결 해제를 완료하지 못했습니다. 연결 상태를 다시 확인하세요.";}finally{pending.Dispose();pending=null;SetBusy(false);providerSettings.IsEnabled=true;disconnect.IsEnabled=true;}};
  void UpdateProvider(){pending?.Cancel();history.Clear();transcript.Clear();models.ItemsSource=null;ReadSavedModel();connectionInfo.Text=pet.Provider.DisplayName+" · "+(pet.Provider.Billing switch{BillingMode.SubscriptionSharing=>"ChatGPT 구독 사용량 공유",BillingMode.ApiUsage=>"별도 API 종량 과금",_=>"사용자 로컬 서버"})+" · 대기 중 AI 호출 없음";connect.Content=pet.Provider.Billing==BillingMode.SubscriptionSharing?"Continue with ChatGPT":"모델 목록 확인";usage.IsEnabled=pet.Provider.Billing==BillingMode.SubscriptionSharing;status.Text="연결 방식을 확인하고 모델을 선택하세요. 이전 대화는 전송하지 않습니다.";}
  pet.Providers.Changed+=UpdateProvider;UpdateProvider();
  models.SelectionChanged+=(s,e)=>{if(models.SelectedItem is ModelChoice choice)try{File.WriteAllText(ModelPath(),choice.Slug);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}};
  ReadSavedModel();
  var manual=new Button{Content="설정의 직접 지정 모델 사용",Margin=new Thickness(0,5,0,0)};header.Children.Add(manual);manual.Click+=(s,e)=>{if(pending is not null||pet.Provider.Id=="chatgpt")return;var value=pet.Providers.Configuration(pet.Provider.Id).Model;if(value==""){status.Text="연결 설정에서 모델을 직접 지정하세요.";return;}models.ItemsSource=new[]{new ModelChoice(value,value+" · 직접 지정")};models.SelectedIndex=0;};
  Closed+=(s,e)=>{pending?.Cancel();pet.Providers.Changed-=UpdateProvider;};
 }
 // Warm speech-bubble input that matches the dog: cream pill, apricot accent, the dog's face on the
 // left, a small tail pointing back at the dog, and a round send button.
 private static readonly System.Windows.Media.Color Cream=System.Windows.Media.Color.FromRgb(0xFF,0xFB,0xF5),Apricot=System.Windows.Media.Color.FromRgb(0xE3,0x9A,0x5C),ApricotLine=System.Windows.Media.Color.FromRgb(0xEE,0xCF,0xAE),Ink=System.Windows.Media.Color.FromRgb(0x4A,0x34,0x26),Muted=System.Windows.Media.Color.FromRgb(0xA8,0x8F,0x79);
 private FrameworkElement BuildInputBar(){
  System.Windows.Media.SolidColorBrush B(System.Windows.Media.Color c){var b=new System.Windows.Media.SolidColorBrush(c);b.Freeze();return b;}
  var grid=new Grid{Margin=new Thickness(10,6,6,8)};
  foreach(var w in new[]{new GridLength(34),new GridLength(1,GridUnitType.Star),new GridLength(34),new GridLength(24)})grid.ColumnDefinitions.Add(new ColumnDefinition{Width=w});
  var avatar=new System.Windows.Shapes.Ellipse{Width=30,Height=30,Stroke=B(ApricotLine),StrokeThickness=1.5,Fill=B(Apricot),VerticalAlignment=VerticalAlignment.Center};
  if(pet.FaceImage() is System.Windows.Media.ImageSource face)avatar.Fill=new System.Windows.Media.ImageBrush(face){Stretch=System.Windows.Media.Stretch.UniformToFill};
  grid.Children.Add(avatar);
  input.FontSize=14;input.Padding=new Thickness(8,7,4,7);input.BorderThickness=new Thickness(0);input.Background=System.Windows.Media.Brushes.Transparent;input.Foreground=B(Ink);input.CaretBrush=B(Apricot);input.VerticalContentAlignment=VerticalAlignment.Center;
  Grid.SetColumn(input,1);grid.Children.Add(input);
  var hint=new TextBlock{Text=pet.Settings.Name+"에게 말 걸기…",Foreground=B(Muted),FontSize=14,Margin=new Thickness(11,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};Grid.SetColumn(hint,1);grid.Children.Add(hint);
  void UpdateHint(){hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;hint.Text=pending is not null?"생각하는 중…":pet.Settings.Name+"에게 말 걸기…";send.Opacity=input.Text.Trim().Length>0&&pending is null?1:.45;}
  input.TextChanged+=(s,e)=>UpdateHint();busyChanged=UpdateHint;
  // Round send button with a drawn arrow; Enter still sends.
  var arrow=new System.Windows.Shapes.Path{Data=System.Windows.Media.Geometry.Parse("M 2,7 L 12,7 M 8,3 L 12,7 L 8,11"),Stroke=System.Windows.Media.Brushes.White,StrokeThickness=2,StrokeStartLineCap=System.Windows.Media.PenLineCap.Round,StrokeEndLineCap=System.Windows.Media.PenLineCap.Round,StrokeLineJoin=System.Windows.Media.PenLineJoin.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
  send.Content=arrow;send.Width=30;send.Height=30;send.Margin=new Thickness(0);send.ToolTip="보내기 (Enter)";send.Cursor=System.Windows.Input.Cursors.Hand;send.Opacity=.45;
  send.Template=RoundButton(B(Apricot));Grid.SetColumn(send,2);grid.Children.Add(send);
  var dismiss=new Button{Content=new TextBlock{Text="×",FontSize=15,Foreground=B(Muted)},Width=22,Height=22,ToolTip="대화 입력 닫기 (Esc)",Cursor=System.Windows.Input.Cursors.Hand,Template=RoundButton(System.Windows.Media.Brushes.Transparent)};
  dismiss.Click+=(s,e)=>CloseInput();Grid.SetColumn(dismiss,3);grid.Children.Add(dismiss);
  input.ToolTip="Enter로 보내기 · Esc로 닫기 · 연결과 모델은 우클릭 설정에서";
  var pill=new Border{Background=B(Cream),BorderBrush=B(ApricotLine),BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(22),Margin=new Thickness(10,10,10,6),Child=grid,
   Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=System.Windows.Media.Color.FromRgb(0x7A,0x4E,0x2A),BlurRadius=12,ShadowDepth=2,Direction=270,Opacity=.18}};
  // Small tail pointing back at the dog, drawn over the pill border; PointTail picks the side.
  tail.Fill=B(Cream);tail.Stroke=B(ApricotLine);tail.StrokeThickness=1.5;cover.Fill=B(Cream);PointTail(Dock.Left,0);
  var root=new Grid{RenderTransform=new System.Windows.Media.TranslateTransform()};root.Children.Add(pill);root.Children.Add(tail);root.Children.Add(cover);
  // Gentle entrance: fade in while sliding out from the dog.
  IsVisibleChanged+=(s,e)=>{if(!IsVisible)return;UpdateHint();var fade=new System.Windows.Media.Animation.DoubleAnimation(0,1,TimeSpan.FromMilliseconds(170));var slide=new System.Windows.Media.Animation.DoubleAnimation(-10,0,TimeSpan.FromMilliseconds(220)){EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut}};root.BeginAnimation(OpacityProperty,fade);root.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,slide);};
  return root;
 }
 private readonly System.Windows.Shapes.Path tail=new();private readonly System.Windows.Shapes.Rectangle cover=new();
 // side: which edge of the input faces the dog; along: for Dock.Top, the x of the dog's head in DIPs.
 public void PointTail(Dock side,double along){
  var align=side==Dock.Right?HorizontalAlignment.Right:HorizontalAlignment.Left;
  if(side==Dock.Top){double x=Math.Clamp(along,34,Width-40);tail.Data=System.Windows.Media.Geometry.Parse("M 0,12 L 7,0 L 14,12");tail.HorizontalAlignment=HorizontalAlignment.Left;tail.VerticalAlignment=VerticalAlignment.Top;tail.Margin=new Thickness(x-7,0,0,0);
   cover.Width=12;cover.Height=3;cover.HorizontalAlignment=HorizontalAlignment.Left;cover.VerticalAlignment=VerticalAlignment.Top;cover.Margin=new Thickness(x-6,10.5,0,0);return;}
  tail.Data=System.Windows.Media.Geometry.Parse(side==Dock.Right?"M 0,0 L 12,7 L 0,14":"M 12,0 L 0,7 L 12,14");tail.HorizontalAlignment=align;tail.VerticalAlignment=VerticalAlignment.Center;tail.Margin=new Thickness(0,2,0,0);
  cover.Width=3;cover.Height=12;cover.HorizontalAlignment=align;cover.VerticalAlignment=VerticalAlignment.Center;cover.Margin=side==Dock.Right?new Thickness(0,2,10.5,0):new Thickness(10.5,2,0,0);
 }
 private static ControlTemplate RoundButton(System.Windows.Media.Brush fill){
  var t=new ControlTemplate(typeof(Button));var b=new FrameworkElementFactory(typeof(Border));b.SetValue(Border.CornerRadiusProperty,new CornerRadius(15));b.SetValue(Border.BackgroundProperty,fill);
  var c=new FrameworkElementFactory(typeof(ContentPresenter));c.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);c.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);b.AppendChild(c);t.VisualTree=b;return t;
 }
 private Action? busyChanged;
 public void CloseInput(){pending?.Cancel();Hide();pet.InputClosed();}
 private async Task Connect() {
  if(pending is not null)return;
  if(pet.Provider.Billing==BillingMode.SubscriptionSharing&&!pet.Auth.Connected && MessageBox.Show(this,"ChatGPT 구독 사용 권한과 지속 연결을 직접 승인해야 합니다. 기존 Codex/Work 한도를 공유하며 유료 API로 자동 전환하지 않습니다. 시스템 브라우저에서 계속할까요?","ChatGPT 연결",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
  pending=new();int generation=pet.Providers.Generation;var selectedProvider=pet.Provider;SetBusy(true);
  try{if(!selectedProvider.Auth.Connected)await selectedProvider.Auth.ConnectAsync(pending.Token);var choices=await selectedProvider.Auth.ModelsAsync(pending.Token);pending.Token.ThrowIfCancellationRequested();if(generation!=pet.Providers.Generation)return;models.ItemsSource=choices;models.SelectedIndex=choices.Count>0?0:-1;status.Text="모델 목록 확인됨 · 사용할 모델을 선택하세요.";}
  catch(OperationCanceledException){status.Text="로그인이 취소되었거나 시간이 초과됐습니다.";}
  catch(Exception e) when(e is HttpRequestException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or KeyNotFoundException or FormatException or InvalidOperationException or System.ComponentModel.Win32Exception){status.Text="연결을 완료하지 못했습니다. 권한·계정 적격 여부·네트워크를 확인하세요.";}
  finally{pending.Dispose();pending=null;SetBusy(false);}
 }
 private async Task Send() {
  if(pending is not null||string.IsNullOrWhiteSpace(input.Text))return;
  if(!pet.Auth.Connected){status.Text="우클릭 설정에서 AI를 연결해 주세요.";pet.SayNotice(status.Text);return;}
  // Catalog lookup is allowed only after explicit send or management interaction.
  if(models.SelectedItem is not ModelChoice){int catalogGeneration=pet.Providers.Generation;var provider=pet.Provider;pending=new CancellationTokenSource(TimeSpan.FromSeconds(30));SetBusy(true);try{var choices=await provider.Auth.ModelsAsync(pending.Token);pending.Token.ThrowIfCancellationRequested();if(catalogGeneration!=pet.Providers.Generation)return;models.ItemsSource=choices;models.SelectedIndex=choices.Count>0?0:-1;}catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException or IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UnauthorizedAccessException){pet.SayNotice("모델 목록을 확인하지 못했습니다. 설정에서 연결을 확인해 주세요.");return;}finally{pending.Dispose();pending=null;SetBusy(false);}}
  if(models.SelectedItem is not ModelChoice model){pet.SayNotice("설정에서 사용할 모델을 선택해 주세요.");return;}
  string text=input.Text;input.Clear();transcript.AppendText("나: "+text+"\n");pending=new();pending.CancelAfter(TimeSpan.FromSeconds(90));int generation=pet.Providers.Generation;var selectedProvider=pet.Provider;SetBusy(true);status.Text="답변을 받는 중…";
  try{string token=await selectedProvider.Auth.AccessTokenAsync(pending.Token);var reply=await selectedProvider.Client.SendAsync(token,model.Slug,text,pet.Settings.Name,pending.Token,history);if(generation!=pet.Providers.Generation)throw new OperationCanceledException();WindowsAiDog.Core.ReplyCommit.Apply(reply,pending.Token,committed=>{history.Add(new("user",text));history.Add(ConversationTurn.FromReply(committed));while(history.Count>12)history.RemoveAt(0);transcript.AppendText(pet.Settings.Name+": "+committed.Reply+"\n\n");pet.SayAi(committed.Reply);if(committed.Action is WindowsAiDog.Core.PetAction action)pet.Request(action);});status.Text="답변 완료";}
  catch(OperationCanceledException){status.Text="요청을 취소했거나 시간이 초과됐습니다.";}
  catch(InvalidDataException e){status.Text=e.Message.Contains("subscription_sharing")?"구독 사용량을 사용할 수 없습니다. ChatGPT 사용량을 확인하세요.":"응답이 중단됐거나 허용된 형식이 아닙니다. 행동은 실행하지 않았습니다.";}
  catch(HttpRequestException){status.Text="네트워크·로그인·사용량 오류입니다. 유료 API로 전환하지 않습니다.";}
  catch(Exception e) when(e is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException or ArgumentException){status.Text="연결 또는 응답 형식 오류입니다. 강아지의 로컬 기능은 계속 사용할 수 있습니다.";}
  finally{pending.Dispose();pending=null;SetBusy(false);if(status.Text!="답변 완료")pet.SayNotice(status.Text);if(transcript.Text.Length>24000)transcript.Text=transcript.Text[^20000..];transcript.ScrollToEnd();}
 }
 private void SetBusy(bool value){send.IsEnabled=!value;connect.IsEnabled=!value;input.IsReadOnly=value;input.Opacity=value?.65:1;busyChanged?.Invoke();}
}
