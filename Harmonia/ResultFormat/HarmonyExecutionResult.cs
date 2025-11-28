using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmonia.ResultFormat;

public class HarmonyExecutionResult
{
   public string FinalText { get; set; } = string.Empty;
   public Dictionary<string, object?> Vars { get; set; } = new();
   public HarmonyError? Error { get; set; }

   public static HarmonyExecutionResult ErrorResult(
      string code, string message, object? details = null)
   {
      return new HarmonyExecutionResult
      {
         Error = new HarmonyError
         {
            Code = code,
            Message = message,
            Details = details
         }
      };
   }

   public static HarmonyExecutionResult ErrorResult(string finalText,
      string code, string message, object? details = null)
   {
      return new HarmonyExecutionResult
      {
         FinalText = finalText,
         Error = new HarmonyError
         {
            Code = code,
            Message = message,
            Details = details
         }
      };
   }

}

