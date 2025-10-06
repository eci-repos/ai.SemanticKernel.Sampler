using ai.SemanticKernel.Library;
using Microsoft.SemanticKernel;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Workflow.Sampler.Console.ChainedWorkflow;

public class SentimentAnalysisChainedWorkflow
{

   private KernelHost _kernelInstance;
   private KernelPlugin _textPlugins;

   public Kernel Kernel
   {
      get { return _kernelInstance.Instance; }
   }

   public static KernelConfig GetConfig()
   {
      var config = new KernelConfig();
      return config;
   }

   /// <summary>
   /// Prepare a Kernel instance with a simple Sentiment Analysis plugin.
   /// </summary>
   /// <param name="config"></param>
   /// <returns>A Kernel instance is returned</returns>
   public Kernel PrepareKernel(KernelConfig? config = null)
   {
      if (config == null)
      {
         config = GetConfig();
      }

      _kernelInstance = new KernelHost(config);

      // Import the TextPlugin from the Plugins directory
      _textPlugins = Kernel.ImportPluginFromPromptDirectory("Plugins/TextPlugin");

      return Kernel;
   }

   /// <summary>
   /// Run the Sentiment Analysis function from the TextPlugin.
   /// </summary>
   /// <returns>Sentiment analysis results are returned</returns>
   public async Task<FunctionResult> ExecSentimentAnalysisAsync()
   {
      FunctionResult sentimentResult;
      // Run the Sentiment Analysis function from the TextPlugin
      try
      {
         sentimentResult = await Kernel.InvokeAsync(
            _textPlugins["AnalyzeSentiment"],
            new KernelArguments { ["input"] = "I love programming with Semantic Kernel!" }
            );
      }
      catch (System.Exception ex)
      {
         System.Console.WriteLine($"Error during sentiment analysis: {ex.Message}");
         throw;
      }

      return sentimentResult;
   }

   /// <summary>
   /// Run a changed workflow that first analyzes sentiment and then generates a response based on 
   /// that sentiment.
   /// </summary>
   /// <returns>Workflow result is returned</returns>
   public async Task<FunctionResult> ExecChainedWorkflowAsync()
   {
      var workflowResult = await Kernel.InvokeAsync(_textPlugins["AnalyzeSentiment"],
           new KernelArguments { ["input"] = "This service is terrible and slow." })
        .ContinueWith(async sentimentResult =>
        {
           var sentiment = sentimentResult.Result.GetValue<string>();
           return await Kernel.InvokeAsync(_textPlugins["GenerateResponse"],
              new KernelArguments
              {
                 ["input"] = "This service is terrible and slow.",
                 ["sentiment"] = sentiment
              });
        });
      return workflowResult.Result;
   }

   /// <summary>
   /// Run the Sentiment Analysis sample async.
   /// </summary>
   public static async Task RunSentimentAnalysisAsync()
   {
      var sentiment = new SentimentAnalysisChainedWorkflow();
      sentiment.PrepareKernel();

      var result = await sentiment.ExecSentimentAnalysisAsync();
      System.Console.WriteLine("Sentiment Analysis Result:");
      System.Console.WriteLine(result);
   }

   /// <summary>
   /// Run the Chained Workflow sample async.
   /// </summary>
   public static async Task RunChainedWorkflowAsync()
   {
      var sentiment = new SentimentAnalysisChainedWorkflow();
      sentiment.PrepareKernel();

      var result = await sentiment.ExecChainedWorkflowAsync();
      System.Console.WriteLine("Chained Workflow Result:");
      System.Console.WriteLine(result);
   }


}
