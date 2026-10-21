using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WindowsAiDog.Windows.Chat;
using WindowsAiDog.Windows.Storage;
namespace WindowsAiDog.Windows.Views;
public sealed class ProviderSettingsWindow:Window {
 public ProviderSettingsWindow(ProviderManager manager) {
  Title="AI 제공자 연결";Width=560;SizeToContent=SizeToContent.Height;Background=System.Windows.Media.Brushes.White;
  var panel=new StackPanel{Margin=new Thickness(20)};Content=panel;
  panel.Children.Add(new TextBlock{Text="사용할 AI를 고르세요. 각 서비스는 본인 계정이나 키로 연결하며, 자동 유료 전환은 없습니다.",TextWrapping=TextWrapping.Wrap});
  var picker=new ComboBox{ItemsSource=ProviderRegistry.All,SelectedItem=ProviderRegistry.Get(manager.Current.Id),Margin=new Thickness(0,12,0,8)};panel.Children.Add(picker);
  var billing=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)};panel.Children.Add(billing);
  // Where to get a key for preset services (opens the system browser).
  var keyLink=new Hyperlink();var keyLinkText=new Run();keyLink.Inlines.Add(keyLinkText);keyLink.RequestNavigate+=(s,e)=>{Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});e.Handled=true;};
  var keyLinkBlock=new TextBlock{Margin=new Thickness(0,0,0,12)};keyLinkBlock.Inlines.Add(keyLink);panel.Children.Add(keyLinkBlock);
  var urlLabel=new TextBlock{Text="서버 주소"};panel.Children.Add(urlLabel);var url=new TextBox{MaxLength=2048,Margin=new Thickness(0,5,0,8)};panel.Children.Add(url);
  // Local servers: probe the usual ports and let the user pick one.
  var localRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)};var find=new Button{Content="내 PC에서 로컬 AI 찾기",Padding=new Thickness(10,4,10,4)};
  var found=new ComboBox{MinWidth=300,Margin=new Thickness(8,0,0,0),Visibility=Visibility.Collapsed};localRow.Children.Add(find);localRow.Children.Add(found);panel.Children.Add(localRow);
  found.SelectionChanged+=(s,e)=>{if(found.SelectedItem is LocalServer server)url.Text=server.Endpoint;};
  panel.Children.Add(new TextBlock{Text="모델 직접 지정 (선택 · 비워 두면 대화창에서 목록으로 선택)"});var model=new TextBox{MaxLength=200,Margin=new Thickness(0,5,0,12)};panel.Children.Add(model);
  var keyLabel=new TextBlock{Text="API 키 (새로 넣을 때만 입력 · 저장된 키는 표시하지 않음)"};panel.Children.Add(keyLabel);var key=new PasswordBox{MaxLength=8192,Margin=new Thickness(0,5,0,8)};panel.Children.Add(key);
  var clear=new CheckBox{Content="이 제공자의 저장된 키 삭제",Margin=new Thickness(0,0,0,10)};panel.Children.Add(clear);
  panel.Children.Add(new TextBlock{Text="키는 이 PC의 제공자별 Windows DPAPI 저장소에만 보관됩니다. 제공자를 바꾸면 진행 중 요청과 이전 대화를 지웁니다.",TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.DimGray});
  var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)};panel.Children.Add(status);
  var save=new Button{Content="저장하고 이 AI 사용",Padding=new Thickness(10)};panel.Children.Add(save);
  void Refresh(){
   var d=(ProviderDescriptor)picker.SelectedItem;var c=manager.Configuration(d.Id);url.Text=c.Endpoint;model.Text=c.Model;key.Clear();clear.IsChecked=false;status.Text="";found.Visibility=Visibility.Collapsed;found.ItemsSource=null;
   bool oauth=d.Authentication==AuthenticationKind.SubscriptionOAuth,local=d.Authentication==AuthenticationKind.LocalEndpoint;
   model.IsEnabled=!oauth;key.IsEnabled=clear.IsEnabled=!oauth;url.IsEnabled=!oauth&&!d.FixedEndpoint;
   urlLabel.Text=d.FixedEndpoint?"서버 주소 (이 서비스는 고정)":local?"로컬 서버 주소 (예: http://127.0.0.1:11434/v1)":"서버 주소 (OpenAI 호환 /v1 주소)";
   keyLabel.Text=local?"API 키 (로컬 서버는 보통 필요 없음)":"API 키 (새로 넣을 때만 입력 · 저장된 키는 표시하지 않음)";
   localRow.Visibility=local?Visibility.Visible:Visibility.Collapsed;
   if(d.KeyPage is string page){keyLink.NavigateUri=new Uri(page);keyLinkText.Text=d.DisplayName+" 키 발급 페이지 열기";keyLinkBlock.Visibility=Visibility.Visible;}else keyLinkBlock.Visibility=Visibility.Collapsed;
   billing.Text=d.Billing switch{
    BillingMode.SubscriptionSharing=>"ChatGPT 구독 · 기존 사용량을 함께 씁니다. 브라우저의 공식 로그인 화면에서 직접 승인합니다.",
    BillingMode.ApiUsage when d.Id=="anthropic"=>"Claude API · 사용한 만큼 Anthropic에 과금됩니다. Claude Pro 구독과는 별개입니다.",
    BillingMode.ApiUsage when d.Id=="gemini"=>"Gemini API · Google AI Studio 키를 씁니다(무료 한도 이후 과금). Gemini 앱 구독과는 별개입니다.",
    BillingMode.ApiUsage when d.Id=="openrouter"=>"OpenRouter · 키 하나로 여러 회사의 모델을 고를 수 있습니다. OpenRouter에 과금됩니다.",
    BillingMode.ApiUsage when d.Id=="compatible-api"=>"OpenAI 호환 주소를 가진 다른 서비스 · 해당 서비스에 과금됩니다.",
    BillingMode.ApiUsage=>d.DisplayName+" API · 사용한 만큼 해당 서비스에 과금됩니다.",
    _=>"내 PC에서 실행 중인 로컬 AI(Ollama, LM Studio 등) · 인터넷과 비용 없이 씁니다."};
  }
  picker.SelectionChanged+=(s,e)=>Refresh();Refresh();
  find.Click+=async(s,e)=>{
   find.IsEnabled=false;status.Text="로컬 AI를 찾는 중…";
   try{var servers=await manager.FindLocalServersAsync(default);found.ItemsSource=servers;found.Visibility=servers.Count>0?Visibility.Visible:Visibility.Collapsed;
    status.Text=servers.Count==0?"실행 중인 로컬 AI를 찾지 못했습니다. Ollama나 LM Studio를 켜고 다시 찾아 보세요.":$"{servers.Count}개를 찾았습니다. 사용할 서버를 고르세요.";if(servers.Count>0)found.SelectedIndex=0;}
   finally{find.IsEnabled=true;}
  };
  save.Click+=(s,e)=>{try{var d=(ProviderDescriptor)picker.SelectedItem;if(d.Authentication!=AuthenticationKind.SubscriptionOAuth)manager.Configure(new(d.Id,url.Text.Trim().TrimEnd('/'),model.Text.Trim()),key.Password,clear.IsChecked==true);manager.Select(d.Id);key.Clear();Close();}
   catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){status.Text="주소·키·모델 또는 저장 권한을 확인하세요. 키는 표시하지 않습니다.";key.Clear();}};
 }
}
