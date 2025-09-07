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

## Dapr Setup
To use the Dapr integration with Semantic Kernel, ensure you have Dapr installed and running 
in your environment. You can follow the official Dapr installation guide [here](https://docs.dapr.io/getting-started/).

### Dapr SQL Server as the State Store
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
and the "sk_user" account has the necessary permissions; then run the following:

```
dapr run --app-id sk-app --resources-path ./components -- dotnet run
```

Upon successful execution, you should see the "dapr.dapr_metadata" and the "sk_state" tables created in your SQL Server database.
