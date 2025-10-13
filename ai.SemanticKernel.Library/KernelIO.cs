using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

/// <summary>
/// Write to Kernel Console and in this case the System.Console.
/// </summary>
public class KernelIO : IKernelIO
{

   public static KernelIO _console { get; } = new KernelIO();
   public static KernelErrorIO _error { get; } = new KernelErrorIO();

   public static IKernelIO Console => _console;
   public static IKernelIO Error => _error;
   public static IKernelIO Log => _error;

   public KernelIO()
   {
      System.Console.OutputEncoding = Encoding.UTF8;
   }

   public string Write(string message)
   {
      System.Console.WriteLine(message);
      return message;
   }

   public string WriteLine(string? message = null)
   {
      System.Console.WriteLine(message == null ? String.Empty : message);
      return message;
   }

}

public class KernelErrorIO : IKernelIO
{

   public static KernelIO _console { get; } = new KernelIO();

   public static IKernelIO Error => _console;

   public KernelErrorIO()
   {
      System.Console.OutputEncoding = Encoding.UTF8;
   }

   public string Write(string message)
   {
      System.Console.Error.WriteLine(message);
      return message;
   }

   public string WriteLine(string message)
   {
      System.Console.Error.WriteLine(message);
      return message;
   }

}
