# Email Worker

Built-in worker for sending emails from Milvaion scheduler.

## Job: SendEmailJob

Sends emails via SMTP with support for attachments and HTML content.

**Configuration:**
```json
"EmailWorkerOptions": {
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "EnableSsl": true,
    "FromEmail": "noreply@yourapp.com",
    "FromName": "Your App"
  }
}
```

**Job Data:**
```json
{
  "To": ["user@example.com"],
  "Cc": ["manager@example.com"],
  "Bcc": ["admin@example.com"],
  "Subject": "Test Email",
  "Body": "<h1>Hello</h1><p>This is a test</p>",
  "IsHtml": true,
  "Attachments": [
    {
      "FileName": "report.pdf",
      "ContentBase64": "JVBERi0xLjQK...",
      "ContentType": "application/pdf"
    }
  ]
}
```

## Running

### Docker
```bash
docker run -d --name email-worker \
  --network milvaion_milvaion-network \
  -e EmailWorkerOptions__Smtp__Host=smtp.gmail.com \
  -e EmailWorkerOptions__Smtp__Username=your-email@gmail.com \
  -e EmailWorkerOptions__Smtp__Password=your-password \
  milvaion-email-worker
```

### Development
```bash
cd src/Workers/EmailWorker
dotnet run
```

## Features

- Multiple recipients (To, Cc, Bcc)
- HTML and plain text emails
- File attachments (Base64 encoded)
- Custom SMTP configuration
- SSL/TLS support
