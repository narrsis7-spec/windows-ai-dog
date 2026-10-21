using System.Windows;
using System.Windows.Controls;
using WindowsAiDog.Windows.Chat;
using WindowsAiDog.Windows.Storage;
namespace WindowsAiDog.Windows.Views;
public sealed class ProviderSettingsWindow:Window {
 public ProviderSettingsWindow(ProviderManager manager) {
  Title="AI 제공자 연결";Width=540;Height=560;Background=System.Windows.Media.Brushes.White;
  var panel=new StackPanel{Margin=new Thickness(20)};Content=panel;
  panel.Children.Add(new TextBlock{Text="연결 방식을 직접 선택하세요. 자동 유료 전환은 없습니다.",TextWrapping=TextWrapping.Wrap});
  var picker=new ComboBox{ItemsSource=ProviderRegistry.All,SelectedItem=ProviderRegistry.Get(manager.Current.Id),Margin=new Thickness(0,12,0,10)};panel.Children.Add(picker);
  var billing=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};panel.Children.Add(billing);
  panel.Children.Add(new TextBlock{Text="OpenAI 호환 기본 URL (예: http://127.0.0.1:1234/v1)"});var url=new TextBox{MaxLength=2048,Margin=new Thickness(0,5,0,12)};panel.Children.Add(url);
  panel.Children.Add(new TextBlock{Text="직접 지정 모델 (선택; 모델 목록에서 선택 가능)"});var model=new TextBox{MaxLength=200,Margin=new Thickness(0,5,0,12)};panel.Children.Add(model);
  panel.Children.Add(new TextBlock{Text="새 API 키 (같은 URL의 기존 키만 유지; 로컬은 선택)"});var key=new PasswordBox{MaxLength=8192,Margin=new Thickness(0,5,0,10)};panel.Children.Add(key);
  var clear=new CheckBox{Content="이 제공자의 저장된 키 삭제",Margin=new Thickness(0,0,0,10)};panel.Children.Add(clear);
  panel.Children.Add(new TextBlock{Text="키는 이 앱의 제공자별 Windows DPAPI 저장소에만 보관됩니다. 변경 시 진행 요청과 이전 대화 기록을 지웁니다. 로컬 서버는 사용자가 실행해야 합니다.",TextWrapping=TextWrapping.Wrap});
  var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)};panel.Children.Add(status);
  var save=new Button{Content="저장하고 제공자 선택",Padding=new Thickness(10)};panel.Children.Add(save);
  void Refresh(){var d=(ProviderDescriptor)picker.SelectedItem;var c=manager.Configuration(d.Id);url.Text=c.Endpoint;model.Text=c.Model;key.Clear();clear.IsChecked=false;
   bool editable=d.Authentication!=AuthenticationKind.SubscriptionOAuth;model.IsEnabled=key.IsEnabled=clear.IsEnabled=editable;url.IsEnabled=editable&&d.Id!="openai-api";
   billing.Text=d.Billing switch{BillingMode.SubscriptionSharing=>"ChatGPT 구독 · 기존 Codex/Work 사용량 공유. OAuth 화면에서 직접 승인.",BillingMode.ApiUsage=>"별도 API 종량 과금 · ChatGPT/Claude/Gemini 소비자 구독과 다릅니다.",_=>"사용자가 실행한 로컬 서버 · 서버의 운영비/모델 조건은 별도입니다."};}
  picker.SelectionChanged+=(s,e)=>Refresh();Refresh();
  save.Click+=(s,e)=>{try{var d=(ProviderDescriptor)picker.SelectedItem;if(d.Authentication!=AuthenticationKind.SubscriptionOAuth)manager.Configure(new(d.Id,url.Text.Trim().TrimEnd('/'),model.Text.Trim()),key.Password,clear.IsChecked==true);manager.Select(d.Id);key.Clear();Close();}
   catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){status.Text="주소·키·모델 또는 저장 권한을 확인하세요. 키는 표시하지 않습니다.";key.Clear();}};
 }
}
