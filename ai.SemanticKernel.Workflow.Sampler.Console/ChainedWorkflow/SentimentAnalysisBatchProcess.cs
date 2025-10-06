using ai.SemanticKernel.Library;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace ai.SemanticKernel.Workflow.Sampler.Console.ChainedWorkflow;

public class SentimentAnalysisBatchProcess
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

   // Define the three documents
   private string[] _documents = new[]
   {
      // Document 1: Technology Article
      "Artificial Intelligence is transforming various industries by enabling machines to perform tasks that typically require human intelligence. Machine learning, a subset of AI, allows systems to learn and improve from experience without explicit programming. Recent advancements in deep learning and neural networks have significantly improved capabilities in areas like natural language processing, computer vision, and predictive analytics. Companies are leveraging AI for customer service chatbots, fraud detection, personalized recommendations, and autonomous vehicles. However, ethical considerations around bias, privacy, and job displacement remain important challenges that need to be addressed as AI continues to evolve and integrate into our daily lives.",
    
      // Document 2: Environmental Report
      "Climate change represents one of the most pressing challenges facing our planet today. Rising global temperatures are causing polar ice caps to melt, leading to sea level rise that threatens coastal communities worldwide. Extreme weather events including hurricanes, droughts, and wildfires are becoming more frequent and intense. The primary driver of climate change is the increased concentration of greenhouse gases in the atmosphere, largely due to human activities such as burning fossil fuels, deforestation, and industrial processes. Addressing climate change requires international cooperation, transition to renewable energy sources, reforestation efforts, and changes in consumption patterns. The Paris Agreement aims to limit global warming to well below 2 degrees Celsius above pre-industrial levels.",
    
      // Document 3: Health and Wellness Guide
      "Maintaining good mental health is essential for overall well-being and quality of life. Regular physical exercise has been shown to reduce symptoms of depression and anxiety while improving mood and cognitive function. Adequate sleep, typically 7-9 hours per night for adults, helps with emotional regulation and memory consolidation. Mindfulness practices such as meditation and deep breathing exercises can reduce stress and improve focus. Social connections and strong relationships provide emotional support and reduce feelings of isolation. A balanced diet rich in fruits, vegetables, and omega-3 fatty acids supports brain health. It's important to recognize when professional help is needed and to reduce the stigma around seeking mental health treatment when necessary."
   };

   public async Task RunBatchSentimentAnalysisAsync()
   {
      // Batch processing with error handling
      var results = new List<(string Summary, string KeyPoints, string Sentiment)>();

      for (int i = 0; i < _documents.Length; i++)
      {
         try
         {
            System.Console.WriteLine($"\nProcessing Document {i + 1}...");

            // Process each document through multiple functions
            var summaryResult = await Kernel.InvokeAsync(_textPlugins["SummarizeContent"],
                new KernelArguments { ["input"] = _documents[i] });

            var keyPointsResult = await Kernel.InvokeAsync(_textPlugins["ExtractKeyPoints"],
                new KernelArguments { ["input"] = _documents[i] });

            var sentimentResult = await Kernel.InvokeAsync(_textPlugins["AnalyzeSentiment"],
                new KernelArguments { ["input"] = _documents[i] });

            results.Add((
                summaryResult.GetValue<string>() ?? "No summary generated",
                keyPointsResult.GetValue<string>() ?? "No key points extracted",
                sentimentResult.GetValue<string>() ?? "No sentiment analysis"
            ));

            // Display results for each document
            System.Console.WriteLine($"Document {i + 1} Summary:\n{summaryResult}");
            System.Console.WriteLine($"\nKey Points:\n{keyPointsResult}");
            System.Console.WriteLine($"\nSentiment Analysis:\n{sentimentResult}");
            System.Console.WriteLine(new string('-', 50));
         }
         catch (Exception ex)
         {
            System.Console.WriteLine($"Error processing document {i + 1}: {ex.Message}");
            results.Add(("Error", "Error", "Error"));
         }
      }

      // Aggregate results analysis
      System.Console.WriteLine("\n=== BATCH PROCESSING SUMMARY ===");
      System.Console.WriteLine($"Total documents processed: {_documents.Length}");
      System.Console.WriteLine(
         $"Successful processing: {results.Count(r => r.Summary != "Error")}");

      // Display comparative analysis
      for (int i = 0; i < results.Count; i++)
      {
         if (results[i].Summary != "Error")
         {
            System.Console.WriteLine($"\nDocument {i + 1} Insights:");
            System.Console.WriteLine($"- Sentiment: {ExtractSentiment(results[i].Sentiment)}");
            System.Console.WriteLine(
               $"- Key Points Count: {CountBulletPoints(results[i].KeyPoints)}");
         }
      }

      // Helper methods for analysis
      string ExtractSentiment(string analysisText)
      {
         if (analysisText.Contains("Sentiment: Positive")) return "Positive";
         if (analysisText.Contains("Sentiment: Negative")) return "Negative";
         if (analysisText.Contains("Sentiment: Neutral")) return "Neutral";
         return "Unknown";
      }

      int CountBulletPoints(string keyPointsText)
      {
         return keyPointsText.Split('\n')
             .Count(line => line.Trim().StartsWith("-") || line.Trim().StartsWith("•"));
      }

   }

   /// <summary>
   /// Static method to run the batch process.
   /// </summary>
   /// <returns></returns>
   public static async Task RunBatchProcessAsync()
   {
      var workflow = new SentimentAnalysisBatchProcess();
      workflow.PrepareKernel();
      await workflow.RunBatchSentimentAnalysisAsync(); 
   }

}
