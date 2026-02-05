# Learning RabbitMQ - Step by Step Guide

This guide takes you from beginner to advanced RabbitMQ concepts through hands-on exercises.

## 📚 Learning Path

### Level 1: Getting Started (30 minutes)

#### Objectives
- Understand what RabbitMQ is
- Run the application
- Send your first message
- See it being consumed

#### Exercises

**1. Start the Application**
```bash
docker-compose up
```

**2. Send an SMS**
```bash
curl -X POST http://localhost:5000/api/sms \
  -H "Content-Type: application/json" \
  -d '{
    "phoneNumber": "+1234567890",
    "message": "My first RabbitMQ message!",
    "campaign": "learning"
  }'
```

**3. Observe the Logs**
- Producer API log: See message published
- SMS Consumer log: See message consumed
- Watch the emoji indicators 📤 📨 ✅

**4. Check RabbitMQ UI**
- Open http://localhost:15672 (guest/guest)
- Go to "Queues" tab
- See `sms_queue` and `email_queue`
- Observe message counts

#### Questions to Answer
1. What is a message queue?
2. Why use async messaging instead of direct HTTP calls?
3. What happens if the consumer is down when you send a message?

---

### Level 2: Understanding Messages (1 hour)

#### Objectives
- Understand message structure
- Learn about routing
- Explore message properties

#### Exercises

**1. Examine Message Model**

Open `src/Shared.Models/MessageModels.cs`:
- See `BaseMessage`, `SmsMessage`, `EmailMessage`
- Notice common fields: `Id`, `Timestamp`, `Campaign`
- Each message has a unique ID for tracking

**2. Send Different Message Types**

```bash
# SMS
curl -X POST http://localhost:5000/api/sms \
  -d '{"phoneNumber":"+1111111111","message":"Test 1","campaign":"test"}'

# Email
curl -X POST http://localhost:5000/api/email \
  -d '{"to":"test@example.com","subject":"Hello","body":"World","campaign":"test"}'
```

**3. Observe Routing**
- Both use the same exchange: `marketing_exchange`
- Different routing keys: `sms` vs `email`
- Messages go to different queues

**4. Try Invalid Messages**

```bash
# Missing required field
curl -X POST http://localhost:5000/api/sms \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"+1234567890"}'
```

Expected: 400 Bad Request with error message

#### Questions to Answer
1. What makes a routing key?
2. How does RabbitMQ know which queue to send a message to?
3. What happens to messages with invalid routing keys?

---

### Level 3: Exchanges and Queues (1.5 hours)

#### Objectives
- Understand exchanges
- Learn queue properties
- Explore bindings

#### Exercises

**1. Examine Exchange Setup**

Open `src/Producer.API/Program.cs`, find initialization:
```csharp
RabbitMQHelper.DeclareExchange(channel, exchangeName, "direct", durable: true);
```

- Exchange type: `direct`
- Why direct? Exact routing key matching
- Alternative types: topic, fanout, headers

**2. Check Queue Properties**

```csharp
RabbitMQHelper.DeclareQueue(channel, queueName, durable: true);
```

- `durable: true` - Queue survives broker restart
- Messages in durable queue also persist (if marked persistent)

**3. Understand Bindings**

```csharp
RabbitMQHelper.BindQueue(channel, smsQueue, exchangeName, "sms");
```

- Links queue to exchange
- Routing key "sms" → sms_queue
- One exchange → many queues

**4. Experiment in RabbitMQ UI**

1. Go to "Exchanges" tab
2. Click `marketing_exchange`
3. See bindings to queues
4. Try publishing message manually

#### Questions to Answer
1. What's the difference between direct and topic exchanges?
2. Can one queue be bound to multiple exchanges?
3. What happens if you publish with a routing key that has no bindings?

---

### Level 4: Consumers and Acknowledgments (2 hours)

#### Objectives
- Understand consumer patterns
- Learn about ACK/NACK
- Explore message reliability

#### Exercises

**1. Study Consumer Code**

Open `src/SMS.Consumer/Program.cs`:
- Event-based consumption
- Manual acknowledgment
- Error handling

**2. Test Message Acknowledgment**

Send a message:
```bash
curl -X POST http://localhost:5000/api/sms \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"+1234567890","message":"ACK test","campaign":"test"}'
```

Observe in logs:
- Message received
- Processing
- ACK sent
- Message removed from queue

**3. Simulate Consumer Failure**

Stop SMS consumer:
```bash
docker-compose stop sms-consumer
```

Send messages:
```bash
for i in {1..5}; do
  curl -X POST http://localhost:5000/api/sms \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"+111111111${i}\",\"message\":\"Test ${i}\",\"campaign\":\"test\"}"
done
```

Check stats:
```bash
curl http://localhost:5000/api/stats
```

See messages queued!

Restart consumer:
```bash
docker-compose start sms-consumer
```

Watch messages get processed!

**4. Test NACK and Requeue**

The consumer has simulated failures (5% failure rate).
Send many messages and watch some get requeued:

```bash
for i in {1..20}; do
  curl -X POST http://localhost:5000/api/sms \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"+${i}\",\"message\":\"Test\",\"campaign\":\"test\"}"
done
```

Watch consumer logs for:
- `❌ Error processing message`
- `🔄 Message requeued`
- Message retried and eventually succeeds

#### Questions to Answer
1. What's the difference between ACK and NACK?
2. Why manual ACK instead of auto-ACK?
3. What could go wrong if you ACK before processing?
4. What happens to messages if consumer crashes without ACK?

---

### Level 5: Scaling and Load Distribution (2 hours)

#### Objectives
- Scale consumers horizontally
- Understand competing consumers
- Learn about prefetch/QoS

#### Exercises

**1. Check Current Setup**

```bash
# See running containers
docker-compose ps

# Check queue stats
curl http://localhost:5000/api/stats
```

**2. Scale SMS Consumer**

```bash
docker-compose up -d --scale sms-consumer=3
```

You now have 3 SMS consumers!

**3. Send Many Messages**

```bash
# Use the test script
chmod +x examples/test-api.sh
./examples/test-api.sh
```

Or manually:
```bash
for i in {1..30}; do
  curl -X POST http://localhost:5000/api/sms \
    -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"+${i}\",\"message\":\"Load test ${i}\",\"campaign\":\"load_test\"}" &
done
wait
```

**4. Observe Load Distribution**

Check logs of all 3 consumers:
```bash
docker-compose logs sms-consumer | grep "Received SMS"
```

You'll see messages distributed among all 3!

**5. Experiment with Prefetch**

Edit `src/SMS.Consumer/Program.cs`:
```csharp
await channel.BasicQosAsync(
    prefetchSize: 0,
    prefetchCount: 5,  // Change from 1 to 5
    global: false
);
```

Rebuild and test how it affects distribution.

#### Questions to Answer
1. How does RabbitMQ distribute messages among consumers?
2. What role does prefetch play?
3. What happens if one consumer is slower than others?
4. How many consumers can you have on one queue?

---

### Level 6: Error Handling and Resilience (2 hours)

#### Objectives
- Implement Dead Letter Queue
- Add retry logic
- Handle permanent failures

#### Exercises

**1. Add Dead Letter Queue**

Create new file `src/SMS.Consumer/DeadLetterQueue.cs`:

```csharp
public static class DeadLetterQueueSetup
{
    public static async Task SetupDLQ(IChannel channel)
    {
        // Create DLX (Dead Letter Exchange)
        await channel.ExchangeDeclareAsync("dlx", "direct", durable: true);
        
        // Create DLQ
        await channel.QueueDeclareAsync("sms_dlq", durable: true);
        
        // Bind DLQ to DLX
        await channel.QueueBindAsync("sms_dlq", "dlx", "failed");
        
        // Update main queue to use DLX
        var args = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "dlx" },
            { "x-dead-letter-routing-key", "failed" }
        };
        
        await channel.QueueDeclareAsync("sms_queue", durable: true, 
            exclusive: false, autoDelete: false, arguments: args);
    }
}
```

**2. Add Retry Counter**

```csharp
private static int GetRetryCount(BasicProperties props)
{
    if (props.Headers != null && 
        props.Headers.TryGetValue("x-retry-count", out var count))
    {
        return Convert.ToInt32(count);
    }
    return 0;
}

// In error handling:
int retryCount = GetRetryCount(ea.BasicProperties);
if (retryCount < 3)
{
    // Requeue with incremented counter
    var newProps = new BasicProperties();
    newProps.Headers = new Dictionary<string, object>
    {
        { "x-retry-count", retryCount + 1 }
    };
    await channel.BasicPublishAsync("", "sms_queue", newProps, ea.Body);
    await channel.BasicAckAsync(ea.DeliveryTag, false);
}
else
{
    // Max retries, send to DLQ
    await channel.BasicNackAsync(ea.DeliveryTag, false, false);
}
```

**3. Test DLQ**

Send messages and watch failed ones go to DLQ after 3 retries.

#### Questions to Answer
1. When should you use a DLQ?
2. How do you monitor DLQ?
3. What do you do with messages in DLQ?

---

### Level 7: Advanced Patterns (3 hours)

#### Objectives
- Implement different exchange types
- Build pub/sub pattern
- Create request-reply pattern

#### Exercises

**1. Topic Exchange for Notifications**

Create a notification system that routes based on patterns:
- `notification.email.urgent` → Email queue
- `notification.sms.urgent` → SMS queue
- `notification.*.urgent` → Urgent queue (both)

**2. Fanout for Broadcasting**

Create an event system where one event goes to multiple services:
- User registered → Email service
- User registered → SMS service
- User registered → Analytics service
- User registered → CRM service

**3. Request-Reply Pattern**

Implement synchronous-style communication over async messaging.

---

## 🎓 Quiz Questions

### Beginner
1. What is RabbitMQ?
2. What's the difference between a queue and an exchange?
3. What does "durable" mean for queues?

### Intermediate
1. Explain the message flow from producer to consumer
2. What's the purpose of routing keys?
3. How does prefetch affect message distribution?

### Advanced
1. Design a system for handling failed messages
2. How would you implement priority messages?
3. How would you ensure exactly-once processing?

---

## 🏆 Projects to Build

### Beginner Projects
1. **To-Do Reminder System**: Send email reminders for due tasks
2. **Welcome Email System**: Send welcome emails to new users
3. **Notification Service**: Basic SMS/Email notifications

### Intermediate Projects
1. **Order Processing System**: Order → Payment → Fulfillment → Notification
2. **Multi-Channel Campaign Manager**: Schedule and send campaigns
3. **Event-Driven Microservices**: User service → Event → Multiple consumers

### Advanced Projects
1. **Saga Pattern Implementation**: Distributed transactions with compensation
2. **Priority Queue System**: VIP messages processed first
3. **Message Throttling**: Rate-limit outgoing messages
4. **Circuit Breaker**: Auto-disable failing consumers

---

## 📖 Additional Resources

### Official Documentation
- [RabbitMQ Tutorials](https://www.rabbitmq.com/getstarted.html)
- [.NET Client Guide](https://www.rabbitmq.com/dotnet-api-guide.html)
- [Best Practices](https://www.rabbitmq.com/best-practices.html)

### Books
- "RabbitMQ in Action" by Alvaro Videla
- "Enterprise Integration Patterns" by Gregor Hohpe

### Videos
- [RabbitMQ in 100 Seconds](https://www.youtube.com/watch?v=NQ3fZtyXji0)
- [Microservices Communication](https://www.youtube.com/watch?v=CZ3wIuvmHeM)

---

## ✅ Completion Checklist

- [ ] Level 1: Getting Started
- [ ] Level 2: Understanding Messages
- [ ] Level 3: Exchanges and Queues
- [ ] Level 4: Consumers and Acknowledgments
- [ ] Level 5: Scaling and Load Distribution
- [ ] Level 6: Error Handling and Resilience
- [ ] Level 7: Advanced Patterns
- [ ] Completed quiz questions
- [ ] Built at least one project

Congratulations on completing the RabbitMQ learning path! 🎉
