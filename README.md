# Building Production AI Agents with Microsoft Agent Framework + .NET

A proof of concept showing how an AI agent decides **which capability a request needs**,
invokes real .NET code to get it, and answers from the result — while the application, not
the model, stays in charge of the business rules.

The scenario is a customer support agent for an online store. The customer never names a
capability; they just talk.

```
"Where is my order ORD-1001?"               → the agent looks the order up
"Can I cancel it?"                           → the agent checks eligibility
"Go ahead and cancel it."                    → the agent cancels it, if the rules allow
"Check ORD-1002 and cancel it if possible."  → the agent checks, declines, and explains why
```

Companion article: [Building Production AI Agents with Microsoft Agent Framework + .NET](https://medium.com/@jenish.raiyani/f2c63de659de)

---

## What it demonstrates

- The agent selects tools from intent alone — the request never names one
- Tools are ordinary .NET methods calling ordinary application services
- Multi-step runs: check eligibility, then act on the result
- Business rules the agent cannot bypass, because the service re-evaluates them
- Authorization enforced by the application, never by the model
- Conversation memory across separate HTTP requests
- A swappable model provider behind `IChatClient`
- Agent behaviour tested without a live model

---

## Prerequisites

- **.NET SDK 10.0** or later
- A chat model that **supports tool calling** — without it the agent can talk but not act

---

## Packages

Versions are pinned in [`Directory.Packages.props`](Directory.Packages.props).

| Package | Version | Why |
|---|---|---|
| `Microsoft.Agents.AI` | 1.22.0 | `AIAgent`, `AgentSession`, `ChatClientAgent` |
| `Microsoft.Extensions.AI` | 10.10.0 | `IChatClient`, `AIFunctionFactory` |
| `Microsoft.Extensions.AI.OpenAI` | 10.10.0 | `.AsIChatClient()` adapter |
| `Azure.AI.OpenAI` | 2.9.0-beta.1 | Azure OpenAI client |
| `Azure.Identity` | 1.21.0 | keyless auth via `DefaultAzureCredential` |

`Azure.AI.OpenAI` is intentionally the beta — the stable 2.1.0 line pins an older `OpenAI`
dependency that conflicts with `Microsoft.Extensions.AI.OpenAI`.

---

## Configuring a model

No secrets are committed. `appsettings.json` ships with empty values; real ones come from
environment variables or user-secrets. See [`.env.example`](.env.example) for every key.

### Azure OpenAI (recommended)

Leave the API key unset to authenticate with Microsoft Entra ID — there is no secret to
store or rotate.

```bash
az login   # then grant yourself "Cognitive Services OpenAI User"

export AGENT__PROVIDER=AzureOpenAI
export AGENT__AZUREOPENAI__ENDPOINT=https://<your-resource>.openai.azure.com/
export AGENT__AZUREOPENAI__DEPLOYMENTNAME=<your-deployment-name>
```

`DEPLOYMENTNAME` is the **deployment** name from Azure AI Foundry, not the model name.

### Free alternatives

`Agent:OpenAI:Endpoint` points the same client at any OpenAI-compatible service.

**Groq** — free tier, no install:

```bash
export AGENT__PROVIDER=OpenAI
export AGENT__OPENAI__ENDPOINT=https://api.groq.com/openai/v1
export AGENT__OPENAI__MODELID=openai/gpt-oss-120b
export AGENT__OPENAI__APIKEY=<your Groq key>
```

Model availability varies by account — check yours with
`curl https://api.groq.com/openai/v1/models -H "Authorization: Bearer $GROQ_API_KEY"`.

**Ollama** — free and fully local:

```bash
ollama pull qwen3

export AGENT__PROVIDER=OpenAI
export AGENT__OPENAI__ENDPOINT=http://localhost:11434/v1
export AGENT__OPENAI__MODELID=qwen3
export AGENT__OPENAI__APIKEY=ollama
```

Smaller local models select tools less reliably. The business rules hold either way, but
the conversation is less impressive.

Missing configuration fails at **startup**, naming the variable:

```
OptionsValidationException: Agent:AzureOpenAI:Endpoint is required.
Set AGENT__AZUREOPENAI__ENDPOINT.
```

---

## Running it

```bash
dotnet build
dotnet test
dotnet run --project src/AgentPoc.Api
```

Swagger UI: <http://localhost:5049/swagger>

From Visual Studio or Rider, pick a provider from the launch profile dropdown:
**Azure OpenAI**, **Groq (free tier)** or **Ollama (local)**. Supply the key with
user-secrets, which lives outside the repository:

```bash
cd src/AgentPoc.Api
dotnet user-secrets set "Agent:OpenAI:ApiKey" "<your key>"
```

### Sample data

| Order | Customer | Status | Amount | Cancellable | Refund |
|---|---|---|---|---|---|
| ORD-1001 | CUST-001 | Processing | 149.99 | **yes** | NotApplicable |
| ORD-1002 | CUST-001 | Shipped | 89.50 | no | NotApplicable |
| ORD-1003 | CUST-002 | Delivered | 245.00 | no | NotApplicable |
| ORD-1004 | CUST-001 | Pending | 32.75 | **yes** | NotApplicable |
| ORD-1005 | CUST-002 | Cancelled | 410.20 | no | **Pending** |
| ORD-1006 | CUST-001 | Processing | 59.00 | no | NotApplicable |

ORD-1006 is the interesting one: still `Processing`, so a status-only rule would allow
cancelling it, yet the merchant flag says no. The rule is two-part.

The signed-in customer defaults to **CUST-001**. Data is in-memory and resets on restart.

---

## Example requests

Omit `conversationId` on the first turn — the API issues one and returns it.

```bash
curl -X POST http://localhost:5049/api/agent/chat \
  -H "Content-Type: application/json" \
  -d '{"message":"Check ORD-1001 and cancel it if possible."}'
```

```json
{
  "response": "Your order ORD-1001 has been cancelled successfully. A refund has been initiated and is now pending.",
  "conversationId": "2219ae6c38084cee93140b4c207bfeca",
  "toolCalls": [
    { "tool": "CheckCancellationEligibility", "status": "Success" },
    { "tool": "CancelOrder", "status": "Success" }
  ],
  "elapsedMilliseconds": 1749
}
```

Two tools, in sequence, from one sentence. Pass the `conversationId` back to continue:

```bash
curl -X POST http://localhost:5049/api/agent/chat \
  -H "Content-Type: application/json" \
  -d '{"message":"Can I cancel it?","conversationId":"2219ae6c38084cee93140b4c207bfeca"}'
```

ORD-1003 belongs to another customer. In Development you can switch identity with a header
to see the refusal:

```bash
curl -X POST http://localhost:5049/api/agent/chat \
  -H "Content-Type: application/json" \
  -H "X-Customer-Id: CUST-001" \
  -d '{"message":"What is the status of ORD-1003?"}'
```

The tool runs, the service returns "not found", and the agent says so. It never learns the
order exists.

---

## Testing

```bash
dotnet test
```

**68 tests, none of which call a live model.** `ScriptedChatClient` replays a fixed script in
place of `IChatClient`, which keeps tool schemas, tool execution, business rules, session
persistence and the API response shape under test.

It also does something a real model cannot be relied on to do:
`The_model_cannot_cancel_an_ineligible_order_by_calling_the_tool_directly` scripts a
`CancelOrder` on a shipped order with the eligibility check skipped, and asserts the order
is untouched.

---

## Known limitations

This is a POC. Production-oriented is not production-ready.

- **No real authentication.** Identity is resolved from the request but falls back to a
  configured value. Everything downstream already enforces authorization against it, so
  this is the only place that changes.
- **Data and sessions are in-process.** Both reset on restart and neither survives a second
  instance. Both sit behind interfaces, so a database and Redis are DI changes.
- **No cost controls** beyond a message-length cap and a run timeout. No rate limiting, no
  token budget, no ceiling on tool calls per run.
- **Destructive operations are protected by business rules alone.** The framework's
  tool-approval mechanism is the next step where rules are not enough.
- **Non-streaming only.** `RunStreamingAsync` exists if you want token-by-token output.
- **Tool selection depends on the model.** Rules hold regardless; conversation quality does not.

---

## API notes for readers of older tutorials

The Agent Framework API changed before 1.22, and much published material still shows the
older shape. If a sample does not compile, this is probably why:

| Older API | Current API |
|---|---|
| `AgentThread` | `AgentSession` |
| `agent.GetNewThread()` | `await agent.CreateSessionAsync(ct)` |
| `AgentRunResponse` | `AgentResponse` |

`AgentSessionStore` is also marked experimental (`MAAI001`) and fails a build without an
explicit suppression. This POC uses the stable `CreateSessionAsync` /
`SerializeSessionAsync` pair instead.

---

## Further reading

- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/)
- [microsoft/agent-framework on GitHub](https://github.com/microsoft/agent-framework)
