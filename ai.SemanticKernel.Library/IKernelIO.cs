using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Library;

public interface IKernelIO
{
   string Write(string message);
   string WriteLine(string? message = null);
}

