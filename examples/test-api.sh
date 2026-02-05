#!/bin/bash

# Test Script for RabbitMQ Marketing Microservices
# This script demonstrates all API endpoints with example requests

BASE_URL="http://localhost:5000"

echo "========================================="
echo "RabbitMQ Marketing Microservices - Test Script"
echo "========================================="
echo ""

# Color codes
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Test 1: Health Check
echo -e "${BLUE}Test 1: Health Check${NC}"
curl -X GET "${BASE_URL}/health" | jq .
echo -e "\n"

# Test 2: Send Single SMS
echo -e "${BLUE}Test 2: Send Single SMS${NC}"
curl -X POST "${BASE_URL}/api/sms" \
  -H "Content-Type: application/json" \
  -d '{
    "phoneNumber": "+1234567890",
    "message": "Hello! This is a test SMS from RabbitMQ microservices",
    "campaign": "test_campaign"
  }' | jq .
echo -e "\n"

# Test 3: Send Single Email
echo -e "${BLUE}Test 3: Send Single Email${NC}"
curl -X POST "${BASE_URL}/api/email" \
  -H "Content-Type: application/json" \
  -d '{
    "to": "customer@example.com",
    "subject": "Welcome to our service!",
    "body": "Thank you for joining us. We are excited to have you on board!",
    "campaign": "welcome_series"
  }' | jq .
echo -e "\n"

# Test 4: Send Bulk Campaign
echo -e "${BLUE}Test 4: Send Bulk Campaign${NC}"
curl -X POST "${BASE_URL}/api/campaign" \
  -H "Content-Type: application/json" \
  -d '{
    "campaign": "summer_sale_2024",
    "sms": {
      "message": "Summer Sale! 30% off everything! Use code: SUMMER30",
      "recipients": ["+1234567890", "+0987654321", "+1122334455"]
    },
    "email": {
      "subject": "Summer Sale - 30% OFF Everything!",
      "body": "Dear valued customer, enjoy our biggest summer sale with 30% off all products. Use code SUMMER30 at checkout. Hurry, sale ends soon!",
      "recipients": [
        "customer1@example.com",
        "customer2@example.com",
        "customer3@example.com"
      ]
    }
  }' | jq .
echo -e "\n"

# Wait a moment for messages to be processed
echo -e "${GREEN}Waiting 3 seconds for messages to be processed...${NC}"
sleep 3

# Test 5: Get Queue Statistics
echo -e "${BLUE}Test 5: Get Queue Statistics${NC}"
curl -X GET "${BASE_URL}/api/stats" | jq .
echo -e "\n"

# Test 6: Send Multiple SMS Messages
echo -e "${BLUE}Test 6: Send Multiple SMS Messages (Load Test)${NC}"
for i in {1..5}
do
  curl -X POST "${BASE_URL}/api/sms" \
    -H "Content-Type: application/json" \
    -d "{
      \"phoneNumber\": \"+123456789${i}\",
      \"message\": \"Test message ${i} from load test\",
      \"campaign\": \"load_test\"
    }" > /dev/null 2>&1
  echo -e "${GREEN}Sent SMS ${i}/5${NC}"
done
echo -e "\n"

# Test 7: Send Multiple Email Messages
echo -e "${BLUE}Test 7: Send Multiple Email Messages (Load Test)${NC}"
for i in {1..5}
do
  curl -X POST "${BASE_URL}/api/email" \
    -H "Content-Type: application/json" \
    -d "{
      \"to\": \"test${i}@example.com\",
      \"subject\": \"Test Email ${i}\",
      \"body\": \"This is test email number ${i} from the load test\",
      \"campaign\": \"load_test\"
    }" > /dev/null 2>&1
  echo -e "${GREEN}Sent Email ${i}/5${NC}"
done
echo -e "\n"

# Wait and check final stats
echo -e "${GREEN}Waiting 5 seconds for all messages to be processed...${NC}"
sleep 5

echo -e "${BLUE}Final Queue Statistics:${NC}"
curl -X GET "${BASE_URL}/api/stats" | jq .
echo -e "\n"

echo -e "${GREEN}========================================="
echo "All tests completed!"
echo "=========================================${NC}"
echo ""
echo "Next steps:"
echo "1. Check the consumer logs to see message processing"
echo "2. Visit RabbitMQ Management UI: http://localhost:15672 (guest/guest)"
echo "3. Experiment with stopping/starting consumers to see message queuing"
