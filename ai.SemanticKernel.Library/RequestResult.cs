using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public readonly struct RequestResult
{
   public bool Ok { get; }
   public object? Data { get; }
   public string? Error { get; }

   private RequestResult(bool ok, object? data, string? error)
   { 
      Ok = ok; 
      Data = data; 
      Error = error; 
   }

   public static RequestResult Okey(object? data)
   {
      return new(true, data, null);
   }
   public static RequestResult Fail(string error)
   {
      return new(false, null, error);
   }
}

