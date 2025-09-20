# Semantic Kernel & Dapr Better Together

At this point I find myself in need to provide a solid foundation for the work in SK while interacting with 
services in an enterprise based distributed environment. For this reason I am providing sample code to demonstrate how to integrate 
SK with Dapr.  Take a look at the code in this repository to see how to implement a memory store and a chat plugin.

This is work in progress and I will be updating this readme as I go along.

NOTE: The code is based on some SK libraries that are in preview and may change.

## Overview

The sample code integrates Semantic Kernel (SK) with Dapr by implementing two core components that leverage Dapr's state management for persistence, using SQL Server as the underlying storage backbone. The first is a DaprMemoryStore that fulfills SK's IMemoryStore interface, enabling AI embeddings and metadata to be saved to and retrieved from a Dapr state store instead of volatile memory. This allows for permanent semantic memory and vector search across application restarts, with all data reliably persisted in SQL Server.

The second component is a ChatPlugin that uses Dapr to maintain stateful conversation history, with SQL Server providing the durable storage for all chat transcripts. It automatically persists each user and AI message exchange to the state store, scoped to a user ID. This provides the crucial ability to have continuous, multi-turn conversations with context that persists over time, which is a foundational requirement for chatbots and AI assistants.

Together, these classes demonstrate a powerful pattern: using Dapr as a persistent backbone for SK's stateful operations, with the robustness and familiarity of SQL Server providing the actual data persistence. This offloads complex state management from the AI logic, allowing SK to focus on reasoning and generation while Dapr and SQL Server handle scalability, durability, and transactional integrity for memories and conversations.

## Why Dapr + SK?

Microsoft Semantic Kernel (SK) becomes more powerful when paired with Dapr (Distributed Application Runtime) because Dapr simplifies building cloud-native, event-driven, and microservice-based systems. SK provides orchestration for AI capabilities—such as planning, memory, and skill composition—while Dapr abstracts away complex service-to-service communication, state management, and pub/sub messaging. This combination enables developers to integrate AI-driven workflows with reliable distributed systems without needing to reinvent infrastructure concerns like retries, scaling, or message delivery guarantees. In other words, SK focuses on AI reasoning, while Dapr ensures resilience, scalability, and interoperability across services.

**Common scenarios** where SK + Dapr shine include:

- **Intelligent microservices orchestration**: SK agents can trigger workflows across microservices via Dapr’s service invocation, making AI the decision layer while Dapr handles communication.
- **Event-driven AI automation**: AI models running through SK can subscribe to domain events (e.g., IoT telemetry, financial transactions) using Dapr pub/sub, analyze context, and take intelligent actions.
- **Stateful AI assistants**: Dapr’s state management (with pluggable stores like Redis, Cosmos DB, or PostgreSQL) enables SK to persist contextual memory and knowledge across sessions.
- **Cross-platform integrations**: With Dapr bindings, SK-powered applications can easily connect AI agents to external systems (queues, databases, messaging platforms), reducing integration friction.

## Benefits of SK + Dapr Integration

This integration successfully establishes a foundation where Semantic Kernel (SK) and Dapr are not just connected, but are beginning to function as complementary halves of a complete distributed AI application. SK acts as the stateless brain—orchestrating AI services, processing language, and managing reasoning—while Dapr, with SQL Server as its persistent storage layer, provides the stateful nervous system—durably managing memory, conversation history, and application state.

The logical and necessary next step is to fully leverage Dapr to provide the robust distributed application support that lies beyond SK's native capabilities. While SK excels at AI task orchestration, it is not designed for the complex orchestration of long-running, stateful, and fault-tolerant workflows across microservices. This is where Dapr's full suite of building blocks becomes critical.

By integrating Dapr's workflow engine for long-running processes, publish/subscribe for event-driven communication between AI and business services, and actors for managing stateful conversational agents, we can move beyond simple memory storage. This creates a system where an AI workflow can span hours or days, survive failures, coordinate across multiple services, and react to events—transforming SK from a powerful library into the core of a truly resilient, scalable, and enterprise-grade distributed application.

The primary benefits include:

- Simplified State Management & Memory Storage: SK's memory (e.g., for chat history, embeddings) often requires persistent storage. Dapr's State Store building block offers a simple, standardized API (GET, SET, DELETE) to interact with various databases (Redis, PostgreSQL, CosmosDB) without embedding complex, database-specific SDKs in your SK code. This makes the memory provider implementation clean, portable, and easily swappable.
- Publish/Subscribe for Event-Driven AI Workflows: SK workflows can be made reactive and decoupled. For instance, a "new document uploaded" event could trigger an SK plugin to summarize it. Dapr's Pub/Sub building block allows your SK application to publish events to or subscribe to events from topics (e.g., RabbitMQ, Azure Service Bus). This enables scalable, asynchronous processing of AI tasks.
- Secure External Service Invocation: SK plugins frequently need to call external APIs (e.g., CRM, databases). Dapr's Service Invocation building block provides secure, service-to-service calling with automatic mTLS and service discovery. This simplifies and secures the connections your AI agents make to other parts of your system.
- Configuration and Secret Management: Plugins often need API keys and connection strings. Dapr's Secrets building block allows you to securely retrieve secrets from a dedicated store (e.g., Azure Key Vault, HashiCorp Vault) at runtime, preventing you from hardcoding sensitive information in your SK application code.
- Portability and Standardization: By leveraging Dapr's standardized APIs, your SK application becomes agnostic to the underlying infrastructure. You can develop locally with Redis and deploy to production with CosmosDB by simply changing a Dapr configuration file, not your application code.

In essence, Dapr handles the complex, distributed systems problems, allowing developers to focus on the AI logic and prompt engineering within Semantic Kernel.

## Verify Chat History Persistence
After running the console app and having a conversation, you can verify that the chat history has been persisted in your SQL Server database. You can do this by querying the `sk_state` table that Dapr uses to store state information.

# SK & Dapr Sample Code
The sample code demonstrates how to implement a custom memory store and a chat plugin using Dapr's state management capabilities with SQL Server as the backend.
Before you run the code make sure that you have Dapr installed and running in your environment (see Dapr Setup section ahead for details).  First run Dapr sidecar and then run the console app or web app.

## Running the Sample Console App
Within the Sampler Console find the DaprChatWithHistory.RunAsync() invocation and undocument it as needed.

### Semantic Kernel flow (console)

- First, construct a Kernel with a chat completion service (model config). In your web example this is wrapped in a ChatService(new KernelModelConfig()); a console host would do the same initialization and then call the same service methods directly. 
- The ChatPlugin is registered against that Kernel. It internally creates a DaprClient, resolves the Kernel’s IChatCompletionService, and is imported as a Kernel plugin (so you can call its functions from your own code). The plugin’s state store name defaults to "statestore" but can be overridden. 
- When calling ChatAsync(userMessage, userId) from your console loop, it:

  - loads prior history from Dapr (LoadChatHistoryAsync),
  - appends the user message,
  - calls the model via GetChatMessageContentAsync(chatHistory, …),
  - appends the assistant reply, and
  - persists the updated history back to Dapr. It returns the assistant text for you to print to the console.

- For tooling, the plugin also exposes GetHistoryAsync(userId) (returns a formatted transcript) and ClearHistoryAsync(userId) (deletes persisted history). Your console host can wire these to commands like /history and /clear. 

### Dapr state and sidecar (console)

- All history I/O is through the Dapr sidecar using DaprClient and your chosen state store name (_storeName, default "statestore"). Keys are "chat-history-{userId}". Reads fall back to empty history if they fail; saves/deletes use the same name, so the component name must match (e.g., a component with metadata.name: statestore). 
- In a console run, you start the sidecar (dapr run … or dapr sidecar …) and then run your console process. The plugin’s DaprClient talks gRPC to the sidecar; ensure the gRPC port is reachable and matches your environment. (Your web sample shows the same dependency but hosted in ASP.NET; the console version keeps the same client calls.) 

### Optional SK memory via Dapr

- Also an IMemoryStore backed by Dapr was implemented, defaulting to "sqlstatestore". It namespaces keys as {collection}:{id} and maintains a simple per-collection index plus a global collections set—all persisted via Dapr state APIs. A console host can new up DaprMemoryStore and plug it into SK memory the same way as the web. 

- ### Putting it together (console host shape)

1. Build Kernel + model, register ChatPlugin (optionally pass your store name). 
2. In a read–eval–print loop, call:
   - ChatAsync(msg, userId) → print reply; persisted automatically,
   - GetHistoryAsync(userId) → print transcript,
   - ClearHistoryAsync(userId) → acknowledge cleared. 
3. Ensure your Dapr component’s metadata.name matches the store the plugin/memory store expects (statestore vs sqlstatestore) so GetStateAsync/SaveStateAsync/DeleteStateAsync succeed.

# Dapr Setup
To use the Dapr integration with Semantic Kernel, ensure you have Dapr installed and running 
in your environment. You can follow the official Dapr installation guide [here](https://docs.dapr.io/getting-started/).

## Dapr SQL Server as the State Store
To use the Dapr SQL State Store with Semantic Kernel, you need to configure the state store in your Dapr components.
Within your Dapr components directory, create a file named `sqlserver-statestore.yaml` with the following content:

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: sqlstatestore
spec:
  type: state.sqlserver
  version: v1
  metadata:
  - name: connectionString
    value: "Server=localhost,1433;Database=sk_db;User Id=sk_user;Password=YourStrong@Passw0rd;Encrypt=false;TrustServerCertificate=true;"
  - name: tableName
    value: "sk_state"
  - name: schema
    value: "dapr"
  - name: keyType
    value: "string"     # string | uuid | integer
  - name: keyLength
    value: "200"        # only applies to keyType=string
  - name: indexedProperties
    value: ""           # JSON string to create extra indexed columns
  - name: cleanupIntervalInSeconds
    value: "3600"       # TTL garbage-collector interval
```

Make sure to replace the connection string with your actual SQL Server connection details also make sure that the "sql_db" exists
and the "sk_user" account has the necessary permissions; then to start Dapr run the following:

```
dapr run --app-id sk-app --dapr-grpc-port 50001 --dapr-http-port 3500 --resources-path ./components
```

Upon successful execution, you should see the "dapr.dapr_metadata" and the "sk_state" tables created in your SQL Server database.

Verify that the Dapr sidecar is running by executing:
```
# Should return metadata; confirms the sidecar and HTTP port
Invoke-RestMethod http://localhost:3500/v1.0/metadata
```

### Debugging Dapr Applications
Note that to start Dapr sidecar without running your app just use the above command without the `-- dotnet run` part.
This is useful for debugging Dapr applications in isolation and you can then debug your .NET app separately in Visual Studio.

