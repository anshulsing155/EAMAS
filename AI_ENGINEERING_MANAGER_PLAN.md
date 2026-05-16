# AI Engineering Manager + Scrum Master + QA Automation System
## Complete Architecture & Implementation Plan for EAMAS

**Project:** EAMAS — Employee Activity Monitoring & Analytics System  
**Prepared:** 2026-05-16  
**Version:** 1.0  
**Current App Version:** 1.2.1 (C# / .NET 8 / WPF / MongoDB)

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current System Analysis](#2-current-system-analysis)
3. [What Is Being Built](#3-what-is-being-built)
4. [High-Level Architecture](#4-high-level-architecture)
5. [New Database Models](#5-new-database-models)
6. [AI Provider Abstraction Layer](#6-ai-provider-abstraction-layer)
7. [New Services (EAMAS.Core)](#7-new-services-eamascore)
8. [New UI Modules (EAMAS.Desktop)](#8-new-ui-modules-eamasdesktop)
9. [New EAMAS.Server Component](#9-new-eamasserver-component)
10. [GitHub Integration Strategy](#10-github-integration-strategy)
11. [RAG Knowledge Engine](#11-rag-knowledge-engine)
12. [Implementation Phases](#12-implementation-phases)
13. [Complete File Structure](#13-complete-file-structure)
14. [New NuGet Dependencies](#14-new-nuget-dependencies)
15. [MongoDB Schema Additions](#15-mongodb-schema-additions)
16. [Security Considerations](#16-security-considerations)
17. [Challenges & Mitigations](#17-challenges--mitigations)
18. [Recommended Timeline](#18-recommended-timeline)

---

## 1. Executive Summary

The goal is to embed a **full AI Engineering Manager + Scrum Master + QA Automation system** directly into the existing EAMAS desktop application. Managers configure one AI API key (OpenAI, Claude, or Gemini — their choice) and a GitHub repository. The system then:

- Generates tasks from project requirements using AI
- Assigns tasks to employees via a Trello-like Kanban board inside EAMAS
- Tracks commits and triggers AI code review automatically
- Runs automated QA (lint, tests, build validation)
- Moves Kanban cards through lifecycle (Backlog → In Progress → Review → Done)
- Sends daily AI-generated standups per employee
- Plans sprints autonomously based on velocity

**Everything runs within the existing EAMAS WPF app.** No new frontend is needed. The only addition is a lightweight ASP.NET Core server (`EAMAS.Server`) for GitHub webhook reception — everything else lives in the existing WPF solution.

**Single AI key drives everything.** Managers enter one API key for their preferred provider. The system abstracts OpenAI, Claude, and Gemini behind one interface.

---

## 2. Current System Analysis

### What Already Exists

| Component | Status | Relevance |
|-----------|--------|-----------|
| MongoDB + GridFS | Production-ready | Will store all new task/project data |
| 4-Role RBAC (SuperAdmin / Admin / Manager / Employee) | Production-ready | Manager role gates all new features |
| Alert System | Production-ready | Extend for task notifications |
| Audit Logging | Production-ready | Extend for AI decision logging |
| Settings System | Production-ready | Add AI config here |
| WPF MVVM + DI | Solid pattern | All new pages follow same pattern |
| NavigationService | 8 pages | Add 2 new pages (Tasks, Projects) |
| Activity Monitoring | Production-ready | Correlate with task assignments |
| Screenshot System | Production-ready | No changes needed |

### What Does NOT Exist (Will Be Built)

- Tasks / Kanban board
- Projects management (GitHub repos, AI config)
- Sprint planning
- AI code review engine
- QA automation runner
- GitHub integration (polling + webhooks)
- RAG knowledge base per project
- Daily standup generator
- Developer performance analytics (AI-powered)
- `EAMAS.Server` — webhook receiver

---

## 3. What Is Being Built

```
EAMAS (After This Build)
├── Existing: Activity Monitoring, Screenshots, Alerts, Reports
└── NEW: AI Engineering Manager Suite
    ├── Projects Module (Manager sets up repos + AI keys)
    ├── Tasks / Kanban Module (Employee self-service board)
    ├── AI Task Generator (breaks PRD → tasks → subtasks)
    ├── Sprint Planner (AI-driven sprint creation + velocity)
    ├── GitHub Commit Tracker (polling OR webhooks via server)
    ├── AI Code Reviewer (diffs → AI analysis → comments)
    ├── QA Automation Runner (lint + tests + build)
    ├── AI Daily Standup Generator (per employee, per project)
    └── Developer Performance Insights (AI analytics)
```

### Role Access Matrix

| Feature | Employee | Manager | Admin | SuperAdmin |
|---------|----------|---------|-------|------------|
| View own tasks | YES | YES | YES | YES |
| Move own task cards | YES | YES | YES | YES |
| View all team tasks | NO | YES | YES | YES |
| Create/edit tasks | NO | YES | YES | YES |
| Configure AI key | NO | YES | YES | YES |
| Add GitHub repos | NO | YES | YES | YES |
| View code reviews | NO | YES | YES | YES |
| Trigger AI task generation | NO | YES | YES | YES |
| View developer analytics | NO | YES | YES | YES |
| Manage sprints | NO | YES | YES | YES |

---

## 4. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    EAMAS.Desktop (WPF)                      │
│                                                             │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │  Projects   │  │    Tasks /   │  │  Developer       │  │
│  │  Module     │  │   Kanban     │  │  Analytics       │  │
│  │  (Manager)  │  │   Board      │  │  (AI Insights)   │  │
│  └──────┬──────┘  └──────┬───────┘  └────────┬─────────┘  │
│         │                │                    │             │
│  ┌──────▼────────────────▼────────────────────▼──────────┐ │
│  │               AI Orchestration Layer                   │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────────────────┐ │ │
│  │  │  Task    │  │  Code    │  │  Sprint / Standup     │ │ │
│  │  │Generator │  │ Reviewer │  │  Generator            │ │ │
│  │  └──────────┘  └──────────┘  └──────────────────────┘ │ │
│  └───────────────────────┬────────────────────────────────┘ │
│                          │                                  │
│  ┌───────────────────────▼────────────────────────────────┐ │
│  │              IAiProvider (Abstraction)                  │ │
│  │     OpenAI │ Claude (Anthropic) │ Gemini (Google)      │ │
│  └───────────────────────┬────────────────────────────────┘ │
└──────────────────────────┼──────────────────────────────────┘
                           │ HTTP
           ┌───────────────▼───────────────┐
           │        External APIs          │
           │  OpenAI API / Claude / Gemini  │
           │  GitHub REST API              │
           └───────────────────────────────┘

┌──────────────────────────────────┐
│        EAMAS.Server              │    ← NEW (Phase 4)
│   ASP.NET Core 8 Minimal API     │
│   ┌────────────────────────────┐ │
│   │  GitHub Webhook Endpoint   │ │    GitHub → EAMAS.Server
│   │  /api/github/webhook       │ │    ← POST (push, PR, review)
│   ├────────────────────────────┤ │
│   │  Queue Processor           │ │
│   │  (Hangfire / Background)   │ │
│   ├────────────────────────────┤ │
│   │  QA Runner                 │ │
│   │  (dotnet test, eslint,     │ │
│   │   playwright, sonar)       │ │
│   └────────────────────────────┘ │
└──────────────────────────────────┘
           │
           ▼
     MongoDB (shared)
     Same EAMAS database
     New collections:
     projects, tasks, sprints,
     code_reviews, qa_results,
     standup_logs, project_embeddings
```

---

## 5. New Database Models

### 5.1 Project Model

```csharp
// EAMAS.Core/Models/Project.cs
public class Project
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId OrganizationId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    // GitHub
    public string GitHubRepoOwner { get; set; }      // "microsoft"
    public string GitHubRepoName { get; set; }        // "vscode"
    public string GitHubAccessToken { get; set; }     // Encrypted at rest
    public string DefaultBranch { get; set; }         // "main"
    public string WebhookSecret { get; set; }         // Encrypted, for webhook validation

    // AI Configuration
    public AiProviderType AiProvider { get; set; }    // OpenAI | Claude | Gemini
    public string AiApiKey { get; set; }              // Encrypted at rest
    public string AiModel { get; set; }               // "gpt-4o" | "claude-opus-4-7" | "gemini-2.0-flash"
    public double AiTemperature { get; set; } = 0.3;

    // Project Context
    public string PrdContent { get; set; }            // Pasted PRD / requirements
    public string ArchitectureNotes { get; set; }     // Architecture description
    public string TechStack { get; set; }             // Tech stack description

    // Sprint Config
    public int SprintDurationDays { get; set; } = 14;
    public int WorkHoursPerDay { get; set; } = 8;

    // Status
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ObjectId CreatedByUserId { get; set; }
    public DateTime? LastSyncedAt { get; set; }       // Last GitHub sync
    public string LastKnownCommitSha { get; set; }    // For change detection
}

public enum AiProviderType { OpenAI, Claude, Gemini }
```

### 5.2 Task Model (Kanban Card)

```csharp
// EAMAS.Core/Models/ProjectTask.cs
public class ProjectTask
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId OrganizationId { get; set; }
    public ObjectId ProjectId { get; set; }
    public ObjectId? SprintId { get; set; }

    public string Title { get; set; }
    public string Description { get; set; }           // Markdown content
    public string AcceptanceCriteria { get; set; }    // AI-generated criteria

    // Assignment
    public ObjectId? AssignedToUserId { get; set; }
    public ObjectId CreatedByUserId { get; set; }

    // Kanban
    public TaskStatus Status { get; set; }            // Backlog → Todo → InProgress → Review → QATesting → NeedsFix → Done
    public int BoardPosition { get; set; }            // Order within column
    public TaskPriority Priority { get; set; }        // Low | Medium | High | Critical
    public List<string> Labels { get; set; }          // ["backend", "auth", "bug"]

    // Estimation
    public double? EstimatedHours { get; set; }       // AI-estimated
    public double? ActualHours { get; set; }          // Tracked from activity monitoring
    public DateTime? DueDate { get; set; }

    // Code Linkage
    public string RelatedCommitSha { get; set; }      // Latest commit for this task
    public string GitHubPrUrl { get; set; }

    // AI Fields
    public string AiGeneratedSummary { get; set; }   // AI short summary
    public List<string> SubTasks { get; set; }        // AI-broken subtasks
    public bool IsAiGenerated { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum TaskStatus
{
    Backlog = 0,
    Todo = 1,
    InProgress = 2,
    CodeReview = 3,
    QATesting = 4,
    NeedsFix = 5,
    Done = 6
}

public enum TaskPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }
```

### 5.3 Sprint Model

```csharp
// EAMAS.Core/Models/Sprint.cs
public class Sprint
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId OrganizationId { get; set; }
    public ObjectId ProjectId { get; set; }
    public string Name { get; set; }                  // "Sprint 1", "Sprint 2", ...
    public string Goal { get; set; }                  // AI-generated sprint goal
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public SprintStatus Status { get; set; }          // Planning | Active | Completed
    public List<ObjectId> TaskIds { get; set; }
    public double PlannedVelocity { get; set; }       // Estimated hours
    public double ActualVelocity { get; set; }        // Actual hours completed
    public string AiSprintSummary { get; set; }       // AI-generated retrospective
    public DateTime CreatedAt { get; set; }
}

public enum SprintStatus { Planning, Active, Completed }
```

### 5.4 Code Review Model

```csharp
// EAMAS.Core/Models/CodeReview.cs
public class CodeReview
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId OrganizationId { get; set; }
    public ObjectId ProjectId { get; set; }
    public ObjectId? TaskId { get; set; }             // Related task (if matched)
    public ObjectId? AssignedUserId { get; set; }     // Developer who pushed

    // Commit Info
    public string CommitSha { get; set; }
    public string CommitMessage { get; set; }
    public string CommitAuthor { get; set; }
    public string Branch { get; set; }
    public List<ChangedFile> ChangedFiles { get; set; }

    // AI Review
    public CodeReviewStatus Status { get; set; }
    public int OverallScore { get; set; }             // 0–100
    public string AiSummary { get; set; }
    public List<CodeIssue> Issues { get; set; }
    public List<string> Suggestions { get; set; }
    public bool RequiresHumanApproval { get; set; }   // Always true in Phase 1-2

    // QA
    public QaStatus QaStatus { get; set; }
    public string QaLog { get; set; }

    public DateTime CreatedAt { get; set; }
    public string AiProvider { get; set; }            // Which AI did the review
}

public class ChangedFile
{
    public string FilePath { get; set; }
    public string Status { get; set; }                // added | modified | deleted
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public string Diff { get; set; }                  // git diff content (truncated for large files)
}

public class CodeIssue
{
    public string Severity { get; set; }              // "error" | "warning" | "info"
    public string FilePath { get; set; }
    public int? LineNumber { get; set; }
    public string Category { get; set; }              // "security" | "performance" | "naming" | "logic"
    public string Description { get; set; }
    public string SuggestedFix { get; set; }
}

public enum CodeReviewStatus { Pending, InProgress, Passed, Failed, NeedsHumanReview }
public enum QaStatus { NotRun, Running, Passed, Failed }
```

### 5.5 Standup Log Model

```csharp
// EAMAS.Core/Models/StandupLog.cs
public class StandupLog
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId OrganizationId { get; set; }
    public ObjectId ProjectId { get; set; }
    public ObjectId UserId { get; set; }
    public DateTime Date { get; set; }                // Standup date (date-only)

    // AI-generated content
    public string YesterdayAccomplished { get; set; }
    public string TodayFocus { get; set; }
    public string Blockers { get; set; }
    public string AiGeneratedMessage { get; set; }    // Full formatted standup

    // Source data
    public List<ObjectId> TasksCompletedYesterday { get; set; }
    public List<ObjectId> TasksInProgressToday { get; set; }
    public int CommitsYesterday { get; set; }
    public bool WasDeliveredViaSms { get; set; }      // Future: Slack/Teams
    public DateTime GeneratedAt { get; set; }
}
```

### 5.6 QA Result Model

```csharp
// EAMAS.Core/Models/QaResult.cs
public class QaResult
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId OrganizationId { get; set; }
    public ObjectId ProjectId { get; set; }
    public ObjectId? TaskId { get; set; }
    public string CommitSha { get; set; }

    public QaRunStatus Status { get; set; }
    public List<QaCheck> Checks { get; set; }         // Each check (lint, test, build)
    public string AiQaSummary { get; set; }           // AI validation summary
    public bool FeatureMatchesTask { get; set; }      // AI: does code match task?
    public string FeatureMatchReason { get; set; }    // AI explanation

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class QaCheck
{
    public string Name { get; set; }                  // "ESLint" | "Jest" | "dotnet build"
    public bool Passed { get; set; }
    public string Output { get; set; }                // Console output (truncated)
    public int ExitCode { get; set; }
    public TimeSpan Duration { get; set; }
}

public enum QaRunStatus { Queued, Running, Passed, Failed }
```

### 5.7 Project Embedding (RAG Knowledge Base)

```csharp
// EAMAS.Core/Models/ProjectEmbedding.cs
public class ProjectEmbedding
{
    [BsonId] public ObjectId Id { get; set; }
    public ObjectId ProjectId { get; set; }
    public string ChunkType { get; set; }             // "prd" | "code" | "architecture" | "task"
    public string SourcePath { get; set; }            // File path or document name
    public string Content { get; set; }               // Raw text chunk
    public float[] Embedding { get; set; }            // Vector (1536-dim for OpenAI, 768 for others)
    public DateTime IndexedAt { get; set; }
    public string CommitSha { get; set; }             // Code chunk: which commit
}
```

---

## 6. AI Provider Abstraction Layer

Single interface hides OpenAI / Claude / Gemini differences. Manager picks one; the system works identically regardless.

```csharp
// EAMAS.Core/Services/AI/IAiProvider.cs
public interface IAiProvider
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, int maxTokens = 2000);
    Task<string> CompleteWithContextAsync(string systemPrompt, List<AiMessage> history, int maxTokens = 4000);
    Task<float[]> EmbedAsync(string text);
    AiProviderType ProviderType { get; }
}

public class AiMessage
{
    public string Role { get; set; }    // "user" | "assistant"
    public string Content { get; set; }
}
```

### 6.1 OpenAI Provider

```csharp
// EAMAS.Core/Services/AI/Providers/OpenAiProvider.cs
public class OpenAiProvider : IAiProvider
{
    // Uses: https://api.openai.com/v1/chat/completions
    // Embedding: https://api.openai.com/v1/embeddings (text-embedding-3-small)
    // Models: gpt-4o, gpt-4o-mini, gpt-4-turbo
}
```

### 6.2 Claude Provider

```csharp
// EAMAS.Core/Services/AI/Providers/ClaudeProvider.cs
public class ClaudeProvider : IAiProvider
{
    // Uses: https://api.anthropic.com/v1/messages
    // Embedding: Use voyage-3 via Anthropic or fallback to local
    // Models: claude-opus-4-7, claude-sonnet-4-6, claude-haiku-4-5-20251001
    // Headers: x-api-key, anthropic-version: 2023-06-01
}
```

### 6.3 Gemini Provider

```csharp
// EAMAS.Core/Services/AI/Providers/GeminiProvider.cs
public class GeminiProvider : IAiProvider
{
    // Uses: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
    // Embedding: models/text-embedding-004
    // Models: gemini-2.0-flash, gemini-1.5-pro
}
```

### 6.4 AI Provider Factory

```csharp
// EAMAS.Core/Services/AI/AiProviderFactory.cs
public class AiProviderFactory
{
    public IAiProvider Create(AiProviderType type, string apiKey, string model)
    {
        return type switch
        {
            AiProviderType.OpenAI  => new OpenAiProvider(apiKey, model),
            AiProviderType.Claude  => new ClaudeProvider(apiKey, model),
            AiProviderType.Gemini  => new GeminiProvider(apiKey, model),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
```

---

## 7. New Services (EAMAS.Core)

### 7.1 ProjectService

```
EAMAS.Core/Services/ProjectService.cs

Responsibilities:
- CRUD for Project documents
- Encrypt/decrypt API keys and GitHub tokens before storage
  (Use DPAPI: ProtectedData.Protect — already a dependency)
- Validate GitHub connection (GET /repos/{owner}/{repo})
- Validate AI API key (small test prompt)
- List projects for org (Manager+ only)
```

### 7.2 TaskService

```
EAMAS.Core/Services/TaskService.cs

Responsibilities:
- CRUD for ProjectTask documents
- Move card to next status (with validation rules)
- Assign / unassign employees
- Bulk task creation (from AI generation)
- Get tasks by sprint, by assignee, by status
- Calculate actual hours from ActivityLog data
  (cross-reference: employee active time while task was InProgress)
- Filter by project / sprint / status / assignee
```

### 7.3 SprintService

```
EAMAS.Core/Services/SprintService.cs

Responsibilities:
- Create sprint (AI suggests goal + task selection from Backlog)
- Activate sprint (move tasks to Todo)
- Complete sprint (generate AI retrospective)
- Velocity calculation (previous sprint actual vs planned)
- Move unfinished tasks to next sprint or Backlog
```

### 7.4 AiTaskGeneratorService

```
EAMAS.Core/Services/AI/AiTaskGeneratorService.cs

Responsibilities:
- Accept: PRD text + architecture notes + existing tasks (context)
- Chunk PRD into features
- For each feature: generate task title, description, acceptance criteria,
  estimated hours, priority, labels, subtasks
- Return: List<ProjectTask> (not yet saved — Manager reviews first)

AI Prompt Strategy:
  System: "You are a senior engineering manager breaking down a PRD into
           development tasks. Project context: {project_summary}.
           Existing tasks: {existing_task_titles}.
           Output JSON array of tasks."

  User: "Break down this feature into implementable developer tasks:
         {feature_text}"

Output format (JSON):
  [
    {
      "title": "...",
      "description": "...",
      "acceptanceCriteria": "...",
      "estimatedHours": 4.0,
      "priority": "High",
      "labels": ["backend", "api"],
      "subTasks": ["...", "..."]
    }
  ]
```

### 7.5 AiCodeReviewService

```
EAMAS.Core/Services/AI/AiCodeReviewService.cs

Responsibilities:
- Accept: CommitSha + list of ChangedFiles (with diffs)
- Match commit to a task (by message keywords / branch name)
- Send diffs to AI in batches (max 3000 tokens per file)
- AI checks: naming, architecture, security, performance, logic, duplication
- Return: CodeReview with score and issues list

AI Prompt Strategy:
  System: "You are a senior code reviewer. Project context: {project_summary}.
           Coding conventions: {conventions}. Respond in JSON."

  User: "Review these code changes:
         File: {file_path}
         Diff:
         {diff_content}

         Check for: security vulnerabilities, naming violations,
         performance issues, missing error handling, logic errors."

Security checks (always include):
  - SQL injection / NoSQL injection
  - XSS vulnerabilities
  - Hardcoded secrets or credentials
  - Insecure deserialization
  - Missing input validation

Output format (JSON):
  {
    "overallScore": 78,
    "summary": "...",
    "issues": [
      {
        "severity": "error",
        "filePath": "...",
        "lineNumber": 42,
        "category": "security",
        "description": "...",
        "suggestedFix": "..."
      }
    ],
    "suggestions": ["...", "..."]
  }
```

### 7.6 AiSprintPlannerService

```
EAMAS.Core/Services/AI/AiSprintPlannerService.cs

Responsibilities:
- Read backlog tasks for project
- Read previous sprint velocity
- Read team capacity (employees assigned, estimated work hours from SystemSettings)
- AI selects tasks that fit sprint capacity (priority + dependency order)
- AI generates sprint goal sentence
- Returns proposed sprint plan for Manager approval

AI Prompt Strategy:
  System: "You are an Agile Scrum Master planning a 2-week sprint.
           Team: {team_members}. Capacity: {total_hours} hours.
           Previous velocity: {velocity} hours/sprint."

  User: "From this backlog, select tasks that fit the sprint capacity
         and form a coherent sprint goal:
         {backlog_json}"
```

### 7.7 AiStandupService

```
EAMAS.Core/Services/AI/AiStandupService.cs

Responsibilities:
- Called every morning (configurable time, e.g., 9:00 AM)
- For each employee + active project:
  - Query tasks completed yesterday (status changed to Done)
  - Query tasks in progress today
  - Query commits made yesterday (from code review records)
  - Query any NeedsFix cards assigned to them
  - Feed to AI → generate natural standup message
- Store in StandupLog
- Display in EAMAS alert/notification panel
- Future: post to Slack/Teams via webhook

AI Prompt Strategy:
  System: "You are writing a daily standup update for a software developer.
           Keep it professional and concise (under 150 words total)."

  User: "Generate a standup for {developer_name}:
         Yesterday completed: {completed_tasks}
         Today working on: {active_tasks}
         Commits: {commit_count} commits to {branches}
         Items needing fix: {fix_items}"
```

### 7.8 GitHubPollingService

```
EAMAS.Core/Services/GitHubPollingService.cs

Responsibilities:
- Run on background timer (every 5 minutes per project)
- GET /repos/{owner}/{repo}/commits (since: LastKnownCommitSha)
- For each new commit:
  - GET /repos/{owner}/{repo}/commits/{sha} (file changes + diffs)
  - Create CodeReview record → trigger AiCodeReviewService
  - Match to task (branch naming convention: "task-{taskId}-feature-name")
  - Update LastKnownCommitSha
- Rate limiting: GitHub API = 5000 req/hour per token

GitHub API wrapper (Octokit.net):
  - CommitsClient.GetAll() — paginated commit list
  - CommitsClient.Get()    — single commit with files
  - PullRequestsClient     — for PR events (Phase 2+)
```

### 7.9 RagService (Knowledge Engine)

```
EAMAS.Core/Services/AI/RagService.cs

Responsibilities:
- Index project documents (PRD, architecture, code files)
  - Chunk text into ~500-token segments
  - Embed each chunk using provider's embedding model
  - Store in project_embeddings collection
- Semantic search: given a query, find top-K relevant chunks
  - Cosine similarity on stored embeddings
  - Return top 5 chunks as context
- Used by: AiTaskGeneratorService, AiCodeReviewService
  to inject relevant project context without sending entire codebase

Chunking strategy:
  - PRD/docs: paragraph-based chunking
  - Code files: class/method-level chunking
  - Max chunk size: 500 tokens
  - Overlap: 50 tokens (sliding window)

Note: MongoDB does NOT have native vector search in free/Community tier.
Alternative: store embeddings as float[] and compute cosine similarity
in-memory for the top-N results (works fine for <10,000 chunks).
For larger projects: upgrade to MongoDB Atlas or add Qdrant as sidecar.
```

### 7.10 EncryptionService (for secrets)

```
EAMAS.Core/Services/EncryptionService.cs

Responsibilities:
- Encrypt AI API keys and GitHub tokens before MongoDB storage
- Use DPAPI (ProtectedData) — already in dependencies
- Machine-scope encryption: key is tied to the machine running EAMAS.Server
- For multi-machine: use AES-256 with a master key stored in environment variable

Methods:
  - string Encrypt(string plainText)
  - string Decrypt(string cipherText)
```

---

## 8. New UI Modules (EAMAS.Desktop)

### 8.1 Navigation Updates

Add 2 new pages to the `AppPage` enum and `NavigationService`:
- `Projects` — visible to Manager, Admin, SuperAdmin
- `Tasks` — visible to all roles (filtered by role)

Sidebar entries:
```
[Dashboard]        (all roles)
[Activity Logs]    (all roles)
[Screenshots]      (all roles)
[Reports]          (all roles)
[Tasks]            (all roles)   ← NEW
[Projects]         (Manager+)   ← NEW
[Employees]        (Admin+)
[Alerts]           (all roles)
[Organizations]    (SuperAdmin)
[Settings]         (all roles)
```

### 8.2 Projects View

**File:** `EAMAS.Desktop/Views/ProjectsView.xaml`  
**ViewModel:** `EAMAS.Desktop/ViewModels/ProjectsViewModel.cs`

Layout: Left panel (project list) + Right panel (project details form)

**Sections in form:**
1. **Basic Info** — Name, Description
2. **GitHub Configuration**
   - Repository Owner / Name
   - Personal Access Token (password box, never shown after save)
   - Default branch
   - [Test Connection] button → validate via GitHub API
3. **AI Configuration**
   - Provider dropdown: OpenAI | Claude | Gemini
   - API Key (password box)
   - Model dropdown (populated based on provider)
   - Temperature slider (0.1 – 0.9)
   - [Test Key] button → send test prompt, show response time + token cost
4. **Project Context**
   - PRD / Requirements (multi-line text box or [Import .txt/.md file] button)
   - Architecture Notes (multi-line)
   - Tech Stack description
   - [Re-index Knowledge Base] button (re-embed everything)
5. **Sprint Settings**
   - Sprint duration (days)
   - Work hours per day
6. **Actions bar**
   - [Save] [Generate Tasks from PRD] [Plan New Sprint] [Start Polling]

### 8.3 Tasks / Kanban View

**File:** `EAMAS.Desktop/Views/TasksView.xaml`  
**ViewModel:** `EAMAS.Desktop/ViewModels/TasksViewModel.cs`

Layout: Full-width horizontal Kanban board

**Column structure:**
```
┌──────────┬──────────┬──────────────┬──────────────┬──────────────┬──────────────┬──────────┐
│ Backlog  │   Todo   │  In Progress │ Code Review  │  QA Testing  │  Needs Fix   │   Done   │
│  (12)    │   (5)    │    (3)       │    (2)       │    (1)       │    (1)       │  (34)    │
├──────────┼──────────┼──────────────┼──────────────┼──────────────┼──────────────┼──────────┤
│ [Card]   │ [Card]   │  [Card]      │  [Card]      │  [Card]      │  [Card]      │ [Card]   │
│ [Card]   │ [Card]   │  [Card]      │  [Card]      │              │              │ [Card]   │
│ [Card]   │ [Card]   │  [Card]      │              │              │              │ [Card]   │
│ [Card]   │          │              │              │              │              │ [Card]   │
└──────────┴──────────┴──────────────┴──────────────┴──────────────┴──────────────┴──────────┘
```

**Task Card (WPF UserControl):**
```
┌────────────────────────────────────┐
│ [!HIGH]  [backend] [auth]         │  ← Priority chip + Labels
│ Login API token refresh            │  ← Title
│ Add refresh token logic to auth... │  ← Description (truncated)
├────────────────────────────────────┤
│ 👤 Rahul    ⏱ 4h est.  📅 May 20  │  ← Assignee + Hours + Due
│ [→ Move to Next] [👁 Details]     │  ← Actions
└────────────────────────────────────┘
```

**Top toolbar:**
- Project selector (dropdown — Manager sees all, Employee sees assigned only)
- Sprint selector (Active sprint / All / Backlog only)
- Filter by: Assignee / Priority / Label
- [+ New Task] (Manager only)
- [Generate AI Tasks] (Manager only)
- [Plan Sprint] (Manager only)
- [View Reviews] (Manager only)

**Task Detail Panel (side flyout or modal):**
- Full description (markdown rendered)
- Acceptance criteria (checklist)
- Sub-tasks (checklist, AI-generated)
- Assignee selector
- Priority selector
- Due date picker
- Labels editor
- Actual hours (read-only, from activity monitoring)
- Code review tab (if commit linked)
- QA results tab
- Activity history (status changes)

### 8.4 Code Review View (nested in Tasks)

When a task card is clicked and has a linked commit:
- Show code review score (0–100, color-coded)
- Show AI issues list with severity badges
- Show changed files tree
- Show diff viewer (syntax-highlighted, simplified)
- [Request Human Review] button (opens GitHub PR URL)
- QA results accordion (lint output, test results, build log)

### 8.5 Developer Analytics Extension

Extend the existing `DashboardViewModel` / `ReportsViewModel` for Managers:

New cards on Manager's dashboard:
- **Sprint Burndown**: remaining hours per day (bar chart)
- **Team Velocity**: last 4 sprints (line chart)
- **AI Review Quality**: avg code score per developer (bar chart)
- **Task Throughput**: tasks completed per week per developer

New page section: **AI Insights** (inside Reports view)
- "Rahul has been consistently flagging security issues — recommend a security training session"
- "Payment module velocity dropped 30% — likely caused by scope creep in Sprint 3"
- "Priya's code review scores improved from 62 to 84 over last 4 sprints"

---

## 9. New EAMAS.Server Component

**When needed:** Phase 4 (GitHub webhook reception)  
**Why needed:** GitHub webhooks require a publicly accessible HTTP endpoint. A desktop WPF app cannot receive inbound HTTP from GitHub's servers.

### 9.1 Project Setup

```
EAMAS.Server/
├── EAMAS.Server.csproj          (ASP.NET Core 8, .NET 8)
├── Program.cs                   (Minimal API setup)
├── Endpoints/
│   ├── GitHubWebhookEndpoint.cs  (POST /api/github/webhook)
│   └── HealthEndpoint.cs         (GET /health)
├── Services/
│   ├── WebhookVerificationService.cs  (HMAC-SHA256 signature check)
│   ├── CommitProcessorService.cs      (Process push events)
│   └── QaRunnerService.cs             (Shell command execution)
├── appsettings.json
└── Dockerfile                   (optional, for containerized deploy)
```

### 9.2 Webhook Endpoint

```csharp
// POST /api/github/webhook
// Headers: X-GitHub-Event, X-Hub-Signature-256
// Body: GitHub webhook payload (JSON)

Supported events:
  push         → Extract commits → Run AI code review → Update CodeReview doc
  pull_request → Track PR lifecycle
  push (tag)   → Detect releases

Processing flow:
  1. Verify HMAC-SHA256 signature (WebhookSecret from Project doc)
  2. Identify project from repository full name
  3. Queue commit processing (Hangfire or Channel<T>)
  4. Return 200 OK immediately (GitHub requires <10s response)
  5. Background: pull diff → AI review → QA runner → update MongoDB
```

### 9.3 QA Runner

```csharp
// EAMAS.Server/Services/QaRunnerService.cs

For each commit:
  1. Clone/fetch repo to temp directory
  2. Checkout commit SHA
  3. Run configured QA checks (from project settings):
     - dotnet build (C# projects)
     - dotnet test  (xUnit / NUnit / MSTest)
     - npm run lint (JS/TS projects)
     - npm test / jest
     - eslint .
     - prettier --check .
  4. Capture stdout/stderr + exit code
  5. Store in QaResult document
  6. AI QA Validation:
     - "Given this task: {task}, and these test results: {results},
       does the implementation match the requirements?"
  7. If all passed: move Kanban card to Done
  8. If failed: move to Needs Fix, add AI comment explaining issue
```

### 9.4 Deployment Options for EAMAS.Server

| Option | Cost | Effort | Best For |
|--------|------|--------|----------|
| **Ngrok** (dev/testing) | Free | 5 min | Development testing |
| **Railway.app** | ~$5/mo | 30 min | Small teams |
| **Azure App Service (Free tier)** | $0 | 1 hour | Small teams |
| **Self-hosted VPS** (DigitalOcean $6/mo) | $6/mo | 2 hours | Full control |
| **Docker on team server** | $0 extra | 1 hour | If you have a server |

**Recommended:** Azure App Service Free tier (F1) for MVP — zero cost, GitHub Actions deploy.

---

## 10. GitHub Integration Strategy

### Phase 1–3: Polling (No Server Required)

```
EAMAS.Desktop (background thread)
   ↓ Every 5 minutes (per project)
   ↓ GET /repos/{owner}/{repo}/commits?since={lastKnownCommit}
   ↓ If new commits found:
   ↓   GET /repos/{owner}/{repo}/commits/{sha} (files + diffs)
   ↓   Trigger AI code review (in-process)
   ↓   Update MongoDB
   ↓   Refresh Kanban UI

Rate limit management:
  - GitHub: 5000 req/hour per token
  - Each poll: ~1-3 requests
  - 5 min interval × 12/hour = ~36 requests/hour → well within limits
```

### Phase 4: Webhooks (With EAMAS.Server)

```
GitHub Repository Settings
   → Webhooks → Add webhook
   → Payload URL: https://your-server.com/api/github/webhook
   → Content type: application/json
   → Secret: {WebhookSecret from Project config}
   → Events: push, pull_request

EAMAS.Server receives →  validates → processes → writes to MongoDB
EAMAS.Desktop reads MongoDB (polling MongoDB for CodeReview changes)
   → Every 30 seconds: check for new CodeReview docs with status = pending
   → Display results in Kanban / Tasks view
```

### Branch Naming Convention for Task Linking

```
Convention: task-{taskId}-{description}
Examples:
  task-507f1f77bcf86cd799439011-login-api-refresh
  task-507f1f77bcf86cd799439012-fix-dashboard-pagination
  bugfix/task-507f...
  feature/task-507f...

The system extracts taskId from branch name and links commit to task.
Fallback: keyword matching in commit message vs task titles (AI-assisted).
```

---

## 11. RAG Knowledge Engine

### Why RAG (Not Direct Full-Repo Injection)

Sending entire codebase to AI on every request:
- **Expensive**: 100k tokens × $0.03/1k = $3 per review
- **Slow**: 30+ seconds latency
- **Limited**: Most models: 128k–200k context window max
- **Inaccurate**: More tokens = higher hallucination risk

RAG sends only the **relevant 3–5 chunks** (1,500–2,500 tokens) as context.

### Indexing Workflow

```
Manager uploads PRD / architecture notes in Projects view
         ↓
[Re-index Knowledge Base] button clicked
         ↓
RagService.IndexProjectAsync(projectId):
  1. Read PRD text → chunk into 500-token paragraphs
  2. Read architecture notes → chunk
  3. Embed each chunk via provider's embedding model
  4. Store in project_embeddings collection
         ↓
GitHubPollingService (background):
  On each new commit → fetch changed files
  Chunk changed files by class/method
  Embed and upsert to project_embeddings (keyed by file + commit)
```

### Query Workflow (At Review Time)

```
AiCodeReviewService.ReviewCommit():
  1. Compose query: "Review authentication token refresh code"
  2. RagService.SearchAsync(query, projectId, topK: 5)
     → Embed query → cosine similarity → return top 5 chunks
  3. Inject chunks as context:
     System prompt + "Relevant project context: {chunks}"
  4. Send to AI for review
```

### Cosine Similarity (In-Memory, No Vector DB Required)

```csharp
// For <10,000 embeddings, in-memory is fast enough (< 50ms)
float[] queryEmbedding = await provider.EmbedAsync(query);
var results = embeddings
    .Select(e => (e, CosineSimilarity(queryEmbedding, e.Embedding)))
    .OrderByDescending(x => x.Item2)
    .Take(topK)
    .ToList();

static float CosineSimilarity(float[] a, float[] b) {
    float dot = 0, magA = 0, magB = 0;
    for (int i = 0; i < a.Length; i++) {
        dot += a[i] * b[i]; magA += a[i]*a[i]; magB += b[i]*b[i];
    }
    return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
}
```

---

## 12. Implementation Phases

### Phase 1: Foundation (Weeks 1–4)
**Goal:** Tasks / Kanban board + Project setup + Basic AI task generation

#### Week 1: Database + Core Models
- [ ] Add new MongoDB models: Project, ProjectTask, Sprint
- [ ] Add ProjectService (CRUD, encrypt/decrypt)
- [ ] Add TaskService (CRUD, status transitions)
- [ ] Create MongoDB indexes for new collections
- [ ] Add EncryptionService (DPAPI wrapper)

#### Week 2: Projects UI
- [ ] Add `Projects` page to NavigationService
- [ ] Build ProjectsView.xaml (form layout)
- [ ] Build ProjectsViewModel.cs
- [ ] GitHub connection test (Octokit.net)
- [ ] AI API key test (small prompt)
- [ ] AiProviderFactory + OpenAiProvider (start with OpenAI only)

#### Week 3: Kanban UI
- [ ] Add `Tasks` page to NavigationService
- [ ] Build TaskCard UserControl (WPF)
- [ ] Build TasksView.xaml (horizontal scroll, columns)
- [ ] Build TasksViewModel.cs (load, filter, role-based)
- [ ] Task detail flyout panel
- [ ] Drag-and-drop card movement (WPF DragDrop)

#### Week 4: AI Task Generation
- [ ] AiTaskGeneratorService
- [ ] PRD import in Projects view
- [ ] [Generate Tasks from PRD] button flow
- [ ] Review/edit generated tasks before saving
- [ ] Add Claude + Gemini providers
- [ ] Basic sprint creation (manual, no AI yet)

**Milestone:** Manager enters GitHub repo + AI key → uploads PRD → AI generates tasks → employees see Kanban.

---

### Phase 2: GitHub + AI Code Review (Weeks 5–8)
**Goal:** Track commits + automated AI review → Kanban automation

#### Week 5: GitHub Polling
- [ ] GitHubPollingService (background timer)
- [ ] Fetch commits since last known SHA
- [ ] Fetch file diffs per commit
- [ ] Store raw commits in CodeReview collection (status: Pending)
- [ ] Branch-to-task matching logic

#### Week 6: AI Code Review Engine
- [ ] AiCodeReviewService
- [ ] Diff batching (large files → multiple AI calls)
- [ ] RagService (chunk + embed + cosine search)
- [ ] Knowledge base indexing from PRD
- [ ] Review result storage + score calculation

#### Week 7: Review UI
- [ ] Code review panel in Task detail flyout
- [ ] Issues list with severity color coding
- [ ] Score gauge (0–100)
- [ ] Kanban automation: review passed → move to QA Testing
- [ ] Kanban automation: review failed → move to Needs Fix

#### Week 8: Polish + Testing
- [ ] Test with multiple AI providers (OpenAI, Claude, Gemini)
- [ ] Edge cases: binary files, large diffs (>10k lines)
- [ ] Rate limiting for GitHub API
- [ ] Error handling + retry logic
- [ ] UI loading states + progress indicators

**Milestone:** Developer pushes commit → system detects → AI reviews → card moves automatically.

---

### Phase 3: QA Automation + Sprint Management (Weeks 9–12)
**Goal:** Full QA pipeline + AI-driven sprint planning + daily standups

#### Week 9: QA Runner (In EAMAS.Desktop for local repos)
- [ ] QARunnerService (runs shell commands)
- [ ] Support: dotnet build, dotnet test, npm test, eslint
- [ ] QaResult model + storage
- [ ] AI QA validation (feature match check)
- [ ] QA results tab in task detail view

#### Week 10: Sprint Planner
- [ ] SprintService (CRUD)
- [ ] AiSprintPlannerService (velocity-aware task selection)
- [ ] Sprint planning dialog (review AI suggestion, adjust)
- [ ] Sprint activation (move tasks to Todo)
- [ ] Sprint completion + AI retrospective

#### Week 11: Daily Standup
- [ ] AiStandupService (generate per employee per project)
- [ ] Scheduled execution (configurable time via settings)
- [ ] Standup display in EAMAS alert panel
- [ ] Standup log storage + history view

#### Week 12: Analytics
- [ ] Extend Manager dashboard: sprint burndown, velocity chart
- [ ] Developer code quality scores over time
- [ ] AI narrative insights per developer
- [ ] Sprint prediction (will we finish on time?)

**Milestone:** Autonomous sprint management — system plans, tracks, and reports on sprints without manager intervention.

---

### Phase 4: Server + Webhooks (Weeks 13–16)
**Goal:** Real-time webhook integration + full autonomy

#### Week 13: EAMAS.Server setup
- [ ] New ASP.NET Core 8 project in solution
- [ ] GitHub webhook endpoint
- [ ] HMAC-SHA256 signature verification
- [ ] MongoDB connection (shared database)
- [ ] Hangfire or Channel<T> for async processing

#### Week 14: Webhook Processing
- [ ] Push event handler
- [ ] PR event handler
- [ ] QA runner in server context
- [ ] Webhook registration helper (auto-configure GitHub webhook)
- [ ] Health check endpoint

#### Week 15: Deployment + DevOps
- [ ] GitHub Actions workflow for EAMAS.Server
- [ ] Azure App Service deploy
- [ ] Environment variable config (MongoDB URI, secrets)
- [ ] ngrok config for local development
- [ ] Monitoring/logging (Serilog → Azure Application Insights)

#### Week 16: Advanced Features
- [ ] PR code review (not just commits)
- [ ] AI PR description auto-fill
- [ ] Slack/Teams notification via incoming webhook
- [ ] Performance analytics dashboard
- [ ] Multi-project aggregated reports

**Milestone:** Full autonomous engineering manager — real-time, event-driven, zero manual intervention.

---

## 13. Complete File Structure

### EAMAS.Core (additions only)

```
EAMAS.Core/
├── Models/
│   ├── Project.cs                    NEW
│   ├── ProjectTask.cs                NEW
│   ├── Sprint.cs                     NEW
│   ├── CodeReview.cs                 NEW
│   ├── QaResult.cs                   NEW
│   ├── StandupLog.cs                 NEW
│   ├── ProjectEmbedding.cs           NEW
│   └── ... (existing unchanged)
├── Enums/
│   ├── AiProviderType.cs             NEW
│   ├── TaskStatus.cs                 NEW
│   ├── TaskPriority.cs               NEW
│   ├── SprintStatus.cs               NEW
│   └── ... (existing unchanged)
├── Services/
│   ├── ProjectService.cs             NEW
│   ├── TaskService.cs                NEW
│   ├── SprintService.cs              NEW
│   ├── GitHubPollingService.cs       NEW
│   ├── EncryptionService.cs          NEW
│   ├── AI/
│   │   ├── IAiProvider.cs            NEW
│   │   ├── AiMessage.cs              NEW
│   │   ├── AiProviderFactory.cs      NEW
│   │   ├── RagService.cs             NEW
│   │   ├── AiTaskGeneratorService.cs NEW
│   │   ├── AiCodeReviewService.cs    NEW
│   │   ├── AiSprintPlannerService.cs NEW
│   │   ├── AiStandupService.cs       NEW
│   │   └── Providers/
│   │       ├── OpenAiProvider.cs     NEW
│   │       ├── ClaudeProvider.cs     NEW
│   │       └── GeminiProvider.cs     NEW
│   └── ... (existing unchanged)
└── Data/
    └── MongoDbContext.cs             MODIFIED (add new collections)
```

### EAMAS.Desktop (additions only)

```
EAMAS.Desktop/
├── ViewModels/
│   ├── ProjectsViewModel.cs          NEW
│   ├── TasksViewModel.cs             NEW
│   ├── TaskDetailViewModel.cs        NEW
│   ├── SprintPlannerViewModel.cs     NEW
│   ├── CodeReviewViewModel.cs        NEW
│   ├── StandupViewModel.cs           NEW
│   ├── DashboardViewModel.cs         MODIFIED (add sprint + team KPIs for managers)
│   └── ... (existing unchanged)
├── Views/
│   ├── ProjectsView.xaml             NEW
│   ├── ProjectsView.xaml.cs          NEW
│   ├── TasksView.xaml                NEW
│   ├── TasksView.xaml.cs             NEW
│   ├── TaskDetailPanel.xaml          NEW (flyout side panel)
│   ├── TaskDetailPanel.xaml.cs       NEW
│   ├── SprintPlannerDialog.xaml      NEW
│   ├── SprintPlannerDialog.xaml.cs   NEW
│   └── ... (existing unchanged)
├── Controls/
│   ├── TaskCard.xaml                 NEW (Kanban card UserControl)
│   ├── TaskCard.xaml.cs              NEW
│   ├── KanbanColumn.xaml             NEW (column with card list)
│   ├── KanbanColumn.xaml.cs          NEW
│   ├── CodeIssueItem.xaml            NEW (review issue display)
│   ├── ScoreGauge.xaml               NEW (0–100 gauge)
│   └── SimpleBarChart.xaml           EXISTING
├── Services/
│   ├── TaskSyncService.cs            NEW (poll MongoDB for review updates)
│   └── ... (existing unchanged)
└── App.xaml.cs                       MODIFIED (register new DI services)
```

### EAMAS.Server (new project — Phase 4)

```
EAMAS.Server/
├── EAMAS.Server.csproj
├── Program.cs
├── Endpoints/
│   ├── GitHubWebhookEndpoint.cs
│   └── HealthEndpoint.cs
├── Services/
│   ├── WebhookVerificationService.cs
│   ├── CommitProcessorService.cs
│   └── QaRunnerService.cs
├── appsettings.json
├── appsettings.Development.json
├── Dockerfile
└── .github/
    └── workflows/
        └── deploy-server.yml
```

---

## 14. New NuGet Dependencies

### EAMAS.Core (additions)

| Package | Version | Purpose |
|---------|---------|---------|
| `Octokit` | ~13.x | GitHub REST API client |
| `System.Net.Http.Json` | Built-in .NET 8 | AI provider HTTP calls |

> **No heavy ML/SK dependency needed.** Direct HttpClient calls to AI APIs are simpler and give more control.

### EAMAS.Desktop (additions)

| Package | Version | Purpose |
|---------|---------|---------|
| `GongSolutions.WPF.DragDrop` | ~3.x | Drag-and-drop for Kanban cards |
| `Markdig` | ~0.37 | Render markdown in task descriptions |

### EAMAS.Server (new project)

| Package | Version | Purpose |
|---------|---------|---------|
| `Hangfire.AspNetCore` | ~1.8.x | Background job queue |
| `Hangfire.Mongo` | ~1.9.x | MongoDB storage for Hangfire |
| `MongoDB.Driver` | ~2.30.x | Same as Core |
| `Serilog.AspNetCore` | ~8.x | Structured logging |
| `LibGit2Sharp` | ~0.30.x | Clone/checkout repos for QA runner |

---

## 15. MongoDB Schema Additions

### New Collections

```javascript
// projects collection
{
  _id: ObjectId,
  organizationId: ObjectId,
  name: String,
  description: String,
  gitHubRepoOwner: String,
  gitHubRepoName: String,
  gitHubAccessToken: String,      // Encrypted
  defaultBranch: String,
  webhookSecret: String,          // Encrypted
  aiProvider: int,                // 0=OpenAI, 1=Claude, 2=Gemini
  aiApiKey: String,               // Encrypted
  aiModel: String,
  aiTemperature: double,
  prdContent: String,
  architectureNotes: String,
  techStack: String,
  sprintDurationDays: int,
  workHoursPerDay: int,
  isActive: bool,
  createdAt: Date,
  createdByUserId: ObjectId,
  lastSyncedAt: Date,
  lastKnownCommitSha: String
}

// Indexes:
db.projects.createIndex({ organizationId: 1 })
db.projects.createIndex({ organizationId: 1, gitHubRepoOwner: 1, gitHubRepoName: 1 })

// tasks collection
{
  _id: ObjectId,
  organizationId: ObjectId,
  projectId: ObjectId,
  sprintId: ObjectId,
  title: String,
  description: String,
  acceptanceCriteria: String,
  assignedToUserId: ObjectId,
  createdByUserId: ObjectId,
  status: int,                    // 0-6
  boardPosition: int,
  priority: int,                  // 0-3
  labels: [String],
  estimatedHours: double,
  actualHours: double,
  dueDate: Date,
  relatedCommitSha: String,
  gitHubPrUrl: String,
  aiGeneratedSummary: String,
  subTasks: [String],
  isAiGenerated: bool,
  createdAt: Date,
  updatedAt: Date,
  startedAt: Date,
  completedAt: Date
}

// Indexes:
db.tasks.createIndex({ organizationId: 1, projectId: 1, status: 1 })
db.tasks.createIndex({ organizationId: 1, assignedToUserId: 1, status: 1 })
db.tasks.createIndex({ projectId: 1, sprintId: 1 })

// sprints collection
{
  _id: ObjectId,
  organizationId: ObjectId,
  projectId: ObjectId,
  name: String,
  goal: String,
  startDate: Date,
  endDate: Date,
  status: int,                    // 0=Planning, 1=Active, 2=Completed
  taskIds: [ObjectId],
  plannedVelocity: double,
  actualVelocity: double,
  aiSprintSummary: String,
  createdAt: Date
}

// code_reviews collection
{
  _id: ObjectId,
  organizationId: ObjectId,
  projectId: ObjectId,
  taskId: ObjectId,
  assignedUserId: ObjectId,
  commitSha: String,
  commitMessage: String,
  commitAuthor: String,
  branch: String,
  changedFiles: [{ filePath, status, additions, deletions, diff }],
  status: int,                    // 0-4
  overallScore: int,
  aiSummary: String,
  issues: [{ severity, filePath, lineNumber, category, description, suggestedFix }],
  suggestions: [String],
  requiresHumanApproval: bool,
  qaStatus: int,
  qaLog: String,
  createdAt: Date,
  aiProvider: String
}

// Indexes:
db.code_reviews.createIndex({ projectId: 1, commitSha: 1 }, { unique: true })
db.code_reviews.createIndex({ taskId: 1 })
db.code_reviews.createIndex({ projectId: 1, createdAt: -1 })

// qa_results collection
{
  _id: ObjectId,
  organizationId: ObjectId,
  projectId: ObjectId,
  taskId: ObjectId,
  commitSha: String,
  status: int,
  checks: [{ name, passed, output, exitCode, durationMs }],
  aiQaSummary: String,
  featureMatchesTask: bool,
  featureMatchReason: String,
  startedAt: Date,
  completedAt: Date
}

// standup_logs collection
{
  _id: ObjectId,
  organizationId: ObjectId,
  projectId: ObjectId,
  userId: ObjectId,
  date: Date,
  yesterdayAccomplished: String,
  todayFocus: String,
  blockers: String,
  aiGeneratedMessage: String,
  tasksCompletedYesterday: [ObjectId],
  tasksInProgressToday: [ObjectId],
  commitsYesterday: int,
  generatedAt: Date
}

// Indexes:
db.standup_logs.createIndex({ organizationId: 1, userId: 1, date: -1 })
db.standup_logs.createIndex({ projectId: 1, date: -1 })

// project_embeddings collection
{
  _id: ObjectId,
  projectId: ObjectId,
  chunkType: String,
  sourcePath: String,
  content: String,
  embedding: [double],            // float array (1536 dims for OpenAI)
  indexedAt: Date,
  commitSha: String
}

// Indexes:
db.project_embeddings.createIndex({ projectId: 1, chunkType: 1 })
db.project_embeddings.createIndex({ projectId: 1, sourcePath: 1, commitSha: 1 })
```

---

## 16. Security Considerations

### API Key Storage
- All API keys (AI + GitHub) are encrypted with DPAPI before storage in MongoDB
- DPAPI keys are machine-scoped (encrypted data only decryptable on the same machine)
- For EAMAS.Server: keys stored in environment variables, never in database or code
- Keys are never logged, never shown in UI after initial save (password boxes only)

### GitHub Token Permissions (Minimum Required)
```
Repository: Contents (read)     → fetch commits, file diffs
Repository: Metadata (read)     → repo info
Repository: Webhooks (write)    → auto-configure webhook (optional)
Pull requests: Read             → PR tracking
```
Do NOT grant: admin, write access, organization, user data.

### AI Prompt Security
- Never inject raw user input directly into AI prompts without sanitization
- PRD/architecture content is sanitized (strip HTML, validate encoding) before embedding in prompts
- Code diffs are truncated (max 3000 tokens per file) to prevent prompt injection via malicious code comments
- AI responses are parsed as JSON (structured output), not executed

### Code Review Sandboxing (QA Runner)
- Clone repo to isolated temp directory (deleted after run)
- QA commands run with limited shell permissions (no network access during tests)
- Timeout: 10 minutes max per QA run
- Output captured and stored; never returned to end-user as raw HTML

### Human-in-the-Loop (Critical)
- AI code review results are **informational only** in Phase 1–3
- No code is auto-merged or auto-deployed
- Kanban card moves require either Manager approval OR explicit human action
- All AI decisions are stored in audit log with full prompt/response for traceability

---

## 17. Challenges & Mitigations

| Challenge | Risk | Mitigation |
|-----------|------|------------|
| AI Hallucination in code review | AI flags false positives, frustrates devs | Show confidence score; allow dev to dismiss; track dismissal patterns |
| Large repo = expensive AI calls | High token cost | RAG: only send relevant 2-3K tokens. Cache embeddings. Skip binary/generated files |
| GitHub rate limit (5000 req/hr) | Polling fails for large teams | Exponential backoff; intelligent polling (only if LastModified changed); Phase 4 webhooks |
| Dev resistance to AI review | Team ignores feedback | Start in "advisory" mode (no card blocking); gamify code score improvements |
| DPAPI cross-machine issues | Encrypted keys unreadable on new machine | Export/import flow: manager re-enters key if DB is migrated to new server |
| QA runner: project-specific setup | Different repos need different commands | Per-project QA config in Projects view (configurable command list) |
| Stale embeddings after code changes | AI review has outdated context | Re-index on each polling cycle (only changed files); TTL on code embeddings |
| Context window limits | Large PRDs or diffs exceed AI limits | Chunked processing; summarize large docs first; sliding window for diffs |

---

## 18. Recommended Timeline

| Week | Phase | Deliverable |
|------|-------|-------------|
| 1 | 1 | New DB models, ProjectService, TaskService, EncryptionService |
| 2 | 1 | Projects view (UI) — GitHub + AI key config |
| 3 | 1 | Kanban board UI — TaskCard, KanbanColumn, drag-and-drop |
| 4 | 1 | AI task generation from PRD, all 3 providers |
| 5 | 2 | GitHub polling service — commit detection |
| 6 | 2 | AI code review engine + RAG indexing |
| 7 | 2 | Review UI in task detail — score, issues, Kanban automation |
| 8 | 2 | Polish, edge cases, multi-provider testing |
| 9 | 3 | QA runner (local) — build, test, lint |
| 10 | 3 | Sprint planner AI — velocity-based sprint creation |
| 11 | 3 | Daily standup generator — per employee, per project |
| 12 | 3 | Analytics: burndown, velocity, developer quality scores |
| 13 | 4 | EAMAS.Server — ASP.NET Core, GitHub webhook endpoint |
| 14 | 4 | Webhook processing — real-time commit tracking |
| 15 | 4 | Deployment — Azure App Service, GitHub Actions CI/CD |
| 16 | 4 | Advanced: PR review, Slack notifications, multi-project analytics |

**Total: 16 weeks to full production system.**  
**MVP (Kanban + AI tasks + basic review): 4 weeks.**

---

## Summary: What Changes in Each Project File

### EAMAS.Core.csproj — Add:
```xml
<PackageReference Include="Octokit" Version="13.*" />
```

### EAMAS.Desktop.csproj — Add:
```xml
<PackageReference Include="GongSolutions.WPF.DragDrop" Version="3.*" />
<PackageReference Include="Markdig" Version="0.37.*" />
```

### EAMAS.Server.csproj — New project:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hangfire.AspNetCore" Version="1.8.*" />
    <PackageReference Include="Hangfire.Mongo" Version="1.9.*" />
    <PackageReference Include="MongoDB.Driver" Version="2.30.*" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.*" />
    <PackageReference Include="LibGit2Sharp" Version="0.30.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\EAMAS.Core\EAMAS.Core.csproj" />
  </ItemGroup>
</Project>
```

### App.xaml.cs — Add DI registrations:
```csharp
services.AddSingleton<AiProviderFactory>();
services.AddSingleton<RagService>();
services.AddSingleton<ProjectService>();
services.AddSingleton<TaskService>();
services.AddSingleton<SprintService>();
services.AddSingleton<GitHubPollingService>();
services.AddSingleton<AiTaskGeneratorService>();
services.AddSingleton<AiCodeReviewService>();
services.AddSingleton<AiSprintPlannerService>();
services.AddSingleton<AiStandupService>();
services.AddTransient<ProjectsViewModel>();
services.AddTransient<TasksViewModel>();
services.AddTransient<SprintPlannerViewModel>();
```

### MongoDbContext.cs — Add:
```csharp
public IMongoCollection<Project> Projects => _db.GetCollection<Project>("projects");
public IMongoCollection<ProjectTask> Tasks => _db.GetCollection<ProjectTask>("tasks");
public IMongoCollection<Sprint> Sprints => _db.GetCollection<Sprint>("sprints");
public IMongoCollection<CodeReview> CodeReviews => _db.GetCollection<CodeReview>("code_reviews");
public IMongoCollection<QaResult> QaResults => _db.GetCollection<QaResult>("qa_results");
public IMongoCollection<StandupLog> StandupLogs => _db.GetCollection<StandupLog>("standup_logs");
public IMongoCollection<ProjectEmbedding> ProjectEmbeddings => _db.GetCollection<ProjectEmbedding>("project_embeddings");
```

---

*End of Plan. This document covers the complete architecture, all new code components, database schema, AI integration, GitHub integration, security model, phased implementation roadmap, and deployment strategy for the AI Engineering Manager system within EAMAS.*
