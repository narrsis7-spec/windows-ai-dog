using System.Net.Http;
namespace WindowsAiDog.Windows.Chat;
public enum BillingMode { SubscriptionSharing,ApiUsage,Local }
public interface IChatProvider {
 string Id {get;} string DisplayName {get;} BillingMode Billing {get;}
 IChatAuth Auth {get;} IChatClient Client {get;}
}
public sealed class ChatGptProvider:IChatProvider {
 public string Id=>"chatgpt";
 public string DisplayName=>"ChatGPT";
 public BillingMode Billing=>BillingMode.SubscriptionSharing;
 public IChatAuth Auth {get;}
 public IChatClient Client {get;}
 public ChatGptProvider(HttpClient http,string directory){Auth=new ChatGptAuthService(http,directory);Client=new ResponsesChatClient(http);}
}
