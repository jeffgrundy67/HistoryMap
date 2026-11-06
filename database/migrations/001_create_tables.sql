-- History Map — Azure SQL / T-SQL schema
-- Migration: 001_create_tables
-- Target: Azure SQL Database (compatible with SQL Server 2019+)

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMigrations
    (
        MigrationId   NVARCHAR(64)  NOT NULL CONSTRAINT PK_SchemaMigrations PRIMARY KEY,
        AppliedAtUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_SchemaMigrations_AppliedAtUtc DEFAULT (SYSUTCDATETIME()),
        Notes         NVARCHAR(512) NULL
    );
END;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationId = N'001_create_tables')
BEGIN
    PRINT N'Migration 001_create_tables already applied; skipping.';
    COMMIT TRANSACTION;
    RETURN;
END;

-- Places: historic sites / landmarks keyed by Ref (e.g. NHL / inventory id)
IF OBJECT_ID(N'dbo.Places', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Places
    (
        Ref           VARCHAR(12)    NOT NULL,
        Name          NVARCHAR(256)  NOT NULL,
        Latitude      DECIMAL(9, 6)  NOT NULL,
        Longitude     DECIMAL(9, 6)  NOT NULL,
        AddressLine1  NVARCHAR(256)  NULL,
        AddressLine2  NVARCHAR(256)  NULL,
        City          NVARCHAR(128)  NULL,
        State         NVARCHAR(64)   NULL,
        PostalCode    NVARCHAR(20)   NULL,
        Country       NVARCHAR(64)   NULL CONSTRAINT DF_Places_Country DEFAULT (N'US'),
        PlaceType     NVARCHAR(64)   NULL,
        IsNhl         BIT            NOT NULL CONSTRAINT DF_Places_IsNhl DEFAULT (0),
        ListedDate    DATE           NULL,
        CreatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_Places_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_Places_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Places PRIMARY KEY CLUSTERED (Ref)
    );

    CREATE NONCLUSTERED INDEX IX_Places_Lat_Long
        ON dbo.Places (Latitude, Longitude)
        INCLUDE (Name, PlaceType, IsNhl);
END;

-- Summaries: one narrative row per place
IF OBJECT_ID(N'dbo.Summaries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Summaries
    (
        Ref            VARCHAR(12)    NOT NULL,
        SummaryText    NVARCHAR(MAX)  NOT NULL,
        SourceHeading  NVARCHAR(256)  NULL,
        Confidence     DECIMAL(4, 3)  NULL,
        SourceUrl      NVARCHAR(1024) NULL,
        ReviewNote     NVARCHAR(1024) NULL,
        CreatedAtUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_Summaries_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_Summaries_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Summaries PRIMARY KEY CLUSTERED (Ref),
        CONSTRAINT FK_Summaries_Places FOREIGN KEY (Ref)
            REFERENCES dbo.Places (Ref)
    );
END;

-- Photos: metadata only; binary lives in Azure Blob Storage
IF OBJECT_ID(N'dbo.Photos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Photos
    (
        PhotoId       INT            IDENTITY(1, 1) NOT NULL,
        Ref           VARCHAR(12)    NOT NULL,
        BlobPath      NVARCHAR(512)  NOT NULL,  -- relative path within container, e.g. places/REF/photo1.jpg
        Caption       NVARCHAR(512)  NULL,
        Source        NVARCHAR(256)  NULL,
        WidthPx       INT            NULL,
        HeightPx      INT            NULL,
        SortOrder     INT            NOT NULL CONSTRAINT DF_Photos_SortOrder DEFAULT (0),
        CreatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_Photos_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Photos PRIMARY KEY CLUSTERED (PhotoId),
        CONSTRAINT FK_Photos_Places FOREIGN KEY (Ref)
            REFERENCES dbo.Places (Ref)
    );

    CREATE NONCLUSTERED INDEX IX_Photos_Ref
        ON dbo.Photos (Ref)
        INCLUDE (BlobPath, Caption, SortOrder);
END;

-- ViewCounts: anonymous daily totals by place + app (no PII)
IF OBJECT_ID(N'dbo.ViewCounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ViewCounts
    (
        Ref           VARCHAR(12)   NOT NULL,
        ViewDay       DATE          NOT NULL,
        AppName       NVARCHAR(32)  NOT NULL,  -- e.g. web, android
        ViewTotal     INT           NOT NULL CONSTRAINT DF_ViewCounts_ViewTotal DEFAULT (0),
        UpdatedAtUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_ViewCounts_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ViewCounts PRIMARY KEY CLUSTERED (Ref, ViewDay, AppName),
        CONSTRAINT FK_ViewCounts_Places FOREIGN KEY (Ref)
            REFERENCES dbo.Places (Ref),
        CONSTRAINT CK_ViewCounts_ViewTotal CHECK (ViewTotal >= 0)
    );
END;

-- SummaryQueue: on-demand summary fill (one row per place)
IF OBJECT_ID(N'dbo.SummaryQueue', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SummaryQueue
    (
        Ref            VARCHAR(12)    NOT NULL,
        Status         NVARCHAR(32)   NOT NULL CONSTRAINT DF_SummaryQueue_Status DEFAULT (N'pending'),
        Priority       INT            NOT NULL CONSTRAINT DF_SummaryQueue_Priority DEFAULT (100),
        Attempts       INT            NOT NULL CONSTRAINT DF_SummaryQueue_Attempts DEFAULT (0),
        LastError      NVARCHAR(1024) NULL,
        EnqueuedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_SummaryQueue_EnqueuedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_SummaryQueue_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SummaryQueue PRIMARY KEY CLUSTERED (Ref),
        CONSTRAINT FK_SummaryQueue_Places FOREIGN KEY (Ref)
            REFERENCES dbo.Places (Ref),
        CONSTRAINT CK_SummaryQueue_Status CHECK (Status IN (N'pending', N'processing', N'done', N'failed', N'skipped'))
    );

    CREATE NONCLUSTERED INDEX IX_SummaryQueue_Status_Priority
        ON dbo.SummaryQueue (Status, Priority, EnqueuedAtUtc);
END;

INSERT INTO dbo.SchemaMigrations (MigrationId, Notes)
VALUES (N'001_create_tables', N'Initial Places, Summaries, Photos, ViewCounts, SummaryQueue');

COMMIT TRANSACTION;
PRINT N'Migration 001_create_tables applied successfully.';
GO
