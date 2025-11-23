# Parsing JSON Harmony Response Format scripts

## Summary

This effort presents a comprehensive approach to implementing a JSON-based alternative to the Harmony Response Format (HRF) within a custom code library, eliminating reliance on external interpreters. The primary objective is to validate the parsing and execution of HRF-like scripts through a controlled, type-safe, and extensible runtime environment. While the included tools return synthetic data due to placeholder logic, this design emphasizes workflow orchestration rather than data fidelity.

The architecture is structured around two core components: format parsing and workflow execution. Parsing responsibilities are handled by classes such as HarmonyEnvelope, HarmonyMessage, and HarmonyStep, which deserialize JSON structures and normalize input for runtime use. Execution is managed by HarmonyExecutor, which processes steps sequentially, including tool calls, conditional logic, and templated assistant messages. A dedicated test harness (HarmoniaFoodExecutor) demonstrates reproducible execution using sample scripts and controlled user input, ensuring traceability and ease of debugging.

Key strengths of this implementation include clear separation of concerns, robust polymorphic deserialization, argument normalization, and resilience through fallback mechanisms. These design choices enhance maintainability, testability, and adaptability, positioning the system as a practical alternative to raw HRF specifications. Unlike schema-only approaches, this solution introduces deterministic control flow, template rendering, and integration with Semantic Kernel for seamless tool invocation.

Despite its advantages, the current implementation has limitations. Tool outputs are stubbed and inconsistent, the expression engine is minimal, and error handling is basic. Recommended next steps include replacing placeholder logic with real functionality, expanding expression and template capabilities, improving observability, and enforcing schema validation. These enhancements will transition the system from a proof-of-concept to a production-ready HRF runtime.

The significance of this approach lies in its ability to operationalize structured conversational workflows internally, granting developers full control over execution semantics and extensibility. By transforming HRF from a static specification into a dynamic runtime, this solution supports enterprise requirements for transparency, auditability, and integration. Furthermore, aligning with OpenAI HRF standards through stricter role semantics, metadata enforcement, and richer templating will ensure interoperability and foster trust across platforms.

In summary, this implementation bridges the gap between HRF as a specification and a robust execution engine, offering a scalable foundation for conversational workflows that can evolve independently of upstream changes. It represents a meaningful step toward standardization, portability, and governance in AI-driven orchestration.

## Overview & Intent

This sample demonstrates a JSON-based “Harmony Response Format” (HRF) alternative implemented entirely in your own code library, without relying on an external HRF interpreter. It parses a structured JSON document, constructs an internal workflow representation, and executes the plan step-by-step by orchestrating tool calls and assistant messages.

Importantly, the included HarmoniaFoodTools.cs contains TODO stubs—so returned data will be synthetic and may be inconsistent compared to what a full chat-completion or knowledge-based system would produce. This is by design: the sample is focused on validating parsing and execution of the HRF-alternative script, not on high-fidelity culinary results.

## Architecture: A Library-Driven HRF Alternative

The solution separates concerns into format parsing and workflow execution:

* Parsing: HarmonyEnvelope.cs, HarmonyMessage.cs, and HarmonyStep.cs define the JSON structures and provide robust deserialization helpers.
* Execution: HarmonyExecutor.cs walks the parsed steps and coordinates tool invocations using Semantic Kernel.
* Test Harness: HarmoniaFoodExecutor.cs drives the flow with FoodPlannerSample.1.json and registers the FoodTools plugin under the functions namespace to resolve tool calls.

This architecture is a clean alternative to raw HRF specs: instead of a monolithic, schema-only approach, you have a type-safe, testable, and extensible runtime that you control. It lets you evolve capabilities (evaluation rules, template rendering, error handling, instrumentation) without waiting on upstream spec changes.

## Data Model & Parsing: Envelope, Messages, Steps

**HarmonyMessage.cs**

* Retains raw Role as a string to preserve exact tool role names (e.g., functions.search_recipes).
* Captures Channel, Recipient, ContentType, Content, and optional termination flags.
* Uses JsonElement for Content to support both plain strings and structured payloads.

**HarmonyEnvelope.cs**

* Provides Parse to deserialize a full JSON conversation.
* Exposes helpers:
  * GetScript() to retrieve the harmony-script object from system messages.
  * GetPlainSystemPrompts() to collect plain system prompts.
  * GetUserMessage() to extract a single user message for initializing chat context.

These helpers are pragmatic: they normalize input from the HRF-like document into a runtime-friendly structure while keeping your options open for richer message types or multi-user interactions later.

**HarmonyStep.cs**

* Implements polymorphic step types via HarmonyStepJsonConverter:
  * ExtractInputStep for mapping user input into variables.
  * ToolCallStep with recipient, args, and save_as.
  * IfStep with condition, then, and else.
  * AssistantMessageStep with channel, content or content_template.
  * HaltStep to terminate execution.
* The converter reads type and dispatches to the appropriate class, giving you type safety and compile-time clarity while still consuming flexible JSON.

## Executor Orchestration: Step-by-Step Flow

**HarmonyExecutor.cs**

* Builds an initial chat history from plain system prompts and the user message.
* Initializes vars with script defaults, then applies extract-input mappings.
* Executes steps sequentially using ExecuteStepAsync, handling:
  * Tool calls: Resolves plugin.function via Semantic Kernel, normalizes arguments (including converting comma-separated strings to string[]), invokes the function, and stores results into vars[save_as].
  * Conditionals: Evaluates lightweight expressions, enabling control flow via if/then/else.
  * Assistant messages: Renders either content or content_template and sets FinalText for channel=final (or appends to chat for other channels).
  * Halt: Stops execution cleanly.

If the script doesn’t produce an explicit final output, the executor falls back to asking the chat completion service to summarize the results—useful for resilience. The orchestration shows a minimal yet complete runtime for HRF-like workflows.

## Expression Evaluation & Templates: Minimal but Practical

The internal ExecContext provides small, well-scoped utilities:

* Expression evaluation: Supports $vars.x, $input.x, $len(...), and $map(collection, 'prop').
* Boolean conditions: Handles comparisons (==, !=, <, <=, >, >=) and “truthiness”.
* Template rendering: Resolves {{vars.key}} and {{input.key}} (including dotted paths) via a simple mustache-like replacement.

This gives scripts enough computational expressiveness to pass data between steps and condition execution without embedding heavyweight logic. It’s a sweet spot between flexibility and maintainability.

## Tooling & Stubs: Intentionally Incomplete for Execution Testing

**HarmoniaFoodTools.cs**

* Provides the FoodTools Semantic Kernel plugin implementing:

  * search_recipes
  * check_pantry
  * generate_shopping_list
  * build_prep_schedule
  * get_cultural_background

* Each method currently returns static JSON stubs (marked with TODOs). This means outputs are synthetic and may contradict each other or lack real-world rigor (e.g., quantities, timing, substitutions). That is expected: the sample purposely focuses on verifying parsing and execution mechanics, not real culinary planning.

Important note: Because tool outputs are not backed by chat-completion or a knowledge base, responses will be inconsistent. The executor and script orchestration are correct, but the data fidelity will only improve once you replace TODOs with real logic or connect to retrieval/LLM services.

## Test Harness: Reproducible Execution

**HarmoniaFoodExecutor.cs**

* Loads FoodPlannerSample.1.json and provides explicit user input to the extract-input step (e.g., cuisine, serves, time budget).
* Imports the FoodTools plugin as functions, ensuring the script’s tool calls (e.g., functions.search_recipes) resolve at runtime.
* Prints both the final output and a snapshot of vars, making debugging and validation straightforward.

This harness is ideal for unit-style testing of the HRF-alternative pipeline: you can modify the JSON script, rerun, and inspect how inputs propagate through steps and how conditionals/timing behave.

## Why This Is a Meaningful Alternative to Raw HRF

Raw HRF (as commonly described in external specifications) is typically schema-first: it defines how a response should be structured but assumes an external interpreter and provides limited guidance on runtime semantics (expression evaluation, tool argument normalization, fallbacks, etc.). Your implementation:

* Adds a first-class runtime with clear, testable semantics for each step type.
* Provides type safety and structured deserialization, making scripts easier to reason about and refactor.
* Integrates directly with Semantic Kernel, allowing seamless tool discovery and invocation (plus future extensibility to memory, planners, or other SK services).
* Introduces deterministic control flow (conditions, halts) and templated finalization—features often left underspecified in raw formats.

In short, this example bridges the gap between “spec as data” and a robust execution engine, making HRF-like workflows operational inside your application stack.

## Strengths & Good Practices Observed

* Separation of concerns: Parsing (Envelope/Messages/Steps) vs. Execution (Executor).
* Polymorphic deserialization: Custom converter keeps JSON scripts flexible while preserving strong types.
* Argument normalization: Thoughtful handling of string[] parameters prevents common runtime mismatches.
* Resilience: Fallback finalization via chat-completion avoids empty user responses.
* Diagnostics-friendly: Vars snapshot and controlled final text aid traceability.

These choices make the system maintainable, testable, and adaptable.

## Limitations & Clear Next Steps

* Stubbed tools (TODOs): Replace synthetic outputs with real logic (recipe search, pantry NER, schedule computation) or connect to LLM-based functions via SK. Until then, expect inconsistent results.
* Minimal expression engine: Extend to arithmetic, more robust boolean logic, and error reporting (e.g., undefined vars).
* Template engine: Consider richer templating (looping, conditionals) while keeping scripts readable.
* Error handling: Add explicit exceptions and user-facing messages for missing plugins/functions and invalid tool outputs.
* Observability: Add structured logs/events per step; optionally expose a trace view for debugging.
* Schema validation: Validate the JSON script (e.g., JSON Schema) before execution to catch mistakes early.

These improvements will elevate the system from a mechanics test bed to a production-ready HRF alternative runtime.

## Why the Provided JSON HRF Implementation Is Important

The JSON-based HRF implementation is a critical step toward operationalizing structured conversational workflows without being locked into external interpreters or proprietary formats. By parsing and executing HRF-like scripts internally, this approach gives developers full control over execution semantics, error handling, and extensibility. It transforms HRF from a static specification into a dynamic runtime, enabling deterministic orchestration of tool calls, conditional logic, and templated responses. This is especially valuable for enterprise scenarios where transparency, auditability, and integration with existing systems (like Semantic Kernel) are essential.

Moreover, this implementation demonstrates how HRF can be language-agnostic and platform-neutral: the JSON script becomes a portable artifact, while the execution engine enforces consistent behavior across environments. This separation of concerns—data specification vs. runtime logic—lays the foundation for scalable, maintainable conversational workflows that can evolve independently of upstream spec changes.

## Enhancements Needed to Conform to OpenAI HRF Specifications

To fully align with OpenAI’s HRF guidelines, the JSON document should incorporate several enhancements:

1. Explicit Role and Channel Semantics
OpenAI HRF emphasizes clarity in roles (system, user, assistant, tool) and channels (analysis, final, commentary). While the current implementation supports these concepts, stricter validation and schema enforcement would ensure scripts adhere to the spec and avoid ambiguity.

2. Structured Content Types and Metadata
HRF often includes content_type markers (e.g., text, json) and termination indicators. Enhancing the JSON schema to mandate these fields—and validating them during parsing—would improve interoperability and make scripts predictable for downstream consumers.

3. Standardized Expression and Template Syntax
OpenAI HRF supports variable interpolation and conditional logic. The current implementation provides minimal $vars and $input evaluation, but expanding this to match HRF’s richer templating (loops, conditionals, formatting) would allow more expressive scripts while maintaining spec compliance.

4. Error and Halt Semantics
HRF defines clear termination states and error signaling. Incorporating these into the JSON schema and execution engine would make workflows more robust and spec-aligned, especially for complex multi-step plans.

## Why These Enhancements Matter

Conforming to OpenAI HRF specifications ensures interoperability with broader ecosystems, including LLM-based planners and external orchestration engines. It also guarantees predictability and portability: scripts authored for one HRF-compliant runtime can execute consistently elsewhere. For developers, this means reduced friction when integrating third-party tools or migrating workflows across platforms.

Finally, adherence to HRF standards fosters trust and transparency. By following a well-defined spec, organizations can audit workflows, validate compliance, and maintain governance over AI-driven processes—critical for regulated industries and enterprise deployments.