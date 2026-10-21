using WindowsAiDog.Core;
namespace WindowsAiDog.Windows.Chat;
public sealed record ConversationTurn(string Role,string Text) {
 public static ConversationTurn FromReply(PetReply reply)=>new("assistant",reply.Reply);
}
public interface IChatClient {
 Task<PetReply> SendAsync(string token,string model,string userText,string petName,CancellationToken ct,IReadOnlyList<ConversationTurn>? history=null);
}
public interface IChatAuth {
 bool Connected {get;}
 Task ConnectAsync(CancellationToken ct);
 Task<string> AccessTokenAsync(CancellationToken ct);
 Task<IReadOnlyList<ModelChoice>> ModelsAsync(CancellationToken ct);
 Task<bool> DisconnectAsync(CancellationToken ct);
}
