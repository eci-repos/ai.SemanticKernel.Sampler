# Semantic Kernel with Harmony Response Format

## Harmony Response Format and Semantic Kernel

As documented by others...

As of today, **Semantic Kernel does not have first-class, built-in support for the Harmony Response Format (HRF)**. Semantic Kernel knows how to work with OpenAI’s *JSON / structured outputs* via `ResponseFormat` and JSON schema integration, and this is well-documented in the existing SK samples and guidance. However, there is currently no official SK integration that understands Harmony’s roles, channels, tool-calling headers, or its token-level encoding/decoding pipeline.

At the same time, the **gpt-oss** models are designed to be used with Harmony and expect their prompts and outputs to follow the Harmony Response Format. The Harmony project provides libraries and documentation for rendering and parsing those messages, but that support lives outside Semantic Kernel.

This sample is meant to bridge that gap for SK developers. By introducing:

- a `HarmonyConversation` / `HarmonyMessage` model around the Asian food script,
- a small `HarmonyParser` responsible for turning Harmony-formatted model output back into structured messages, and
- a `SemanticKernelInterop` layer that converts Harmony conversations to `ChatHistory` and routes `functions.*` tool calls into SK plugins,

the code here offers a **practical path to start working with Harmony Response Format from Semantic Kernel today**, even before there is official SK support. You can experiment with gpt-oss and HRF, wire up your own tools through SK plugins, and validate real end-to-end workflows, while keeping all of this integration code in your own application layer instead of waiting on Semantic Kernel to natively understand Harmony.

In short, this repository is intentionally opinionated and exploratory: it does not replace any future SK-native Harmony integration, but it gives you a concrete, working pattern you can adopt, adapt, or evolve right now if you want to use Harmony Response Format together with Semantic Kernel.

## Harmony Asian Food Preparation Planner

This repository demonstrates a practical approach to learning and exercising the **Harmony Response Format** by building a small but realistic domain: an **Asian food preparation planner**. Instead of starting with abstract examples, the sample leans on a concrete scenario that uses multiple tools, a multi-step Harmony script, a C# `HarmonyParser` and a food-focused test harness. Together, these pieces give you a full end-to-end path from Harmony messages to Semantic Kernel, through tool execution, and back to a structured final response.

The core idea is simple: treat Harmony not just as a wire format, but as a way to describe and execute a miniature workflow. In this project, that workflow is “help the user plan an Asian dinner,” but the same pattern could be reused for many other domains (trip planning, coding assistants, support triage, and so on). By grounding the workflow in food preparation, we also get natural opportunities to exercise planning, tool use, and structured outputs.

## Overview of the Approach

The sample is built around three main elements: a **Harmony script** for an “AsianFoodPrep” agent, a **C# runner and interop layer** that integrates Harmony with Semantic Kernel, and a **testing strategy** focused on food scenarios. The Harmony script defines how the assistant should extract input, call tools, handle failure cases and, ultimately, compose a final answer. The C# pieces are responsible for parsing and representing that script as a `HarmonyConversation`, converting it into `ChatHistory` for Semantic Kernel, running tools via a plugin, and capturing the final Harmony response. The tests then drive the whole loop using concrete food-planning prompts.

By starting with a single domain and pushing all the way from conversation JSON to tool calls and back, you gain a much deeper understanding of the Harmony Response Format than you would from isolated code snippets. You see how roles and channels are used, how tools are invoked from assistant messages, how tool results are fed back into the conversation, and how a final structured response can be validated against a response format schema.

## The Harmony Asian Food Script

At the heart of the sample is the **AsianFoodPrep Harmony script**, expressed as JSON within a system `harmony-script` message. The script declares a set of variables, such as `cuisineRegion`, `serves`, `totalTimeMinutes`, `recipes`, `pantry`, `shoppingList` and `prepSchedule`. It then defines a series of steps that describe the high-level flow:

1. Extract user input (cuisine preference, time budget, skill level, pantry contents) into script variables.
2. Call a recipe search tool to find candidate dishes that match the constraints.
3. Call a pantry tool to normalize what the user already has.
4. If no recipes are found, emit a final error message and halt.
5. Otherwise, call tools to generate a shopping list, build a preparation schedule and enrich the dishes with cultural background notes.
6. Finally, produce an assistant message on the `final` channel that summarizes the menu, the shopping list and the prep schedule in a structured way.

The script is intentionally more than a single “call this tool once” example. It includes branching with an `if` step, multiple tool calls, and a clear separation between intermediate analysis and the final answer. This shape mirrors realistic orchestrations you might want to implement with Harmony for other applications. The script is also designed to pair naturally with a response format (for example, an `asian_food_plan` JSON schema), encouraging the model to output machine-readable final results instead of only free-form text.

## Harmony Conversation and Parser

The Harmony script lives inside a larger **Harmony conversation** structure: an array of messages that includes system messages, a developer-style script message and a user message with a concrete request. The C# sample uses types like `HarmonyConversation` and `HarmonyMessage` to represent this conversation in memory. A loader function reads the JSON file containing the food script and deserializes it into these types, filling in the initial message list that seeds the interaction.

On top of this representation, you can add a `HarmonyParser` component responsible for turning raw model output (Harmony-formatted text or JSON) back into `HarmonyMessage` instances. In the simplest form, the sample uses a placeholder parser that wraps the entire reply as a single assistant commentary message, enough to wire up the loop and interop. Over time, this parser can be made more sophisticated: it can recognize assistant messages with different channels (`analysis`, `commentary`, `final`), detect tool calls via `recipient` fields and JSON bodies, and map tool results into messages with tool roles. Building and testing this parser is one of the most effective ways to become deeply familiar with the practical details of Harmony’s roles, channels and tool-calling conventions.

## Semantic Kernel Interop and Runner

To connect Harmony with Semantic Kernel, the sample uses a static **`SemanticKernelInterop`** class with extension methods like `ToChatHistory` and `ExecuteToolCallsAsync`. The `ToChatHistory` method walks the `HarmonyConversation` and converts messages into Semantic Kernel’s `ChatHistory`, mapping system and developer messages to system messages, user messages to user messages, assistant final messages to assistant outputs, and tool results to `AuthorRole.Tool` entries. Commentary messages that represent tool calls are not directly surfaced in `ChatHistory`; instead, they are consumed later when tools are executed.

The `ExecuteToolCallsAsync` method scans the conversation for assistant commentary messages that have a non-empty `Recipient`, such as `"functions.search_recipes"`. For each such message, it splits the recipient into a plugin namespace and function name, constructs `KernelArguments` from the JSON content of the message and looks up the corresponding Semantic Kernel function from the requested plugin. It then invokes the function through the `Kernel`, captures the result, and appends a new Harmony message representing the tool output. In cases where a tool cannot be found or where the arguments are invalid JSON, it appends a structured error payload instead, allowing the model to recover gracefully.

The **runner** brings everything together. It loads the Harmony conversation from JSON, then enters a loop where it converts the conversation to `ChatHistory`, calls the chat completion service, parses the assistant’s Harmony output back into `HarmonyMessage` objects, appends them, executes tool calls with `ExecuteToolCallsAsync` and finally checks for an assistant message on the `final` channel. When such a message appears, the runner prints it as the completed Asian food plan. This loop gives you a clear, inspectable pipeline from Harmony to Semantic Kernel and back again.

## AsianFoodTools Plugin

In order for the Harmony script’s tools to be executable, the sample defines an **`AsianFoodTools`** plugin and imports it into the Kernel under the `functions` namespace. Each method in this class is annotated with `[KernelFunction]` and uses a function name that exactly matches the Harmony tool names: `search_recipes`, `check_pantry`, `generate_shopping_list`, `build_prep_schedule` and `get_cultural_background`. The parameter names are written in snake_case to line up with the JSON argument keys in the Harmony script.

The methods are implemented as asynchronous functions returning `Task<string>`, where each string is JSON representing the tool’s result. In the sample, these implementations are stubs that return simple example payloads, but they can be replaced with real logic, calls to recipe databases, or any other data source. What matters for Harmony is that the arguments and return values align with the script’s expectations. This plugin design also makes it easy to inspect or adjust tool behavior without touching the core Harmony or Semantic Kernel infrastructure.

By keeping the plugin focused on the domain logic and letting `SemanticKernelInterop` handle the plumbing, the sample cleanly separates concerns: Harmony describes the workflow, the plugin provides the capabilities, and the runner orchestrates the interaction between the two.

## Food-Focused Testing Strategy

The food planning domain is not just thematically pleasant—it also lends itself to a meaningful **testing strategy**. Test cases can simulate different user prompts, such as a beginner asking for a quick Japanese dinner, a vegetarian wanting a spice-medium Thai meal, or a group with tight time constraints and limited pantry ingredients. Each test can load the same Harmony script, run the runner and inspect both the intermediate tool calls and the final Harmony output.

Tests can verify that the script correctly enters the error branch when no recipes are returned, that tools receive the expected arguments for given inputs, and that tool failures are surfaced back into the conversation as structured error messages. They can also validate the final assistant message by parsing its JSON content and checking it against an `asian_food_plan` schema, ensuring that the model respects the response format and produces fully structured output. Over time, you can add tests that deliberately break JSON, omit required fields or simulate tool timeouts, building confidence in your error handling and recovery paths.

This testing approach makes Harmony concrete. Instead of reasoning about the spec in the abstract, you see exactly how it behaves when recipes are missing, when the pantry is unusual, or when users specify conflicting constraints. The food domain gives you a rich set of variations without requiring complex external integrations.

## Why This Approach Is Effective for Learning Harmony

Taken together, the Asian food Harmony script, the `HarmonyParser`, the Semantic Kernel interop layer, the `AsianFoodTools` plugin and the food-oriented tests form a compact but powerful learning scaffold for Harmony Response Format. The script itself forces you to think in terms of variables, steps and control flow. The parser and interop classes require you to handle roles, channels and tools precisely. The plugin and tests make you confront the details of argument naming, JSON bodies and structured outputs.

Because the entire system is end-to-end and executable, you can iterate quickly: tweak the script, observe how tool calls change; modify the plugin, see how results affect the final answer; tighten the response format, and watch how the model’s output must adapt. This feedback loop is exactly what you need to move from theoretical understanding of the Harmony specification to confident, practical competence.

In short, this approach uses a focused, realistic scenario to exercise most of the important Harmony concepts without overwhelming you with unrelated complexity. Once you are comfortable with the Asian food planner, you will be well positioned to reuse the same patterns for other domains and to explore more advanced Harmony features with confidence.

