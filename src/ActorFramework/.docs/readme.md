# Load Balanced Actor Model Concurrency in Director

## Load Balancing Algorithm

```mermaid
flowchart TD
  %% Start
  A([Start RouteAsync]) --> B{Is message type registered?}

  %% Registration check
  B -- No --> C[Throw InvalidOperationException<br/>'No actor registered']
  B -- Yes --> D[GetDirectorForMessage]

  %% Director exists?
  D --> E{director != null?}

  %% Path when director exists
  E -- Yes --> F{queuedCount > halfCapacity?}
  F -- Yes --> G[director = GetFirstAvailable<br/>or CreateDirector<br/>or GetLeastLoadedIdleDirector]
  F -- No --> H[Use existing director]
  G --> I[Send]
  H --> I

  %% Path when no initial director
  E -- No --> J[director = GetFirstAvailable<br/>or CreateDirector]
  J --> K{director == null?}
  K -- Yes --> L[PruneIdleDirectors]
  K -- No --> M[Proceed to re-select]

  L --> N[Re-select director]
  M --> N

  %% Final selection
  N --> O{Director found?}
  O -- Yes --> P[Send]
  O -- No --> Q[Throw InvalidOperationException<br/>'No directors available']
```

## State Diagram of Director Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Created: CreateDirector()

    Created --> Active: Send(message)

    Active --> Active: Send(message)
    Active --> Idle: Queue empties

    Idle --> Active: New message arrives
    Idle --> PrunePending: IdleThresholdExceeded

    PrunePending --> Pruned: PruneIdleDirectors()

    Pruned --> [*]
```

# Actor Model Concurrency in Director

## 🧠 How is concurrency handled in the actor model?

**Director** is dispatching actor messages via a single-threaded loop (e.g. using a `Task.Run`): it's effectively **scheduling actors on one thread**, even if the actors themselves are async.

---

### 🎬 What the Director Is Doing

- ✅ It’s **single-threaded** in terms of scheduling.
- ✅ It ensures **message ordering** and **non-concurrent dispatch**.
- ✅ It avoids race conditions between actors sharing state.

---

### 🧠 But What About Actor Execution?

If `DispatchToActor` is async and actors do I/O or await tasks:

- The **scheduling** is single-threaded.
- The **execution** of actor logic may hop threads (via `await`), but each actor still processes messages sequentially.
- You’re not blocking the thread—just sequencing the work.

---

### 🧵 Analogy: Like a Mailroom Clerk

Think of the director as a clerk who hands out mail (messages) to actors one at a time. Even if the actors go off and do async work, the clerk doesn’t hand out the next message until the previous one is "accepted" (i.e. dispatched).

---

### 🧩 Why This Matters

- ✅ Preserves **actor isolation**.
- ✅ Avoids **concurrent access** to actor state.
- ✅ Makes **metrics and tracing** easier (you know exactly when and where messages are dispatched).

---

If you're considering scaling this up—e.g. multiple directors, partitioned queues, or mailbox types with parallelism—then we can talk about how to preserve actor guarantees while increasing throughput. Want to go there?
