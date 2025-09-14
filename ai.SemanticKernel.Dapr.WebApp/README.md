# Semantic Kernel Dapr Web Application

## Run it

Move to the project directory before running the commands below.

```
# One-time (installs dapr locally with default components)
dapr init

# Run the app under Dapr
dapr run --app-id sk-app --app-port 5190 --dapr-http-port 3500 --resources-path ./components -- dotnet run

# Open dapr dashboard (optional)
dapr dashboard
```

## Test it with PowerShell

```
# Health
Invoke-RestMethod -Uri http://localhost:5190/

# Send a chat message (POST /chat?userId=...&message=...)
Invoke-RestMethod -Uri "http://localhost:5190/chat?userId=user1&message=where is borinquen?" -Method Post

# Get conversation history (GET /history?userId=...)
Invoke-RestMethod -Uri "http://localhost:5190/history?userId=user1"

# Clear conversation history (POST /clear?userId=...)
Invoke-RestMethod -Uri "http://localhost:5190/clear?userId=user1" -Method Post
```

### Troubleshooting Dapr

```
# See if the sidecar is up
Invoke-RestMethod http://localhost:3500/v1.0/metadata

# List components Dapr actually loaded (check "name" fields)
Invoke-RestMethod http://localhost:3500/v1.0/components

# Smoke-test the state API against the name your code uses (change statestore if needed)
$store="sqlstatestore"   # or "statestore" to match your component
$key="diag-key"; $val=@{ ok = $true } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:3500/v1.0/state/$store" -Body ("[{""key"":""$key"",""value"":$val}]") -ContentType "application/json"
Invoke-RestMethod "http://localhost:3500/v1.0/state/$store/$key"
```

