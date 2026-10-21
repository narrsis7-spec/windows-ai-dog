using System.Net.Http;
using WindowsAiDog.Windows.Storage;
namespace WindowsAiDog.Windows.Chat;
public sealed class ProviderManager {
 private readonly HttpClient http;private readonly string root;private readonly ProviderSettingsStore store;
 public event Action? Changed;
 public IChatProvider Current {get;private set;}
 public int Generation {get;private set;}
 public bool IsDisconnecting {get;private set;}
 private void EnsureAvailable(){if(IsDisconnecting)throw new InvalidOperationException("연결 해제가 진행 중입니다.");}
 public async Task<bool> DisconnectAsync(CancellationToken ct){EnsureAvailable();IsDisconnecting=true;try{return await Current.Auth.DisconnectAsync(ct);}finally{IsDisconnecting=false;Generation++;Changed?.Invoke();}}
 public ProviderManager(HttpClient http,string root,IChatProvider? injected=null){this.http=http;this.root=root;store=new(root);Current=injected??Create(store.Selected);}
 private IChatProvider Create(string id)=>id=="chatgpt"?new ChatGptProvider(http,root):new OpenAiCompatibleProvider(http,store.Load(id),store.DirectoryFor(id));
 public ProviderConfiguration Configuration(string id)=>store.Load(id);
 public void Configure(ProviderConfiguration config,string? replacementKey,bool clearKey=false){
  EnsureAvailable();if(replacementKey is not null&&(replacementKey.Length>8192||replacementKey.Any(char.IsControl)))throw new ArgumentException("키 형식을 확인하세요.");store.Save(config);var vault=new EndpointKeyStore(store.DirectoryFor(config.Id),config.Endpoint);
  if(clearKey)vault.Clear();else if(!string.IsNullOrWhiteSpace(replacementKey)){if(replacementKey.Length>8192||replacementKey.Any(char.IsControl))throw new ArgumentException("키 형식을 확인하세요.");vault.Save(replacementKey);}
  if(Current.Id==config.Id)Select(config.Id);
 }
 public void Select(string id){EnsureAvailable();var next=Create(id);store.SaveSelection(id);Current=next;Generation++;Changed?.Invoke();}
}
