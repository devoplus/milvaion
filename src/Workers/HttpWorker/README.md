# HTTP Worker

Built-in worker for making HTTP/HTTPS requests from Milvaion scheduler.

## Job: RequestSenderJob

Sends HTTP requests with full customization (headers, body, authentication, etc.).

**Job Data:**
```json
{
  "Url": "https://api.example.com/webhook",
  "Method": "POST",
  "Headers": {
    "Authorization": "Bearer your-token",
    "Content-Type": "application/json"
  },
  "Body": "{\"event\":\"user_created\",\"userId\":123}",
  "TimeoutSeconds": 30
}
```

**Supported Methods:**
- GET
- POST
- PUT
- PATCH
- DELETE

## Running

### Docker
```bash
docker run -d --name http-worker \
  --network milvaion_milvaion-network \
  milvaion-http-worker
```

### Development
```bash
cd src/Workers/HttpWorker
dotnet run
```

## Features

- All HTTP methods (GET, POST, PUT, PATCH, DELETE)
- Custom headers
- Request body (JSON, form-data, etc.)
- Configurable timeout
- Response logging
- Error handling with retries

## Use Cases

- Webhook notifications
- API integrations
- Data synchronization
- Third-party service calls
- Microservice communication
