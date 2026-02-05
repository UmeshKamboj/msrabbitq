# MSRabbitQ - Marketing Microservices with RabbitMQ

A comprehensive .NET microservices application demonstrating SMS and Email marketing using RabbitMQ message queue. This project is designed to help you learn RabbitMQ from basic to advanced concepts with extensive comments and documentation.

## 🎯 Project Overview

This application demonstrates a real-world microservices architecture for marketing campaigns using:
- **Producer API**: HTTP REST API that receives marketing requests
- **SMS Consumer**: Service that processes SMS messages
- **Email Consumer**: Service that processes email messages
- **RabbitMQ**: Message broker that coordinates communication

## 📋 Table of Contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [Running the Application](#running-the-application)
- [API Usage](#api-usage)
- [RabbitMQ Concepts](#rabbitmq-concepts)
- [Learning Path](#learning-path)
- [Advanced Patterns](#advanced-patterns)

## 🏗️ Architecture

```
┌─────────────┐       ┌──────────────┐       ┌─────────────────┐
│   Client    │──────▶│  Producer    │──────▶│   RabbitMQ      │
│  (HTTP)     │       │     API      │       │   Exchange      │
└─────────────┘       └──────────────┘       └────────┬────────┘
                                                       │
                                        ┌──────────────┴──────────────┐
                                        │                             │
                                  ┌─────▼──────┐             ┌───────▼─────┐
                                  │ SMS Queue  │             │ Email Queue │
                                  └─────┬──────┘             └───────┬─────┘
                                        │                             │
                                  ┌─────▼──────┐             ┌───────▼─────┐
                                  │    SMS     │             │    Email    │
                                  │  Consumer  │             │  Consumer   │
                                  └────────────┘             └─────────────┘
```

### Message Flow

1. **Client** sends HTTP request to Producer API
2. **Producer API** validates request and publishes message to RabbitMQ
3. **RabbitMQ** routes message to appropriate queue based on routing key
4. **Consumer** receives message, processes it, and acknowledges
5. **RabbitMQ** removes acknowledged message from queue

## 🛠️ Prerequisites

- **.NET SDK 9.0** or higher
- **Docker** and **Docker Compose** (for containerized deployment)
- **Visual Studio 2022** or **VS Code** (optional, for development)

## 🚀 Getting Started

### Option 1: Using Docker Compose (Recommended)

1. **Clone the repository**
   ```bash
   git clone https://github.com/UmeshKamboj/msrabbitq.git
   cd msrabbitq
   ```

2. **Start all services**
   ```bash
   docker-compose up --build
   ```

3. **Access the services**
   - Producer API: http://localhost:5000
   - RabbitMQ Management UI: http://localhost:15672 (guest/guest)

### Option 2: Running Locally

1. **Start RabbitMQ**
   ```bash
   docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
   ```

2. **Run Producer API**
   ```bash
   cd src/Producer.API
   dotnet run
   ```

3. **Run SMS Consumer** (in new terminal)
   ```bash
   cd src/SMS.Consumer
   dotnet run
   ```

4. **Run Email Consumer** (in new terminal)
   ```bash
   cd src/Email.Consumer
   dotnet run
   ```

## 📁 Project Structure

```
msrabbitq/
├── src/
│   ├── Producer.API/          # HTTP API Gateway
│   │   ├── Program.cs         # API endpoints and RabbitMQ publisher
│   │   └── appsettings.json   # Configuration
│   │
│   ├── SMS.Consumer/          # SMS message processor
│   │   └── Program.cs         # Consumer logic with ACK/NACK
│   │
│   ├── Email.Consumer/        # Email message processor
│   │   └── Program.cs         # Consumer logic with ACK/NACK
│   │
│   └── Shared.Models/         # Shared code library
│       ├── MessageModels.cs   # Message classes
│       └── RabbitMQHelper.cs  # RabbitMQ utilities
│
├── docker-compose.yml         # Multi-container Docker setup
├── Dockerfile.producer        # Producer API Docker image
├── Dockerfile.sms             # SMS Consumer Docker image
├── Dockerfile.email           # Email Consumer Docker image
└── README.md                  # This file
```

## 🌐 API Usage

### Send SMS

```bash
curl -X POST http://localhost:5000/api/sms \
  -H "Content-Type: application/json" \
  -d '{
    "phoneNumber": "+1234567890",
    "message": "Hello! This is a test SMS from RabbitMQ microservices",
    "campaign": "test_campaign"
  }'
```

**Response:**
```json
{
  "status": "Accepted",
  "message": "SMS message queued for delivery",
  "messageId": "abc123-def456-ghi789",
  "timestamp": "2024-02-05T10:30:00Z"
}
```

### Send Email

```bash
curl -X POST http://localhost:5000/api/email \
  -H "Content-Type: application/json" \
  -d '{
    "to": "customer@example.com",
    "subject": "Welcome to our service!",
    "body": "Thank you for joining us. We are excited to have you!",
    "campaign": "welcome_series"
  }'
```

### Send Bulk Campaign

```bash
curl -X POST http://localhost:5000/api/campaign \
  -H "Content-Type: application/json" \
  -d '{
    "campaign": "black_friday_2024",
    "sms": {
      "message": "Black Friday Sale! 50% off everything!",
      "recipients": ["+1234567890", "+0987654321"]
    },
    "email": {
      "subject": "Black Friday - 50% OFF Everything!",
      "body": "Dear valued customer, don'\''t miss our biggest sale of the year!",
      "recipients": ["customer1@example.com", "customer2@example.com"]
    }
  }'
```

### Get Queue Statistics

```bash
curl http://localhost:5000/api/stats
```

**Response:**
```json
{
  "timestamp": "2024-02-05T10:35:00Z",
  "queues": {
    "sms": {
      "name": "sms_queue",
      "messageCount": 5,
      "consumerCount": 1
    },
    "email": {
      "name": "email_queue",
      "messageCount": 3,
      "consumerCount": 1
    }
  }
}
```

### Health Check

```bash
curl http://localhost:5000/health
```

## 📚 RabbitMQ Concepts

### Core Components

1. **Producer**: Sends messages to an exchange
   - In this project: `Producer.API`
   - Creates and publishes messages

2. **Exchange**: Routes messages to queues
   - Types: direct, topic, fanout, headers
   - We use: `direct` exchange with routing keys

3. **Queue**: Stores messages until consumed
   - `sms_queue`: Holds SMS messages
   - `email_queue`: Holds email messages

4. **Consumer**: Receives and processes messages
   - `SMS.Consumer`: Processes SMS
   - `Email.Consumer`: Processes emails

5. **Binding**: Links exchange to queue
   - Routing key "sms" → sms_queue
   - Routing key "email" → email_queue

### Message Reliability

**Persistence**
- Messages are marked as `persistent`
- Queues are `durable`
- Messages survive broker restart

**Acknowledgments (ACK/NACK)**
- `ACK`: "I processed this successfully, remove it"
- `NACK`: "I couldn't process this, requeue it"
- Manual ACK ensures at-least-once delivery

**Quality of Service (QoS)**
- `prefetchCount: 1`: Process one message at a time
- Ensures fair distribution among consumers
- Prevents consumer overload

## 🎓 Learning Path

### Level 1: Basics
1. Run the application with Docker Compose
2. Send a single SMS via API
3. Watch the console logs to see message flow
4. Check RabbitMQ Management UI (http://localhost:15672)

### Level 2: Understanding Messages
1. Review `MessageModels.cs` - understand message structure
2. Review `Program.cs` in Producer.API - see how messages are published
3. Review `Program.cs` in SMS.Consumer - see how messages are consumed
4. Experiment with different message payloads

### Level 3: RabbitMQ Operations
1. Study `RabbitMQHelper.cs` - understand exchange and queue declaration
2. Learn about routing keys and bindings
3. Explore different exchange types
4. Understand message persistence and durability

### Level 4: Error Handling
1. Study ACK/NACK logic in consumers
2. Understand requeue mechanism
3. Simulate failures (stop a consumer, send messages)
4. Watch how messages are redelivered

### Level 5: Scaling
1. Run multiple instances of SMS.Consumer
2. Observe load distribution
3. Understand competing consumers pattern
4. Experiment with prefetchCount values

### Level 6: Advanced Patterns
1. Implement Dead Letter Queues (DLQ)
2. Add retry limits with exponential backoff
3. Implement priority queues
4. Add message TTL (Time To Live)

## 🔥 Advanced Patterns

### Dead Letter Queue (DLQ)

Add this to queue declaration for handling permanently failed messages:

```csharp
var arguments = new Dictionary<string, object>
{
    { "x-dead-letter-exchange", "dlx_exchange" },
    { "x-dead-letter-routing-key", "failed" },
    { "x-message-ttl", 60000 } // 60 seconds
};

await channel.QueueDeclareAsync("sms_queue", true, false, false, arguments);
```

### Retry with Exponential Backoff

```csharp
int retryCount = GetRetryCount(message);
if (retryCount < 3)
{
    int delay = (int)Math.Pow(2, retryCount) * 1000;
    await Task.Delay(delay);
    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
}
else
{
    // Send to DLQ
    await channel.BasicNackAsync(ea.DeliveryTag, false, false);
}
```

### Priority Queues

```csharp
var arguments = new Dictionary<string, object>
{
    { "x-max-priority", 10 }
};

var properties = new BasicProperties
{
    Priority = 5  // 0-10 scale
};
```

### Message TTL

```csharp
var properties = new BasicProperties
{
    Expiration = "60000"  // Milliseconds
};
```

## 🔧 Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `RABBITMQ_HOSTNAME` | RabbitMQ server address | localhost |
| `RABBITMQ_PORT` | RabbitMQ AMQP port | 5672 |
| `RABBITMQ_USERNAME` | Authentication username | guest |
| `RABBITMQ_PASSWORD` | Authentication password | guest |
| `SMS_QUEUE` | SMS queue name | sms_queue |
| `EMAIL_QUEUE` | Email queue name | email_queue |

### appsettings.json

```json
{
  "RabbitMQ": {
    "Hostname": "localhost",
    "Port": "5672",
    "Username": "guest",
    "Password": "guest",
    "ExchangeName": "marketing_exchange",
    "SmsQueue": "sms_queue",
    "EmailQueue": "email_queue"
  }
}
```

## 📊 Monitoring

### RabbitMQ Management UI

Access at http://localhost:15672 (guest/guest)

**What to monitor:**
- Queue depths (message count)
- Consumer count per queue
- Message rates (publish/deliver/ack)
- Connection status
- Memory usage

### Application Logs

Each service outputs structured logs:
- Connection status
- Message processing
- Success/failure counts
- Statistics every 30 seconds

## 🐛 Troubleshooting

### Connection Refused

**Problem**: Can't connect to RabbitMQ
**Solution**: Ensure RabbitMQ is running
```bash
docker ps | grep rabbitmq
```

### Messages Not Being Consumed

**Problem**: Messages stuck in queue
**Solution**: Check if consumers are running
```bash
docker-compose logs sms-consumer
docker-compose logs email-consumer
```

### High Error Rate

**Problem**: Many messages failing
**Solution**: Check consumer logs for error details
- Validate message format
- Check external service availability
- Review retry logic

## 🤝 Contributing

This is a learning project. Feel free to:
- Add new message types
- Implement additional patterns
- Improve error handling
- Add monitoring and metrics
- Create integration tests

## 📖 Additional Resources

- [RabbitMQ Official Documentation](https://www.rabbitmq.com/documentation.html)
- [RabbitMQ .NET Client Guide](https://www.rabbitmq.com/dotnet-api-guide.html)
- [CloudAMQP Blog](https://www.cloudamqp.com/blog/index.html)
- [Microservices Patterns](https://microservices.io/patterns/index.html)

## 📝 License

MIT License - feel free to use this project for learning and development.

## 🙏 Acknowledgments

This project demonstrates best practices for:
- Microservices architecture
- Message-driven systems
- Asynchronous processing
- Distributed systems
- .NET development

Happy Learning! 🎉