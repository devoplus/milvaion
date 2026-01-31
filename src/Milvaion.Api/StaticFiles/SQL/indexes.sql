-- =================================================
-- Extensions
-- =================================================
-- pg_trgm for efficient pattern matching (ILike)
--CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- =================================================
-- Users Table Indexes
-- =================================================
-- GIN index for fast lookups in JSONB or array columns
CREATE INDEX IF NOT EXISTS "IX_Users_AllowedNotifications"
ON "Users" USING gin ("AllowedNotifications");

-- =================================================
-- JobOccurrences - Primary Access Patterns
-- =================================================

-- CRITICAL: Unique index for StatusTrackerService batch lookups
-- StatusTrackerService does: WHERE CorrelationId IN (...) - this is HIGHEST frequency query
-- Also used by FailedOccurrenceHandler: WHERE CorrelationId = X
CREATE UNIQUE INDEX IF NOT EXISTS "IX_JobOccurrences_CorrelationId_Unique"
ON "JobOccurrences" ("CorrelationId" DESC);

-- CRITICAL: Composite index for UI listing (default view)
-- UI pattern: ORDER BY CreatedAt DESC with optional Status filter
-- Replaces old CorrelationId sorting (CreatedAt is better for time-series data)
-- Covers: WHERE Status = X ORDER BY CreatedAt DESC
-- Also covers: ORDER BY CreatedAt DESC (partial scan)
CREATE INDEX IF NOT EXISTS "IX_JobOccurrences_Status_CreatedAt_Covering"
ON "JobOccurrences" ("Status", "CreatedAt" DESC)
INCLUDE (
    "Id", "JobId", "JobName", "CorrelationId", "WorkerId",
    "StartTime", "EndTime", "DurationMs"
);

CREATE INDEX "IX_JobOccurrences_CreatedAt_Recent"
ON public."JobOccurrences" ("CreatedAt" DESC)
INCLUDE ("Id", "JobId", "JobName", "CorrelationId", "WorkerId", "Status", "StartTime", "EndTime", "DurationMs");

-- =================================================
-- JobOccurrences - Background Services (Zombie Detector)
-- =================================================

-- Partial index for Zombie Queued detection (Status = 0: Queued)
-- Scans only pending jobs, making the background check extremely light.
CREATE INDEX IF NOT EXISTS "IX_JobOccurrences_Zombie_Queued"
ON "JobOccurrences" ("CreatedAt" ASC)
WHERE "Status" = 0;

-- Partial index for Lost Running detection (Status = 1: Running)
-- Used for heartbeat monitoring. Ignores 99% of the table (Completed/Failed jobs).
CREATE INDEX IF NOT EXISTS "IX_JobOccurrences_Zombie_Running_Heartbeat"
ON "JobOccurrences" ("LastHeartbeat" ASC)
WHERE "Status" = 1;

-- =================================================
-- JobOccurrences - Dispatcher & Filters
-- =================================================

-- Optimized partial index for retryable queued job occurrences
CREATE INDEX IF NOT EXISTS "IX_JobOccurrences_RetryQueued"
ON "JobOccurrences" ("NextDispatchRetryAt", "DispatchRetryCount")
WHERE "Status" = 0 AND "NextDispatchRetryAt" IS NOT NULL;