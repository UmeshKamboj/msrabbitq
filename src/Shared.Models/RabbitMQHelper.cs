using RabbitMQ.Client;
using System;
using System.Text;
using System.Text.Json;

namespace Shared.Models;

/// <summary>
/// RabbitMQ Connection Factory and Helper Methods
/// This class provides reusable functionality for connecting to RabbitMQ
/// and performing common operations.
/// 
/// Key Concepts:
/// - ConnectionFactory: Creates connections to RabbitMQ server
/// - IConnection: Represents a TCP connection to RabbitMQ
/// - IChannel: Lightweight virtual connection for AMQP operations
/// - Exchange: Routes messages to queues based on routing keys
/// - Queue: Stores messages until consumed
/// - Binding: Links exchange to queue with routing key
/// </summary>
public static class RabbitMQHelper
{
    /// <summary>
    /// Creates a RabbitMQ connection factory with configured settings
    /// </summary>
    /// <param name="hostname">RabbitMQ server hostname</param>
    /// <param name="port">RabbitMQ server port (default: 5672)</param>
    /// <param name="username">Username for authentication</param>
    /// <param name="password">Password for authentication</param>
    /// <returns>Configured ConnectionFactory</returns>
    public static ConnectionFactory CreateConnectionFactory(
        string hostname = "localhost",
        int port = 5672,
        string username = "guest",
        string password = "guest")
    {
        return new ConnectionFactory
        {
            HostName = hostname,
            Port = port,
            UserName = username,
            Password = password,
            
            // Automatic recovery settings
            // If connection is lost, client will automatically try to reconnect
            AutomaticRecoveryEnabled = true,
            
            // How long to wait between reconnection attempts
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            
            // Topology recovery: recreates exchanges, queues, and bindings after reconnection
            TopologyRecoveryEnabled = true,
            
            // Client-provided name for easier debugging
            ClientProvidedName = "Marketing-Service"
        };
    }
    
    /// <summary>
    /// Declares an exchange on RabbitMQ
    /// </summary>
    /// <param name="channel">RabbitMQ channel</param>
    /// <param name="exchangeName">Name of the exchange</param>
    /// <param name="exchangeType">Type: direct, topic, fanout, or headers</param>
    /// <param name="durable">If true, exchange survives broker restart</param>
    public static void DeclareExchange(
        IChannel channel,
        string exchangeName,
        string exchangeType = "direct",
        bool durable = true)
    {
        // Exchange Types:
        // - direct: Routes to queues with exact routing key match
        // - topic: Routes based on wildcard pattern matching (e.g., "order.*.created")
        // - fanout: Broadcasts to all bound queues (ignores routing key)
        // - headers: Routes based on message header attributes
        
        channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: exchangeType,
            durable: durable,      // Survives broker restart
            autoDelete: false,     // Not deleted when last queue is unbound
            arguments: null
        ).GetAwaiter().GetResult();
        
        Console.WriteLine($"🔀 Exchange '{exchangeName}' ({exchangeType}) declared");
    }
    
    /// <summary>
    /// Declares a queue on RabbitMQ
    /// </summary>
    /// <param name="channel">RabbitMQ channel</param>
    /// <param name="queueName">Name of the queue</param>
    /// <param name="durable">If true, queue survives broker restart</param>
    public static void DeclareQueue(
        IChannel channel,
        string queueName,
        bool durable = true)
    {
        // Queue Options:
        // - durable: Queue definition survives broker restart
        // - exclusive: Queue is deleted when connection closes
        // - autoDelete: Queue is deleted when last consumer unsubscribes
        
        channel.QueueDeclareAsync(
            queue: queueName,
            durable: durable,      // Queue survives broker restart
            exclusive: false,      // Not exclusive to one connection
            autoDelete: false,     // Not deleted when consumers disconnect
            arguments: null
        ).GetAwaiter().GetResult();
        
        Console.WriteLine($"📬 Queue '{queueName}' declared");
    }
    
    /// <summary>
    /// Binds a queue to an exchange with a routing key
    /// </summary>
    /// <param name="channel">RabbitMQ channel</param>
    /// <param name="queueName">Queue to bind</param>
    /// <param name="exchangeName">Exchange to bind to</param>
    /// <param name="routingKey">Routing key for message routing</param>
    public static void BindQueue(
        IChannel channel,
        string queueName,
        string exchangeName,
        string routingKey)
    {
        // Binding connects an exchange to a queue
        // Messages published to the exchange with matching routing key
        // will be routed to this queue
        
        channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey,
            arguments: null
        ).GetAwaiter().GetResult();
        
        Console.WriteLine($"🔗 Queue '{queueName}' bound to exchange '{exchangeName}' with routing key '{routingKey}'");
    }
    
    /// <summary>
    /// Publishes a message to an exchange
    /// </summary>
    /// <typeparam name="T">Type of message to publish</typeparam>
    /// <param name="channel">RabbitMQ channel</param>
    /// <param name="exchangeName">Exchange to publish to</param>
    /// <param name="routingKey">Routing key determining destination queue(s)</param>
    /// <param name="message">Message object to publish</param>
    public static void PublishMessage<T>(
        IChannel channel,
        string exchangeName,
        string routingKey,
        T message)
    {
        // Serialize message to JSON
        string jsonMessage = JsonSerializer.Serialize(message, new JsonSerializerOptions 
        { 
            WriteIndented = false // Compact JSON for efficiency
        });
        
        // Convert to bytes (RabbitMQ works with byte arrays)
        byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
        
        // Create message properties
        var properties = new BasicProperties
        {
            // Message persistence
            Persistent = true, // Message survives broker restart (stored on disk)
            
            // Content type helps consumers know how to deserialize
            ContentType = "application/json",
            
            // Message ID for tracking and deduplication
            MessageId = Guid.NewGuid().ToString(),
            
            // Timestamp when message was published
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            
            // Delivery mode: 2 = persistent
            DeliveryMode = DeliveryModes.Persistent
        };
        
        // Publish message to exchange
        channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false, // If true, message is returned if no queue matches
            basicProperties: properties,
            body: messageBytes
        ).GetAwaiter().GetResult();
        
        Console.WriteLine($"📤 Published message to exchange '{exchangeName}' with routing key '{routingKey}'");
    }
    
    /// <summary>
    /// Deserializes a message from RabbitMQ
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="body">Message body bytes</param>
    /// <returns>Deserialized message object</returns>
    public static T? DeserializeMessage<T>(byte[] body)
    {
        string jsonMessage = Encoding.UTF8.GetString(body);
        return JsonSerializer.Deserialize<T>(jsonMessage);
    }
}
