
using ai.SemanticKernel.Workflow.Sampler.Console.ChainedWorkflow;
using ai.SemanticKernel.Workflow.Sampler.Console.ProcessWorkflow;
using Microsoft.Extensions.Hosting;

// Comment out the sample code you want to run

// -------------------------------------------------------------------------------------------------
// - Note that the following is not needed for some of the examples...
var builder = Host.CreateApplicationBuilder(args);

// [SINGLE EXECUTION] - Sentiment Analysis
//Console.WriteLine("Single Execution Sample");
//await SentimentAnalysisChainedWorkflow.RunChainedWorkflowAsync();

// [CHAINED WORKFLOW] - Sentiment Analysis
//Console.WriteLine("Chanined Workflow Sample");
//await SentimentAnalysisChainedWorkflow.RunChainedWorkflowAsync();

// [BATCH PROCESSING] - Sentiment Analysis
//Console.WriteLine("Batch Processing Sample");
//await SentimentAnalysisBatchProcess.RunBatchProcessAsync();

// [WORKFLOW PROCESSING] - Product Documentation
Console.WriteLine("Workflow Processing Sample");
await Processor.RunAsync();

var host = builder.Build();
await host.RunAsync();
