-- PostgreSQL schema for the ADO.NET Notes service.
-- Apply through the deployment workflow; this script is additive for a new database.

CREATE TABLE IF NOT EXISTS notes (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    title varchar(200) NOT NULL,
    content text NOT NULL DEFAULT '',
    color varchar(50) NULL,
    is_pinned boolean NOT NULL DEFAULT FALSE,
    is_archived boolean NOT NULL DEFAULT FALSE,
    is_trashed boolean NOT NULL DEFAULT FALSE,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_notes_user_active
    ON notes (user_id, is_pinned DESC, created_at_utc DESC)
    WHERE is_archived = FALSE AND is_trashed = FALSE;

CREATE INDEX IF NOT EXISTS ix_notes_user_archived
    ON notes (user_id, updated_at_utc DESC)
    WHERE is_archived = TRUE AND is_trashed = FALSE;

CREATE INDEX IF NOT EXISTS ix_notes_user_trashed
    ON notes (user_id, updated_at_utc DESC)
    WHERE is_trashed = TRUE;

CREATE TABLE IF NOT EXISTS labels (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    name varchar(100) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_labels_user_name ON labels (user_id, name);

CREATE TABLE IF NOT EXISTS note_labels (
    note_id uuid NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    label_id uuid NOT NULL REFERENCES labels(id) ON DELETE CASCADE,
    PRIMARY KEY (note_id, label_id)
);

CREATE INDEX IF NOT EXISTS ix_note_labels_label_id ON note_labels (label_id);
