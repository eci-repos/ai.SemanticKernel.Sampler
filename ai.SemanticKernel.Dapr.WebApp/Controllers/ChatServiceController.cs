using ai.SemanticKernel.Dapr.Library.Services;
using ai.SemanticKernel.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Dapr.WebApp.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChatServiceController : ControllerBase
{
   private readonly ChatService _chatService;
   public ChatServiceController()
   {
      _chatService = new ChatService(new KernelConfig());
   }
   [HttpPost("chat")]
   public async Task<IActionResult> Chat([FromQuery] string userId, [FromQuery] string message)
   {
      var result = await _chatService.SendMessageAsync(userId, message);
      return Ok(new { reply = result });
   }
   [HttpGet("history")]
   public async Task<IActionResult> GetHistory([FromQuery] string userId)
   {
      var result = await _chatService.GetHistoryAsync(userId);
      return Ok(new { history = result });
   }
   [HttpPost("clear")]
   public async Task<IActionResult> ClearHistory([FromQuery] string userId)
   {
      var msg = await _chatService.ClearHistoryAsync(userId);
      return Ok(new { message = msg });
   }
   [HttpGet("status")]
   public IActionResult Status()
   {
      return Ok(new { status = "Chat Service is running" });
   }
}
