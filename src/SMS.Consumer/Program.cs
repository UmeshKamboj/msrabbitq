/**
 * SMS Consumer Service
 * 
 * This microservice consumes SMS messages from RabbitMQ and processes them.
 * It demonstrates the Consumer pattern in a message-driven architecture.
 * 
 * Key Concepts Demonstrated:
 * 1. Message Consumption: Receiving messages from a queue
 * 2. Message Acknowledgment: Confirming successful processing
 * 3. Error Handling: Dealing with processing failures
 * 4. Prefetch/QoS: Controlling message flow
 * 5. Graceful Shutdown: Proper cleanup on service stop
 * 
 * Consumer Flow:
 * 1. Connect to RabbitMQ and create channel
 * 2. Subscribe to SMS queue
 * 3. Receive messages one at a time (prefetch = 1)
 * 4. Process message (simulate sending SMS)
 * 5. Acknowledge (ACK) on success or Negative Acknowledge (NACK) on failure
 * 6. Repeat until shutdown signal
 * 
 * Scaling:
 * Multiple instances of this service can run concurrently.
 * RabbitMQ distributes messages among available consumers (competing consumers pattern).
 */

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Models;
using System.Text;

Console.WriteLine("🚀 Starting SMS Consumer Service...");

// Read RabbitMQ configuration from environment variables
var hostname = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost";
var port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672");
var username = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest";
var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";
var queueName = Environment.GetEnvironmentVariable("SMS_QUEUE") ?? "sms_queue";

Console.WriteLine($"🔌 Connecting to RabbitMQ at {hostname}:{port}");

// Create connection factory with retry and recovery settings
var factory = RabbitMQHelper.CreateConnectionFactory(hostname, port, username, password);

// Create connection to RabbitMQ server
// Connection represents a TCP connection and is expensive to create
var connection = await factory.CreateConnectionAsync();
Console.WriteLine("✅ Connected to RabbitMQ");

// Create channel for communication
// Channels are lightweight and can be created freely
var channel = await connection.CreateChannelAsync();
Console.WriteLine("📡 Channel created");

// Ensure queue exists (idempotent operation)
// If queue already exists, this just confirms its configuration
RabbitMQHelper.DeclareQueue(channel, queueName, durable: true);

/**
 * Set Quality of Service (QoS) - Prefetch Count
 * 
 * prefetchCount: Number of unacknowledged messages a consumer can have
 * Setting to 1 means: "Give me one message at a time, wait for ACK before sending next"
 * 
 * Benefits:
 * - Fair distribution: Prevents one consumer from being overloaded
 * - Better load balancing among multiple consumers
 * - Backpressure: Slows down message delivery if consumer is busy
 * 
 * In production, you might set this higher (e.g., 10-50) for better throughput,
 * but 1 is good for learning and debugging.
 */
await channel.BasicQosAsync(
    prefetchSize: 0,      // No specific byte limit
    prefetchCount: 1,     // Process one message at a time
    global: false         // Applied per consumer, not per channel
);
Console.WriteLine("⚙️  QoS set: prefetchCount = 1 (one message at a time)");

// Statistics tracking
int totalProcessed = 0;
int totalSuccess = 0;
int totalErrors = 0;

/**
 * Create an async event-based consumer
 * This is the modern approach for consuming messages in .NET
 */
var consumer = new AsyncEventingBasicConsumer(channel);

/**
 * Message Received Event Handler
 * This is called every time a message is delivered to this consumer
 */
consumer.ReceivedAsync += async (model, ea) =>
{
    totalProcessed++;
    var body = ea.Body.ToArray();
    
    try
    {
        // Deserialize message from JSON to SmsMessage object
        var smsMessage = RabbitMQHelper.DeserializeMessage<SmsMessage>(body);
        
        if (smsMessage == null)
        {
            throw new Exception("Failed to deserialize message");
        }
        
        Console.WriteLine($"\n📨 Received SMS Message #{totalProcessed}");
        Console.WriteLine($"   Message ID: {smsMessage.Id}");
        Console.WriteLine($"   Phone: {smsMessage.PhoneNumber}");
        Console.WriteLine($"   Message: {smsMessage.Message}");
        Console.WriteLine($"   Campaign: {smsMessage.Campaign}");
        Console.WriteLine($"   Timestamp: {smsMessage.Timestamp}");
        
        // Check if this is a redelivered message (retry)
        if (ea.Redelivered)
        {
            Console.WriteLine($"   ⚠️  This is a redelivered message (retry attempt)");
        }
        
        // Process the SMS message
        await ProcessSmsMessage(smsMessage);
        
        /**
         * Acknowledge (ACK) the message
         * This tells RabbitMQ: "I successfully processed this message, remove it from queue"
         * 
         * CRITICAL: Only ACK after successful processing!
         * If you ACK before processing and the process crashes, message is lost forever.
         * 
         * Parameters:
         * - deliveryTag: Unique identifier for this delivery
         * - multiple: If true, ACK this and all previous unacked messages
         */
        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        
        totalSuccess++;
        Console.WriteLine($"✅ Message acknowledged and removed from queue");
        Console.WriteLine($"📊 Stats: Processed: {totalProcessed}, Success: {totalSuccess}, Errors: {totalErrors}");
    }
    catch (Exception ex)
    {
        totalErrors++;
        Console.WriteLine($"❌ Error processing message: {ex.Message}");
        
        /**
         * Negative Acknowledge (NACK) the message
         * This tells RabbitMQ: "I couldn't process this message"
         * 
         * Options:
         * 1. Requeue (requeue=true): Put message back in queue for retry
         *    - Use for transient errors (network issues, temporary service outage)
         *    - Risk: Infinite retry loop if error is permanent
         * 
         * 2. Dead Letter Queue (requeue=false with DLQ configured):
         *    - Use for permanent errors that need manual inspection
         *    - Requires DLQ setup in queue declaration
         * 
         * 3. Reject permanently (requeue=false without DLQ):
         *    - Message is discarded
         *    - Use for invalid/malformed messages
         * 
         * Best Practice:
         * - Implement retry counter in message headers
         * - Requeue up to N times, then send to DLQ
         * - Monitor DLQ and set up alerts
         */
        
        // For this demo, we'll requeue failed messages
        // In production, check retry count and use DLQ for permanently failed messages
        await channel.BasicNackAsync(
            deliveryTag: ea.DeliveryTag,
            multiple: false,
            requeue: true  // Put back in queue for retry
        );
        
        Console.WriteLine($"🔄 Message requeued for retry");
    }
};

/**
 * Start consuming messages from the queue
 * 
 * Parameters:
 * - queue: Name of queue to consume from
 * - autoAck: If true, messages are auto-acknowledged (NOT RECOMMENDED)
 *            We use false for manual ACK to ensure reliability
 * - consumer: The event handler for received messages
 */
await channel.BasicConsumeAsync(
    queue: queueName,
    autoAck: false,  // Manual acknowledgment for reliability
    consumer: consumer
);

Console.WriteLine($"👂 Listening for messages on queue: {queueName}");
Console.WriteLine($"⏳ Waiting for SMS messages... (Press Ctrl+C to exit)");

// Statistics reporting timer
var statsTimer = new Timer(_ =>
{
    Console.WriteLine($"\n📊 === SMS Consumer Statistics ===");
    Console.WriteLine($"   Total Processed: {totalProcessed}");
    Console.WriteLine($"   Successful: {totalSuccess}");
    Console.WriteLine($"   Errors: {totalErrors}");
    Console.WriteLine($"   Success Rate: {(totalProcessed > 0 ? (totalSuccess * 100.0 / totalProcessed).ToString("F2") : "0")}%");
    Console.WriteLine($"==================================\n");
}, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

// Wait indefinitely (service keeps running)
// In production, you'd use IHost with proper lifecycle management
var exitEvent = new ManualResetEvent(false);

// Handle graceful shutdown on Ctrl+C
Console.CancelKeyPress += async (sender, e) =>
{
    e.Cancel = true; // Prevent immediate termination
    
    Console.WriteLine("\n🛑 Shutdown signal received...");
    Console.WriteLine($"📊 Final Statistics:");
    Console.WriteLine($"   Total Processed: {totalProcessed}");
    Console.WriteLine($"   Successful: {totalSuccess}");
    Console.WriteLine($"   Errors: {totalErrors}");
    
    // Cleanup
    statsTimer.Dispose();
    await channel.CloseAsync();
    await connection.CloseAsync();
    Console.WriteLine("✅ Disconnected from RabbitMQ");
    Console.WriteLine("👋 SMS Consumer stopped gracefully");
    
    exitEvent.Set();
};

exitEvent.WaitOne();

/**
 * Simulates processing an SMS message
 * 
 * In a production environment, this would:
 * 1. Validate phone number format
 * 2. Call SMS gateway API (Twilio, AWS SNS, etc.)
 * 3. Handle rate limits
 * 4. Store delivery status in database
 * 5. Send delivery reports
 * 6. Update analytics
 */
async Task ProcessSmsMessage(SmsMessage sms)
{
    // Validate message
    if (string.IsNullOrWhiteSpace(sms.PhoneNumber) || string.IsNullOrWhiteSpace(sms.Message))
    {
        throw new Exception("Invalid SMS: missing phone number or message");
    }
    
    // Simulate API call to SMS gateway (e.g., Twilio)
    Console.WriteLine($"📱 Sending SMS to {sms.PhoneNumber}...");
    
    // Simulate network delay
    await Task.Delay(500);
    
    // Simulate 95% success rate for demonstration
    var random = new Random();
    if (random.Next(100) < 95)
    {
        Console.WriteLine($"✉️  SMS sent successfully!");
        Console.WriteLine($"   Provider: SMS-Gateway-Simulator");
        Console.WriteLine($"   Delivery Status: Sent");
        
        // In production:
        // - Call actual SMS provider API
        // - Store delivery receipt
        // - Update campaign analytics
        // - Trigger any follow-up workflows
    }
    else
    {
        // Simulate occasional failures (network issues, rate limits, etc.)
        throw new Exception("SMS gateway temporarily unavailable");
    }
}
