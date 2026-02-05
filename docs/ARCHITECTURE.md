# RabbitMQ Architecture and Design Patterns

This document provides in-depth explanations of RabbitMQ concepts and architectural patterns used in this project.

## Table of Contents

1. [Message Queue Fundamentals](#message-queue-fundamentals)
2. [Exchange Types Explained](#exchange-types-explained)
3. [Message Reliability](#message-reliability)
4. [Scaling Patterns](#scaling-patterns)
5. [Error Handling Strategies](#error-handling-strategies)

---

## Message Queue Fundamentals

### What is a Message Queue?

A message queue is an asynchronous communication pattern where:
- **Producers** send messages without waiting for processing
- **Messages** are stored in a queue
- **Consumers** process messages at their own pace

### Benefits

1. **Decoupling**: Services don't need to know about each other
2. **Scalability**: Add more consumers to handle load
3. **Reliability**: Messages persist even if services are down
4. **Load Leveling**: Queue buffers traffic spikes
5. **Flexibility**: Easy to add new consumers for same messages

### When to Use Message Queues

✅ **Good Use Cases:**
- Background job processing
- Event-driven architectures
- Microservices communication
- Sending notifications (email, SMS)
- Processing orders
- Data pipelines

❌ **Not Ideal For:**
- Real-time user interactions (use HTTP/WebSocket)
- Simple request-response patterns
- When immediate consistency is required

---

## Exchange Types Explained

### 1. Direct Exchange (Used in This Project)

Routes messages to queues based on exact routing key match.

**Example:**
```csharp
channel.BasicPublish("marketing_exchange", "sms", null, messageBytes);
```

### 2. Topic Exchange

Routes based on wildcard pattern matching using dot notation.

**Patterns:**
- `user.*.created` matches `user.profile.created`
- `order.#` matches `order.created`, `order.item.added`

### 3. Fanout Exchange

Broadcasts to ALL bound queues (ignores routing key).

### 4. Headers Exchange

Routes based on message header attributes instead of routing keys.

---

## Message Reliability

### Publisher Confirms

Ensure messages reach the broker.

### Consumer Acknowledgments

- `ACK`: Success, remove from queue
- `NACK (requeue=true)`: Retry
- `NACK (requeue=false)`: Send to DLQ

### Prefetch (QoS)

Limit unacknowledged messages per consumer for fair distribution.

---

## Scaling Patterns

### Competing Consumers

Multiple consumers on same queue for horizontal scaling.

### Work Queue

Distribute long-running tasks among workers.

### Pub/Sub

Broadcast events to multiple subscribers.

---

## Error Handling Strategies

### Dead Letter Queue (DLQ)

Store permanently failed messages for manual inspection.

### Retry with Backoff

Exponential backoff: 2^n seconds delay between retries.

### Circuit Breaker

Stop processing if downstream service is unavailable.

### Idempotency

Track processed message IDs to prevent duplicate processing.

---

For detailed patterns and code examples, see [official RabbitMQ documentation](https://www.rabbitmq.com/getstarted.html).
