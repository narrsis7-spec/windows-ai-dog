using System.IO;
using System.Net.Http;
using System.Text.Json;
namespace WindowsAiDog.Windows.Chat;
public sealed record LocalServer(string Name,string Endpoint,int ModelCount) {
 public override string ToString()=>$"{Name} · 모델 {ModelCount}개 · {Endpoint}";
}
// Finds OpenAI-compatible servers already running on this PC by asking each well-known default
// port for its model list. Only loopback addresses are probed; nothing is installed or started.
public static class LocalDiscovery {
 public static readonly IReadOnlyList<(string Name,string Endpoint)> Known=new[]{
  ("Ollama","http://127.0.0.1:11434/v1"),("LM Studio","http://127.0.0.1:1234/v1"),("llama.cpp · LocalAI","http://127.0.0.1:8080/v1"),
  ("vLLM","http://127.0.0.1:8000/v1"),("Jan","http://127.0.0.1:1337/v1"),("text-generation-webui","http://127.0.0.1:5000/v1")};
 public static async Task<IReadOnlyList<LocalServer>> FindAsync(HttpClient http,CancellationToken ct,TimeSpan? perServer=null){
  var probes=Known.Select(async k=>{
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(perServer??TimeSpan.FromMilliseconds(900));
   try{
    using var response=await http.GetAsync(ProviderEndpoint.Route(k.Endpoint,"models"),timeout.Token);if(!response.IsSuccessStatusCode)return null;
    var body=await response.Content.ReadAsStringAsync(timeout.Token);if(body.Length>1024*1024)return null;
    using var doc=JsonDocument.Parse(body);return new LocalServer(k.Name,k.Endpoint,doc.RootElement.GetProperty("data").GetArrayLength());
   }catch(Exception e) when(e is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or IOException){ct.ThrowIfCancellationRequested();return null;}
  }).ToArray();
  return (await Task.WhenAll(probes)).OfType<LocalServer>().ToList();
 }
}
