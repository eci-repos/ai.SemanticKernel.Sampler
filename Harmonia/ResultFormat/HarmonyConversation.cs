using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.ResultFormat;

public sealed class HarmonyConversation
{
   public List<HarmonyMessage> Messages { get; set; } = new();
}
