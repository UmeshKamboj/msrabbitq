using System;

namespace Shared.Models;

/// <summary>
/// Base message model containing common properties for all message types.
/// This demonstrates inheritance and polymorphism in message-based systems.
/// </summary>
public class BaseMessage
{
    /// <summary>
    /// Unique identifier for the message (used for tracking and deduplication)
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Campaign identifier for grouping related messages
    /// </summary>
    public string Campaign { get; set; } = "default";
    
    /// <summary>
    /// Message type (SMS, EMAIL, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// SMS Message Model
/// Represents a text message to be sent via SMS
/// </summary>
public class SmsMessage : BaseMessage
{
    /// <summary>
    /// Recipient phone number (E.164 format recommended: +1234567890)
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// SMS message content (typically limited to 160 characters for single SMS)
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    public SmsMessage()
    {
        Type = "SMS";
    }
}

/// <summary>
/// Email Message Model
/// Represents an email to be sent
/// </summary>
public class EmailMessage : BaseMessage
{
    /// <summary>
    /// Recipient email address
    /// </summary>
    public string To { get; set; } = string.Empty;
    
    /// <summary>
    /// Email subject line
    /// </summary>
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Email body content (can be plain text or HTML)
    /// </summary>
    public string Body { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional: CC recipients
    /// </summary>
    public string[]? Cc { get; set; }
    
    /// <summary>
    /// Optional: BCC recipients
    /// </summary>
    public string[]? Bcc { get; set; }
    
    public EmailMessage()
    {
        Type = "EMAIL";
    }
}

/// <summary>
/// Bulk Campaign Request Model
/// Used for sending messages to multiple recipients
/// </summary>
public class CampaignRequest
{
    public string Campaign { get; set; } = string.Empty;
    public SmsRequest? Sms { get; set; }
    public EmailRequest? Email { get; set; }
}

/// <summary>
/// SMS Request for campaigns
/// </summary>
public class SmsRequest
{
    public string Message { get; set; } = string.Empty;
    public string[] Recipients { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Email Request for campaigns
/// </summary>
public class EmailRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string[] Recipients { get; set; } = Array.Empty<string>();
}
