/**
 * Email Consumer Service
 * 
 * This microservice consumes Email messages from RabbitMQ and processes them.
 * Similar to SMS Consumer but optimized for email-specific workflows.
 * 
 * Key Differences from SMS Consumer:
 * 1. Email processing typically takes longer (HTML rendering, attachments)
 * 2. Different validation rules (email format vs phone format)
 * 3. Different provider APIs (SendGrid, AWS SES vs Twilio, AWS SNS)
 * 4. May need to handle bounces and unsubscribes
 * 
 * Advanced Patterns Demonstrated:
 * - Message prefetching for better throughput
 * - Structured logging
 * - Statistics and monitoring
 * - Graceful degradation
 * 
 * Scalability:
 * This service can be scaled horizontally (multiple instances).
 * Each instance processes messages independently, coordinated by RabbitMQ.
 */

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Models;
using System.Text;

Console.WriteLine("🚀 Starting Email Consumer Service...");

// Read configuration from environment variables
// This allows easy configuration in Docker, Kubernetes, etc.
var hostname = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost";
var port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672");
var username = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest";
var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";
var queueName = Environment.GetEnvironmentVariable("EMAIL_QUEUE") ?? "email_queue";

Console.WriteLine($"🔌 Connecting to RabbitMQ at {hostname}:{port}");

// Create connection factory
var factory = RabbitMQHelper.CreateConnectionFactory(hostname, port, username, password);

// Establish connection
var connection = await factory.CreateConnectionAsync();
Console.WriteLine("✅ Connected to RabbitMQ");

// Create channel
var channel = await connection.CreateChannelAsync();
Console.WriteLine("📡 Channel created");

// Declare queue (ensure it exists with correct settings)
RabbitMQHelper.DeclareQueue(channel, queueName, durable: true);

/**
 * Set QoS (Quality of Service)
 * 
 * For emails, we might want slightly higher prefetch than SMS
 * because email processing can be more I/O bound (loading templates, etc.)
 * 
 * However, for this demo we'll keep it at 1 for simplicity
 */
await channel.BasicQosAsync(
    prefetchSize: 0,
    prefetchCount: 1,  // One message at a time
    global: false
);
Console.WriteLine("⚙️  QoS set: prefetchCount = 1");

// Statistics
int totalProcessed = 0;
int totalSuccess = 0;
int totalErrors = 0;

// Create consumer
var consumer = new AsyncEventingBasicConsumer(channel);

/**
 * Message handler
 * Processes each email message received from the queue
 */
consumer.ReceivedAsync += async (model, ea) =>
{
    totalProcessed++;
    var body = ea.Body.ToArray();
    
    try
    {
        // Deserialize message
        var emailMessage = RabbitMQHelper.DeserializeMessage<EmailMessage>(body);
        
        if (emailMessage == null)
        {
            throw new Exception("Failed to deserialize message");
        }
        
        Console.WriteLine($"\n📨 Received Email Message #{totalProcessed}");
        Console.WriteLine($"   Message ID: {emailMessage.Id}");
        Console.WriteLine($"   To: {emailMessage.To}");
        Console.WriteLine($"   Subject: {emailMessage.Subject}");
        Console.WriteLine($"   Body Preview: {(emailMessage.Body.Length > 50 ? emailMessage.Body.Substring(0, 50) + "..." : emailMessage.Body)}");
        Console.WriteLine($"   Campaign: {emailMessage.Campaign}");
        Console.WriteLine($"   Timestamp: {emailMessage.Timestamp}");
        
        // Check for redelivery
        if (ea.Redelivered)
        {
            Console.WriteLine($"   ⚠️  This is a redelivered message (retry attempt)");
        }
        
        // Process email
        await ProcessEmailMessage(emailMessage);
        
        /**
         * ACK the message
         * Only done after successful processing
         * This is the "at-least-once" delivery guarantee
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
        Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
        
        /**
         * Error Handling Strategy:
         * 
         * For emails, we might want different retry logic:
         * - Invalid email format: Don't retry (permanent error)
         * - Network timeout: Retry (transient error)
         * - Rate limit exceeded: Delay and retry
         * - Bounce/Unsubscribe: Don't retry, update database
         * 
         * Advanced Implementation:
         * - Check message header for retry count
         * - Implement exponential backoff
         * - Use Dead Letter Queue for max retries exceeded
         */
        
        // For demo: requeue all failures
        await channel.BasicNackAsync(
            deliveryTag: ea.DeliveryTag,
            multiple: false,
            requeue: true
        );
        
        Console.WriteLine($"🔄 Message requeued for retry");
    }
};

/**
 * Start consuming
 * The consumer will now receive messages asynchronously
 */
await channel.BasicConsumeAsync(
    queue: queueName,
    autoAck: false,  // Manual ACK for reliability
    consumer: consumer
);

Console.WriteLine($"👂 Listening for messages on queue: {queueName}");
Console.WriteLine($"⏳ Waiting for Email messages... (Press Ctrl+C to exit)");

// Periodic statistics reporting
var statsTimer = new Timer(_ =>
{
    Console.WriteLine($"\n📊 === Email Consumer Statistics ===");
    Console.WriteLine($"   Total Processed: {totalProcessed}");
    Console.WriteLine($"   Successful: {totalSuccess}");
    Console.WriteLine($"   Errors: {totalErrors}");
    var successRate = totalProcessed > 0 ? (totalSuccess * 100.0 / totalProcessed).ToString("F2") : "0";
    Console.WriteLine($"   Success Rate: {successRate}%");
    Console.WriteLine($"====================================\n");
}, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

// Keep service running
var exitEvent = new ManualResetEvent(false);

// Graceful shutdown handler
Console.CancelKeyPress += async (sender, e) =>
{
    e.Cancel = true;
    
    Console.WriteLine("\n🛑 Shutdown signal received...");
    Console.WriteLine($"📊 Final Statistics:");
    Console.WriteLine($"   Total Processed: {totalProcessed}");
    Console.WriteLine($"   Successful: {totalSuccess}");
    Console.WriteLine($"   Errors: {totalErrors}");
    
    // Cleanup resources
    statsTimer.Dispose();
    await channel.CloseAsync();
    await connection.CloseAsync();
    Console.WriteLine("✅ Disconnected from RabbitMQ");
    Console.WriteLine("👋 Email Consumer stopped gracefully");
    
    exitEvent.Set();
};

exitEvent.WaitOne();

/**
 * Process Email Message
 * 
 * In production, this would:
 * 1. Validate email address format
 * 2. Check against bounce/unsubscribe lists
 * 3. Render HTML templates (e.g., using Razor, Handlebars)
 * 4. Add tracking pixels for open rates
 * 5. Generate unsubscribe links
 * 6. Call email provider API (SendGrid, AWS SES, Mailgun, etc.)
 * 7. Handle provider-specific errors (rate limits, bounces)
 * 8. Store sent status in database
 * 9. Update campaign analytics
 * 10. Handle webhooks for delivery status
 */
async Task ProcessEmailMessage(EmailMessage email)
{
    // Validation
    if (string.IsNullOrWhiteSpace(email.To) || 
        string.IsNullOrWhiteSpace(email.Subject) || 
        string.IsNullOrWhiteSpace(email.Body))
    {
        throw new Exception("Invalid email: missing To, Subject, or Body");
    }
    
    // Basic email format validation
    if (!email.To.Contains("@"))
    {
        throw new Exception($"Invalid email address format: {email.To}");
    }
    
    Console.WriteLine($"📧 Sending email to {email.To}...");
    
    // Simulate email provider API call
    // In production: Call SendGrid, AWS SES, etc.
    await Task.Delay(800); // Emails typically take longer than SMS
    
    // Simulate 98% success rate
    var random = new Random();
    if (random.Next(100) < 98)
    {
        Console.WriteLine($"✉️  Email sent successfully!");
        Console.WriteLine($"   Provider: Email-Gateway-Simulator");
        Console.WriteLine($"   Status: Delivered to inbox");
        Console.WriteLine($"   Message-ID: msg-{Guid.NewGuid()}");
        
        // Production tasks:
        // - Store delivery receipt
        // - Update email sent count for campaign
        // - Add to sent emails table for tracking
        // - Schedule follow-up emails if part of drip campaign
    }
    else
    {
        // Simulate failures
        throw new Exception("Email gateway temporarily unavailable");
    }
}
