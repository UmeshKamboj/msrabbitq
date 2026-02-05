/**
 * Producer API Service (Marketing API Gateway)
 * 
 * This is the main entry point for the marketing microservices system.
 * It provides RESTful HTTP endpoints that receive marketing requests
 * and publishes them as messages to RabbitMQ queues for asynchronous processing.
 * 
 * Architecture Pattern: API Gateway + Message Queue (Producer)
 * 
 * Benefits:
 * 1. Decoupling: API doesn't depend on consumer implementation
 * 2. Scalability: Can handle high request volumes without blocking
 * 3. Reliability: Messages persist even if consumers are down
 * 4. Asynchronous: Immediate response without waiting for processing
 * 5. Load Leveling: Queue buffers traffic spikes
 * 
 * Message Flow:
 * Client -> HTTP Request -> Producer API -> RabbitMQ -> Consumer Services
 */

using RabbitMQ.Client;
using Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register RabbitMQ connection as a singleton
// Singleton ensures one connection is shared across all requests (efficient)
builder.Services.AddSingleton<IConnection>(sp =>
{
    // Read RabbitMQ configuration from environment variables or appsettings
    var config = builder.Configuration;
    var hostname = config["RabbitMQ:Hostname"] ?? "localhost";
    var port = int.Parse(config["RabbitMQ:Port"] ?? "5672");
    var username = config["RabbitMQ:Username"] ?? "guest";
    var password = config["RabbitMQ:Password"] ?? "guest";
    
    var factory = RabbitMQHelper.CreateConnectionFactory(hostname, port, username, password);
    
    Console.WriteLine($"🔌 Connecting to RabbitMQ at {hostname}:{port}");
    var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
    Console.WriteLine("✅ Connected to RabbitMQ successfully");
    
    return connection;
});

// Register RabbitMQ channel as scoped (one per request)
// Scoped lifetime is appropriate for channels as they are lightweight
builder.Services.AddScoped<IChannel>(sp =>
{
    var connection = sp.GetRequiredService<IConnection>();
    return connection.CreateChannelAsync().GetAwaiter().GetResult();
});

var app = builder.Build();

// Initialize RabbitMQ infrastructure on startup
// This ensures exchanges, queues, and bindings are created before handling requests
using (var scope = app.Services.CreateScope())
{
    var connection = scope.ServiceProvider.GetRequiredService<IConnection>();
    var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
    
    var config = app.Configuration;
    var exchangeName = config["RabbitMQ:ExchangeName"] ?? "marketing_exchange";
    var smsQueue = config["RabbitMQ:SmsQueue"] ?? "sms_queue";
    var emailQueue = config["RabbitMQ:EmailQueue"] ?? "email_queue";
    
    Console.WriteLine("🚀 Initializing RabbitMQ infrastructure...");
    
    // Declare exchange (central routing hub)
    RabbitMQHelper.DeclareExchange(channel, exchangeName, "direct", durable: true);
    
    // Declare queues (message storage)
    RabbitMQHelper.DeclareQueue(channel, smsQueue, durable: true);
    RabbitMQHelper.DeclareQueue(channel, emailQueue, durable: true);
    
    // Bind queues to exchange with routing keys
    // Messages with routing key "sms" go to sms_queue
    RabbitMQHelper.BindQueue(channel, smsQueue, exchangeName, "sms");
    // Messages with routing key "email" go to email_queue
    RabbitMQHelper.BindQueue(channel, emailQueue, exchangeName, "email");
    
    await channel.CloseAsync();
    Console.WriteLine("✅ RabbitMQ infrastructure initialized");
}

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

/**
 * Health Check Endpoint
 * GET /health
 * Returns service status and RabbitMQ connection status
 */
app.MapGet("/health", (IConnection connection) =>
{
    return Results.Ok(new
    {
        Status = "UP",
        Service = "Producer API",
        Timestamp = DateTime.UtcNow,
        RabbitMQ = connection.IsOpen ? "CONNECTED" : "DISCONNECTED"
    });
});

/**
 * Send SMS Endpoint
 * POST /api/sms
 * 
 * Accepts SMS message request and publishes to RabbitMQ
 * 
 * Request Body Example:
 * {
 *   "phoneNumber": "+1234567890",
 *   "message": "Your promotional code: SAVE20",
 *   "campaign": "summer_sale"
 * }
 */
app.MapPost("/api/sms", (SmsMessage smsMessage, IChannel channel, IConfiguration config) =>
{
    try
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(smsMessage.PhoneNumber) || 
            string.IsNullOrWhiteSpace(smsMessage.Message))
        {
            return Results.BadRequest(new { Error = "PhoneNumber and Message are required" });
        }
        
        var exchangeName = config["RabbitMQ:ExchangeName"] ?? "marketing_exchange";
        
        // Publish message to RabbitMQ with routing key "sms"
        RabbitMQHelper.PublishMessage(channel, exchangeName, "sms", smsMessage);
        
        return Results.Accepted("/api/sms", new
        {
            Status = "Accepted",
            Message = "SMS message queued for delivery",
            MessageId = smsMessage.Id,
            Timestamp = smsMessage.Timestamp
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error publishing SMS: {ex.Message}");
        return Results.Problem("Failed to queue SMS message");
    }
});

/**
 * Send Email Endpoint
 * POST /api/email
 * 
 * Accepts email message request and publishes to RabbitMQ
 * 
 * Request Body Example:
 * {
 *   "to": "customer@example.com",
 *   "subject": "Special Offer Inside!",
 *   "body": "Dear customer, we have a special offer for you...",
 *   "campaign": "newsletter"
 * }
 */
app.MapPost("/api/email", (EmailMessage emailMessage, IChannel channel, IConfiguration config) =>
{
    try
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(emailMessage.To) || 
            string.IsNullOrWhiteSpace(emailMessage.Subject) || 
            string.IsNullOrWhiteSpace(emailMessage.Body))
        {
            return Results.BadRequest(new { Error = "To, Subject, and Body are required" });
        }
        
        var exchangeName = config["RabbitMQ:ExchangeName"] ?? "marketing_exchange";
        
        // Publish message to RabbitMQ with routing key "email"
        RabbitMQHelper.PublishMessage(channel, exchangeName, "email", emailMessage);
        
        return Results.Accepted("/api/email", new
        {
            Status = "Accepted",
            Message = "Email message queued for delivery",
            MessageId = emailMessage.Id,
            Timestamp = emailMessage.Timestamp
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error publishing email: {ex.Message}");
        return Results.Problem("Failed to queue email message");
    }
});

/**
 * Bulk Campaign Endpoint
 * POST /api/campaign
 * 
 * Sends both SMS and Email to multiple recipients
 * 
 * Request Body Example:
 * {
 *   "campaign": "black_friday",
 *   "sms": {
 *     "message": "Black Friday Sale! 50% off everything!",
 *     "recipients": ["+1234567890", "+0987654321"]
 *   },
 *   "email": {
 *     "subject": "Black Friday - 50% OFF!",
 *     "body": "Dear valued customer...",
 *     "recipients": ["customer1@example.com", "customer2@example.com"]
 *   }
 * }
 */
app.MapPost("/api/campaign", (CampaignRequest campaign, IChannel channel, IConfiguration config) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(campaign.Campaign))
        {
            return Results.BadRequest(new { Error = "Campaign name is required" });
        }
        
        var exchangeName = config["RabbitMQ:ExchangeName"] ?? "marketing_exchange";
        int smsQueued = 0, emailQueued = 0;
        var smsIds = new List<string>();
        var emailIds = new List<string>();
        
        // Process SMS messages
        if (campaign.Sms != null && campaign.Sms.Recipients.Length > 0)
        {
            foreach (var phoneNumber in campaign.Sms.Recipients)
            {
                var smsMessage = new SmsMessage
                {
                    PhoneNumber = phoneNumber,
                    Message = campaign.Sms.Message,
                    Campaign = campaign.Campaign
                };
                
                RabbitMQHelper.PublishMessage(channel, exchangeName, "sms", smsMessage);
                smsQueued++;
                smsIds.Add(smsMessage.Id);
            }
        }
        
        // Process Email messages
        if (campaign.Email != null && campaign.Email.Recipients.Length > 0)
        {
            foreach (var recipient in campaign.Email.Recipients)
            {
                var emailMessage = new EmailMessage
                {
                    To = recipient,
                    Subject = campaign.Email.Subject,
                    Body = campaign.Email.Body,
                    Campaign = campaign.Campaign
                };
                
                RabbitMQHelper.PublishMessage(channel, exchangeName, "email", emailMessage);
                emailQueued++;
                emailIds.Add(emailMessage.Id);
            }
        }
        
        return Results.Accepted("/api/campaign", new
        {
            Status = "Accepted",
            Message = "Campaign messages queued for delivery",
            Campaign = campaign.Campaign,
            Results = new
            {
                Sms = new { Queued = smsQueued, MessageIds = smsIds },
                Email = new { Queued = emailQueued, MessageIds = emailIds }
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error processing campaign: {ex.Message}");
        return Results.Problem("Failed to queue campaign messages");
    }
});

/**
 * Queue Statistics Endpoint
 * GET /api/stats
 * 
 * Returns current queue depths and consumer counts
 */
app.MapGet("/api/stats", async (IChannel channel, IConfiguration config) =>
{
    try
    {
        var smsQueue = config["RabbitMQ:SmsQueue"] ?? "sms_queue";
        var emailQueue = config["RabbitMQ:EmailQueue"] ?? "email_queue";
        
        // Passive declare returns queue info without modifying it
        var smsQueueInfo = await channel.QueueDeclarePassiveAsync(smsQueue);
        var emailQueueInfo = await channel.QueueDeclarePassiveAsync(emailQueue);
        
        return Results.Ok(new
        {
            Timestamp = DateTime.UtcNow,
            Queues = new
            {
                Sms = new
                {
                    Name = smsQueue,
                    MessageCount = smsQueueInfo.MessageCount,
                    ConsumerCount = smsQueueInfo.ConsumerCount
                },
                Email = new
                {
                    Name = emailQueue,
                    MessageCount = emailQueueInfo.MessageCount,
                    ConsumerCount = emailQueueInfo.ConsumerCount
                }
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error getting stats: {ex.Message}");
        return Results.Problem("Failed to retrieve queue statistics");
    }
});

Console.WriteLine("🚀 Producer API is starting...");
Console.WriteLine($"📝 Available endpoints:");
Console.WriteLine($"   - POST /api/sms - Send SMS message");
Console.WriteLine($"   - POST /api/email - Send email message");
Console.WriteLine($"   - POST /api/campaign - Send bulk campaign");
Console.WriteLine($"   - GET /api/stats - Get queue statistics");
Console.WriteLine($"   - GET /health - Health check");

app.Run();
