IF OBJECT_ID('dbo.CoverageDefaultPolicies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CoverageDefaultPolicies
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DefaultRadiusMiles FLOAT NOT NULL,
        DefaultMaxTravelTimeMinutes INT NOT NULL
    );
END;

IF OBJECT_ID('dbo.CoverageRegionPolicies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CoverageRegionPolicies
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Region NVARCHAR(200) NOT NULL,
        RadiusMiles FLOAT NULL,
        MaxTravelTimeMinutes INT NULL
    );
    CREATE UNIQUE INDEX UX_CoverageRegionPolicies_Region ON dbo.CoverageRegionPolicies(Region);
END;

IF OBJECT_ID('dbo.CoverageAdviserPolicies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CoverageAdviserPolicies
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AdviserId NVARCHAR(100) NOT NULL,
        RadiusMiles FLOAT NULL,
        MaxTravelTimeMinutes INT NULL
    );
    CREATE UNIQUE INDEX UX_CoverageAdviserPolicies_AdviserId ON dbo.CoverageAdviserPolicies(AdviserId);
END;

IF OBJECT_ID('dbo.AvailabilityDefaultPolicies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AvailabilityDefaultPolicies
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DefaultTravelBufferMinutes INT NOT NULL,
        MaxTravelBufferMinutes INT NOT NULL,
        DefaultCompanyBufferMinutes INT NOT NULL,
        MaxCompanyBufferMinutes INT NOT NULL,
        PreviousClientProximityMinutes INT NOT NULL
    );
END;

IF OBJECT_ID('dbo.SearchAuditRecords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SearchAuditRecords
    (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        RequestId NVARCHAR(120) NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        RequestedStartUtc DATETIME2 NOT NULL,
        DurationMinutes INT NOT NULL,
        SearchHorizonMinutes INT NOT NULL,
        DestinationPostcode NVARCHAR(24) NULL,
        RegionsCsv NVARCHAR(1000) NULL,
        CandidatesReturned INT NOT NULL,
        SelectedAdviserId NVARCHAR(100) NULL,
        SelectedAdviserRating FLOAT NULL,
        SelectedAdviserGoldStar BIT NULL,
        SelectedTravelMinutes INT NULL,
        SelectedMaxTravelTimeMinutes INT NULL,
        SelectedCompanyBufferMinutes INT NULL,
        SelectedTravelBufferMinutes INT NULL,
        SelectedOriginSource NVARCHAR(40) NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL
    );

    CREATE INDEX IX_SearchAuditRecords_CreatedUtc ON dbo.SearchAuditRecords(CreatedUtc);
    CREATE INDEX IX_SearchAuditRecords_RequestId ON dbo.SearchAuditRecords(RequestId);
END;

IF OBJECT_ID('dbo.IntegrationOperationAudit', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.IntegrationOperationAudit
    (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        ServiceName NVARCHAR(64) NOT NULL,
        FunctionName NVARCHAR(256) NOT NULL,
        Method NVARCHAR(16) NOT NULL,
        Path NVARCHAR(512) NOT NULL,
        QueryString NVARCHAR(2048) NULL,
        CorrelationId NVARCHAR(128) NULL,
        OperationId NVARCHAR(128) NOT NULL,
        StatusCode INT NOT NULL,
        DurationMs BIGINT NOT NULL,
        ErrorType NVARCHAR(128) NULL,
        ErrorMessage NVARCHAR(2048) NULL,
        CreatedUtc DATETIME2 NOT NULL
    );

    CREATE INDEX IX_IntegrationOperationAudit_CreatedUtc ON dbo.IntegrationOperationAudit(CreatedUtc);
    CREATE INDEX IX_IntegrationOperationAudit_CorrelationId ON dbo.IntegrationOperationAudit(CorrelationId);
    CREATE INDEX IX_IntegrationOperationAudit_FunctionName_CreatedUtc ON dbo.IntegrationOperationAudit(FunctionName, CreatedUtc);
END;
