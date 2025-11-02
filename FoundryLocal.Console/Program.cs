using System.Collections.Generic;
using System.IO;
using System.Configuration;
using Microsoft.Extensions.Configuration;
using ai.SemanticKernel.Library;

// See https://aka.ms/new-console-template for more information
// -------------------------------------------------------------------------------------------------
// make sure to start FoundryLocal.Api project before running this console app
// you may do so with something like: foundry chache list
Console.WriteLine("Test Foundry Models (using OpenAI model)");

// Bind to a list of records
var defaultConfig = ProviderInfo.GetDefaultConfiguration();
var host = KernelHost.PrepareKernelHost(defaultConfig);

await host.GetChatMessageContentAsync(
   "explain purpose of Harmony format for chat/prompt scripting");

return 0;

