# API Testing Examples

This directory contains example requests for testing the RabbitMQ Marketing Microservices API.

## Prerequisites

- The application must be running (via Docker Compose or locally)
- `curl` and `jq` installed (for shell scripts)
- Or use any HTTP client (Postman, Insomnia, etc.)

## Quick Start

### Using the Test Script

```bash
chmod +x examples/test-api.sh
./examples/test-api.sh
```

## Manual Testing Examples

### 1. Health Check

```bash
curl http://localhost:5000/health
```

### 2. Send SMS

```bash
curl -X POST http://localhost:5000/api/sms \
  -H "Content-Type: application/json" \
  -d '{
    "phoneNumber": "+1234567890",
    "message": "Your verification code is: 123456",
    "campaign": "user_verification"
  }'
```

### 3. Send Email

```bash
curl -X POST http://localhost:5000/api/email \
  -H "Content-Type: application/json" \
  -d '{
    "to": "user@example.com",
    "subject": "Welcome to Our Platform",
    "body": "Thank you for signing up! We are excited to have you.",
    "campaign": "onboarding"
  }'
```

### 4. Bulk Campaign

```bash
curl -X POST http://localhost:5000/api/campaign \
  -H "Content-Type: application/json" \
  -d '{
    "campaign": "black_friday",
    "sms": {
      "message": "Black Friday: 50% OFF! Shop now!",
      "recipients": ["+1111111111", "+2222222222"]
    },
    "email": {
      "subject": "Black Friday Sale - 50% OFF",
      "body": "Don'\''t miss our biggest sale of the year!",
      "recipients": ["user1@example.com", "user2@example.com"]
    }
  }'
```

### 5. Queue Statistics

```bash
curl http://localhost:5000/api/stats
```

## Testing with PowerShell (Windows)

```powershell
# Send SMS
$body = @{
    phoneNumber = "+1234567890"
    message = "Test SMS from PowerShell"
    campaign = "test"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/sms" `
  -Method Post `
  -Body $body `
  -ContentType "application/json"
```

## Testing with C#

```csharp
using var client = new HttpClient();

var smsRequest = new
{
    phoneNumber = "+1234567890",
    message = "Test SMS from C#",
    campaign = "test"
};

var content = new StringContent(
    JsonSerializer.Serialize(smsRequest),
    Encoding.UTF8,
    "application/json"
);

var response = await client.PostAsync(
    "http://localhost:5000/api/sms",
    content
);

var result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);
```

## Load Testing

### Using Apache Bench

```bash
# Test SMS endpoint (100 requests, 10 concurrent)
ab -n 100 -c 10 -p sms.json -T application/json \
  http://localhost:5000/api/sms
```

Create `sms.json`:
```json
{
  "phoneNumber": "+1234567890",
  "message": "Load test message",
  "campaign": "load_test"
}
```

### Using hey

```bash
# Install hey
go install github.com/rakyll/hey@latest

# Run load test
hey -n 1000 -c 50 -m POST \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"+1234567890","message":"Load test","campaign":"test"}' \
  http://localhost:5000/api/sms
```

## Monitoring

### Watch Queue Stats

```bash
# Update stats every 2 seconds
watch -n 2 'curl -s http://localhost:5000/api/stats | jq .'
```

### Monitor RabbitMQ Management UI

Open http://localhost:15672 in your browser:
- Username: `guest`
- Password: `guest`

Navigate to:
- **Queues**: See message counts and rates
- **Connections**: See active connections
- **Channels**: See active channels

## Troubleshooting

### No Response from API

Check if services are running:
```bash
docker-compose ps
```

### Messages Not Being Consumed

Check consumer logs:
```bash
docker-compose logs -f sms-consumer
docker-compose logs -f email-consumer
```

### Connection Refused

Ensure RabbitMQ is running:
```bash
docker-compose logs rabbitmq
```

## Next Steps

1. Experiment with different message payloads
2. Try stopping a consumer and watch messages queue up
3. Scale consumers and observe load distribution
4. Monitor RabbitMQ Management UI during load tests
5. Implement your own message types and consumers
