-- TeknikResimOlcum SQL Server şema dosyası
-- Ana arama/indeks alanları NVARCHAR(450), diğer alanlar NVARCHAR(MAX) tutulur; mevcut CSV yapısıyla uyumludur.

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Users] (
    [Username] NVARCHAR(450) NULL,
    [PasswordHash] NVARCHAR(MAX) NULL,
    [PasswordSalt] NVARCHAR(MAX) NULL,
    [Role] NVARCHAR(MAX) NULL,
    [IsActive] NVARCHAR(MAX) NULL,
    [ShowOnLogin] NVARCHAR(MAX) NULL,
    [IsPermissionTestAccount] NVARCHAR(MAX) NULL,
    [MustChangePassword] NVARCHAR(MAX) NULL,
    [PasswordChangedAt] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL
);
END
GO

IF COL_LENGTH('dbo.Users', 'Username') IS NULL ALTER TABLE dbo.[Users] ADD [Username] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.Users', 'PasswordHash') IS NULL ALTER TABLE dbo.[Users] ADD [PasswordHash] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Users', 'PasswordSalt') IS NULL ALTER TABLE dbo.[Users] ADD [PasswordSalt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Users', 'MustChangePassword') IS NULL ALTER TABLE dbo.[Users] ADD [MustChangePassword] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Users', 'PasswordChangedAt') IS NULL ALTER TABLE dbo.[Users] ADD [PasswordChangedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Users', 'Role') IS NULL ALTER TABLE dbo.[Users] ADD [Role] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Users', 'IsActive') IS NULL ALTER TABLE dbo.[Users] ADD [IsActive] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Users', 'ShowOnLogin') IS NULL ALTER TABLE dbo.[Users] ADD [ShowOnLogin] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Users', 'IsPermissionTestAccount') IS NULL ALTER TABLE dbo.[Users] ADD [IsPermissionTestAccount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Users', 'CreatedAt') IS NULL ALTER TABLE dbo.[Users] ADD [CreatedAt] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.ActiveSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ActiveSessions] (
    [SessionId] NVARCHAR(MAX) NULL,
    [Username] NVARCHAR(450) NULL,
    [ComputerName] NVARCHAR(MAX) NULL,
    [LoginAt] NVARCHAR(MAX) NULL,
    [LastSeen] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.ActiveSessions', 'SessionId') IS NULL ALTER TABLE dbo.[ActiveSessions] ADD [SessionId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ActiveSessions', 'Username') IS NULL ALTER TABLE dbo.[ActiveSessions] ADD [Username] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ActiveSessions', 'ComputerName') IS NULL ALTER TABLE dbo.[ActiveSessions] ADD [ComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ActiveSessions', 'LoginAt') IS NULL ALTER TABLE dbo.[ActiveSessions] ADD [LoginAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ActiveSessions', 'LastSeen') IS NULL ALTER TABLE dbo.[ActiveSessions] ADD [LastSeen] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Products] (
    [TrCode] NVARCHAR(450) NULL,
    [ProductName] NVARCHAR(MAX) NULL,
    [PlasticCode] NVARCHAR(MAX) NULL,
    [Material] NVARCHAR(MAX) NULL,
    [ColorName] NVARCHAR(MAX) NULL,
    [MoldCavityCount] NVARCHAR(MAX) NULL,
    [MoldCode] NVARCHAR(MAX) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [DrawingFile] NVARCHAR(MAX) NULL,
    [DrawingScope] NVARCHAR(MAX) NULL,
    [IsActive] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.Products', 'TrCode') IS NULL ALTER TABLE dbo.[Products] ADD [TrCode] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.Products', 'ProductName') IS NULL ALTER TABLE dbo.[Products] ADD [ProductName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'PlasticCode') IS NULL ALTER TABLE dbo.[Products] ADD [PlasticCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'Material') IS NULL ALTER TABLE dbo.[Products] ADD [Material] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'ColorName') IS NULL ALTER TABLE dbo.[Products] ADD [ColorName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'MoldCavityCount') IS NULL ALTER TABLE dbo.[Products] ADD [MoldCavityCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'MoldCode') IS NULL ALTER TABLE dbo.[Products] ADD [MoldCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'DrawingRev') IS NULL ALTER TABLE dbo.[Products] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'DrawingFile') IS NULL ALTER TABLE dbo.[Products] ADD [DrawingFile] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'DrawingScope') IS NULL ALTER TABLE dbo.[Products] ADD [DrawingScope] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'IsActive') IS NULL ALTER TABLE dbo.[Products] ADD [IsActive] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'CreatedBy') IS NULL ALTER TABLE dbo.[Products] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Products', 'CreatedAt') IS NULL ALTER TABLE dbo.[Products] ADD [CreatedAt] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.ControlPoints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ControlPoints] (
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [DrawingScope] NVARCHAR(MAX) NULL,
    [MeasureId] NVARCHAR(MAX) NULL,
    [MeasureName] NVARCHAR(MAX) NULL,
    [Nominal] NVARCHAR(MAX) NULL,
    [LowerTol] NVARCHAR(MAX) NULL,
    [UpperTol] NVARCHAR(MAX) NULL,
    [LowerLimit] NVARCHAR(MAX) NULL,
    [UpperLimit] NVARCHAR(MAX) NULL,
    [PageNo] NVARCHAR(MAX) NULL,
    [XPercent] NVARCHAR(MAX) NULL,
    [YPercent] NVARCHAR(MAX) NULL,
    [Unit] NVARCHAR(MAX) NULL,
    [IsMandatory] NVARCHAR(MAX) NULL,
    [MeasurementGroup] NVARCHAR(MAX) NULL,
    [SampleFrequency] NVARCHAR(MAX) NULL,
    [IsCritical] NVARCHAR(MAX) NULL,
    [SortNo] NVARCHAR(MAX) NULL,
    [IsActive] NVARCHAR(MAX) NULL,
    [SpcKey] NVARCHAR(MAX) NULL,
    [MeasureVersion] NVARCHAR(MAX) NULL,
    [ValidFrom] NVARCHAR(MAX) NULL,
    [ValidTo] NVARCHAR(MAX) NULL,
    [ChangeReason] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.ControlPoints', 'TrCode') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [TrCode] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'DrawingRev') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'DrawingScope') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [DrawingScope] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'MeasureId') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [MeasureId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'MeasureName') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [MeasureName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'Nominal') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [Nominal] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'LowerTol') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [LowerTol] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'UpperTol') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [UpperTol] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'LowerLimit') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [LowerLimit] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'UpperLimit') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [UpperLimit] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'PageNo') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [PageNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'XPercent') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [XPercent] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'YPercent') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [YPercent] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'Unit') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [Unit] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'IsMandatory') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [IsMandatory] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'MeasurementGroup') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [MeasurementGroup] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'SampleFrequency') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [SampleFrequency] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'IsCritical') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [IsCritical] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'SortNo') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [SortNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'IsActive') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [IsActive] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'SpcKey') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [SpcKey] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'MeasureVersion') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [MeasureVersion] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'ValidFrom') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [ValidFrom] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'ValidTo') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [ValidTo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ControlPoints', 'ChangeReason') IS NULL ALTER TABLE dbo.[ControlPoints] ADD [ChangeReason] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.MeasurementGroupAreas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[MeasurementGroupAreas] (
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [DrawingScope] NVARCHAR(MAX) NULL,
    [GroupName] NVARCHAR(450) NULL,
    [PageNo] NVARCHAR(MAX) NULL,
    [LeftPercent] NVARCHAR(MAX) NULL,
    [TopPercent] NVARCHAR(MAX) NULL,
    [RightPercent] NVARCHAR(MAX) NULL,
    [BottomPercent] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.MeasurementGroupAreas', 'TrCode') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [TrCode] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'DrawingRev') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'DrawingScope') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [DrawingScope] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'GroupName') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [GroupName] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'PageNo') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [PageNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'LeftPercent') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [LeftPercent] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'TopPercent') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [TopPercent] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'RightPercent') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [RightPercent] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'BottomPercent') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [BottomPercent] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'UpdatedBy') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementGroupAreas', 'UpdatedAt') IS NULL ALTER TABLE dbo.[MeasurementGroupAreas] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.MeasurementRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[MeasurementRecords] (
    [RecordId] NVARCHAR(450) NULL,
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [DrawingScope] NVARCHAR(MAX) NULL,
    [LotNo] NVARCHAR(MAX) NULL,
    [SerialNo] NVARCHAR(MAX) NULL,
    [EyeCount] NVARCHAR(MAX) NULL,
    [EyeNo] NVARCHAR(MAX) NULL,
    [OperatorName] NVARCHAR(MAX) NULL,
    [ComputerName] NVARCHAR(MAX) NULL,
    [MeasurementDate] NVARCHAR(MAX) NULL,
    [MeasureId] NVARCHAR(MAX) NULL,
    [MeasureName] NVARCHAR(MAX) NULL,
    [MeasurementGroup] NVARCHAR(MAX) NULL,
    [SampleFrequency] NVARCHAR(MAX) NULL,
    [IsCritical] NVARCHAR(MAX) NULL,
    [SortNo] NVARCHAR(MAX) NULL,
    [Nominal] NVARCHAR(MAX) NULL,
    [LowerLimit] NVARCHAR(MAX) NULL,
    [UpperLimit] NVARCHAR(MAX) NULL,
    [PageNo] NVARCHAR(MAX) NULL,
    [XPercent] NVARCHAR(MAX) NULL,
    [YPercent] NVARCHAR(MAX) NULL,
    [MeasuredValue] NVARCHAR(MAX) NULL,
    [Result] NVARCHAR(MAX) NULL,
    [Note] NVARCHAR(MAX) NULL,
    [ProductionTicketId] NVARCHAR(MAX) NULL,
    [SpcKey] NVARCHAR(MAX) NULL,
    [MeasureVersion] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.MeasurementRecords', 'RecordId') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [RecordId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'TrCode') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [TrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'DrawingRev') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'DrawingScope') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [DrawingScope] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'LotNo') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [LotNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'SerialNo') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [SerialNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'EyeCount') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [EyeCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'EyeNo') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [EyeNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'OperatorName') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [OperatorName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'ComputerName') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [ComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'MeasurementDate') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [MeasurementDate] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'MeasureId') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [MeasureId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'MeasureName') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [MeasureName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'MeasurementGroup') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [MeasurementGroup] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'SampleFrequency') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [SampleFrequency] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'IsCritical') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [IsCritical] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'SortNo') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [SortNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'Nominal') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [Nominal] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'LowerLimit') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [LowerLimit] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'UpperLimit') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [UpperLimit] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'PageNo') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [PageNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'XPercent') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [XPercent] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'YPercent') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [YPercent] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'MeasuredValue') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [MeasuredValue] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'Result') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [Result] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'Note') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [Note] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'ProductionTicketId') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [ProductionTicketId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'SpcKey') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [SpcKey] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MeasurementRecords', 'MeasureVersion') IS NULL ALTER TABLE dbo.[MeasurementRecords] ADD [MeasureVersion] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.VisualControlRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[VisualControlRecords] (
    [RecordId] NVARCHAR(450) NULL,
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [DrawingScope] NVARCHAR(MAX) NULL,
    [LotNo] NVARCHAR(MAX) NULL,
    [SerialNo] NVARCHAR(MAX) NULL,
    [EyeCount] NVARCHAR(MAX) NULL,
    [EyeNo] NVARCHAR(MAX) NULL,
    [OperatorName] NVARCHAR(MAX) NULL,
    [ComputerName] NVARCHAR(MAX) NULL,
    [ControlDate] NVARCHAR(MAX) NULL,
    [ControlName] NVARCHAR(MAX) NULL,
    [IsSelected] NVARCHAR(MAX) NULL,
    [Result] NVARCHAR(MAX) NULL,
    [Note] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.VisualControlRecords', 'RecordId') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [RecordId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'TrCode') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [TrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'DrawingRev') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'DrawingScope') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [DrawingScope] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'LotNo') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [LotNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'SerialNo') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [SerialNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'EyeCount') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [EyeCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'EyeNo') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [EyeNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'OperatorName') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [OperatorName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'ComputerName') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [ComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'ControlDate') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [ControlDate] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'ControlName') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [ControlName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'IsSelected') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [IsSelected] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'Result') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [Result] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.VisualControlRecords', 'Note') IS NULL ALTER TABLE dbo.[VisualControlRecords] ADD [Note] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.ClosedEyeRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ClosedEyeRecords] (
    [RecordId] NVARCHAR(450) NULL,
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [DrawingScope] NVARCHAR(MAX) NULL,
    [LotNo] NVARCHAR(MAX) NULL,
    [SerialNo] NVARCHAR(MAX) NULL,
    [EyeCount] NVARCHAR(MAX) NULL,
    [EyeNo] NVARCHAR(MAX) NULL,
    [OperatorName] NVARCHAR(MAX) NULL,
    [ComputerName] NVARCHAR(MAX) NULL,
    [ClosedDate] NVARCHAR(MAX) NULL,
    [Reason] NVARCHAR(MAX) NULL,
    [ProductionTicketId] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.ClosedEyeRecords', 'RecordId') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [RecordId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'TrCode') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [TrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'DrawingRev') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'DrawingScope') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [DrawingScope] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'LotNo') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [LotNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'SerialNo') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [SerialNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'EyeCount') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [EyeCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'EyeNo') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [EyeNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'OperatorName') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [OperatorName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'ComputerName') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [ComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'ClosedDate') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [ClosedDate] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'Reason') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [Reason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ClosedEyeRecords', 'ProductionTicketId') IS NULL ALTER TABLE dbo.[ClosedEyeRecords] ADD [ProductionTicketId] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[AuditLog] (
    [LogId] NVARCHAR(MAX) NULL,
    [DateTime] NVARCHAR(MAX) NULL,
    [UserName] NVARCHAR(MAX) NULL,
    [Role] NVARCHAR(MAX) NULL,
    [ComputerName] NVARCHAR(MAX) NULL,
    [Action] NVARCHAR(MAX) NULL,
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [Detail] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.AuditLog', 'LogId') IS NULL ALTER TABLE dbo.[AuditLog] ADD [LogId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuditLog', 'DateTime') IS NULL ALTER TABLE dbo.[AuditLog] ADD [DateTime] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuditLog', 'UserName') IS NULL ALTER TABLE dbo.[AuditLog] ADD [UserName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuditLog', 'Role') IS NULL ALTER TABLE dbo.[AuditLog] ADD [Role] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuditLog', 'ComputerName') IS NULL ALTER TABLE dbo.[AuditLog] ADD [ComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuditLog', 'Action') IS NULL ALTER TABLE dbo.[AuditLog] ADD [Action] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuditLog', 'TrCode') IS NULL ALTER TABLE dbo.[AuditLog] ADD [TrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuditLog', 'DrawingRev') IS NULL ALTER TABLE dbo.[AuditLog] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.AuditLog', 'Detail') IS NULL ALTER TABLE dbo.[AuditLog] ADD [Detail] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.ProductionTickets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[ProductionTickets] (
    [TicketId] NVARCHAR(450) NULL,
    [Status] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [ComputerName] NVARCHAR(MAX) NULL,
    [MachineNo] NVARCHAR(MAX) NULL,
    [PreviousMachineNo] NVARCHAR(MAX) NULL,
    [MoldCode] NVARCHAR(MAX) NULL,
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [ProductName] NVARCHAR(MAX) NULL,
    [Material] NVARCHAR(MAX) NULL,
    [ColorName] NVARCHAR(MAX) NULL,
    [PlasticCode] NVARCHAR(MAX) NULL,
    [RawMaterial] NVARCHAR(MAX) NULL,
    [WorkOrderNo] NVARCHAR(MAX) NULL,
    [Note] NVARCHAR(MAX) NULL,
    [SeenByQuality] NVARCHAR(MAX) NULL,
    [SeenAt] NVARCHAR(MAX) NULL,
    [ClosedBy] NVARCHAR(MAX) NULL,
    [ClosedAt] NVARCHAR(MAX) NULL,
    [CloseNote] NVARCHAR(MAX) NULL,
    [BindingId] NVARCHAR(450) NULL,
    [BindingStartAt] NVARCHAR(MAX) NULL,
    [BindingEndAt] NVARCHAR(MAX) NULL,
    [BindingDurationMin] NVARCHAR(MAX) NULL,
    [BindingReason] NVARCHAR(MAX) NULL,
    [MachineChangeReason] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.ProductionTickets', 'TicketId') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [TicketId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'Status') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'CreatedAt') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'CreatedBy') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'ComputerName') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [ComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'MachineNo') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [MachineNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'PreviousMachineNo') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [PreviousMachineNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'MoldCode') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [MoldCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'TrCode') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [TrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'DrawingRev') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'ProductName') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [ProductName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'Material') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [Material] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'ColorName') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [ColorName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'PlasticCode') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [PlasticCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'RawMaterial') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [RawMaterial] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'WorkOrderNo') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [WorkOrderNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'Note') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [Note] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'SeenByQuality') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [SeenByQuality] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'SeenAt') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [SeenAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'ClosedBy') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [ClosedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'ClosedAt') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [ClosedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'CloseNote') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [CloseNote] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'BindingId') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [BindingId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'BindingStartAt') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [BindingStartAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'BindingEndAt') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [BindingEndAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'BindingDurationMin') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [BindingDurationMin] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'BindingReason') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [BindingReason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ProductionTickets', 'MachineChangeReason') IS NULL ALTER TABLE dbo.[ProductionTickets] ADD [MachineChangeReason] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.MoldBindingRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[MoldBindingRecords] (
    [BindingId] NVARCHAR(450) NULL,
    [Status] NVARCHAR(MAX) NULL,
    [StartedAt] NVARCHAR(MAX) NULL,
    [StartedBy] NVARCHAR(MAX) NULL,
    [StartComputerName] NVARCHAR(MAX) NULL,
    [CompletedAt] NVARCHAR(MAX) NULL,
    [CompletedBy] NVARCHAR(MAX) NULL,
    [CompletedComputerName] NVARCHAR(MAX) NULL,
    [MachineNo] NVARCHAR(MAX) NULL,
    [PreviousMachineNo] NVARCHAR(MAX) NULL,
    [MoldCode] NVARCHAR(MAX) NULL,
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [ProductName] NVARCHAR(MAX) NULL,
    [Material] NVARCHAR(MAX) NULL,
    [ColorName] NVARCHAR(MAX) NULL,
    [PlasticCode] NVARCHAR(MAX) NULL,
    [RawMaterial] NVARCHAR(MAX) NULL,
    [WorkOrderNo] NVARCHAR(MAX) NULL,
    [BindingReason] NVARCHAR(MAX) NULL,
    [MachineChangeReason] NVARCHAR(MAX) NULL,
    [StartNote] NVARCHAR(MAX) NULL,
    [FinishNote] NVARCHAR(MAX) NULL,
    [Note] NVARCHAR(MAX) NULL,
    [BindingDurationMin] NVARCHAR(MAX) NULL,
    [ProductionTicketId] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.MoldBindingRecords', 'BindingId') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [BindingId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'Status') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'StartedAt') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [StartedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'StartedBy') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [StartedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'StartComputerName') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [StartComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'CompletedAt') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [CompletedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'CompletedBy') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [CompletedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'CompletedComputerName') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [CompletedComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'MachineNo') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [MachineNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'PreviousMachineNo') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [PreviousMachineNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'MoldCode') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [MoldCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'TrCode') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [TrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'DrawingRev') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'ProductName') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [ProductName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'Material') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [Material] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'ColorName') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [ColorName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'PlasticCode') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [PlasticCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'RawMaterial') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [RawMaterial] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'WorkOrderNo') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [WorkOrderNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'BindingReason') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [BindingReason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'MachineChangeReason') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [MachineChangeReason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'StartNote') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [StartNote] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'FinishNote') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [FinishNote] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'Note') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [Note] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'BindingDurationMin') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [BindingDurationMin] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldBindingRecords', 'ProductionTicketId') IS NULL ALTER TABLE dbo.[MoldBindingRecords] ADD [ProductionTicketId] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.MoldTickets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[MoldTickets] (
    [MoldTicketId] NVARCHAR(450) NULL,
    [Status] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [ComputerName] NVARCHAR(MAX) NULL,
    [MoldCode] NVARCHAR(MAX) NULL,
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [ProductName] NVARCHAR(MAX) NULL,
    [Severity] NVARCHAR(MAX) NULL,
    [ProblemType] NVARCHAR(MAX) NULL,
    [ProblemDescription] NVARCHAR(MAX) NULL,
    [ActionPlan] NVARCHAR(MAX) NULL,
    [SourcePlasticShiftRecordId] NVARCHAR(450) NULL,
    [ClosedBy] NVARCHAR(MAX) NULL,
    [ClosedAt] NVARCHAR(MAX) NULL,
    [CloseNote] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.MoldTickets', 'MoldTicketId') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [MoldTicketId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'Status') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'CreatedAt') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'CreatedBy') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'ComputerName') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [ComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'MoldCode') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [MoldCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'TrCode') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [TrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'DrawingRev') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'ProductName') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [ProductName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'Severity') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [Severity] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'ProblemType') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [ProblemType] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'ProblemDescription') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [ProblemDescription] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'ActionPlan') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [ActionPlan] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'SourcePlasticShiftRecordId') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [SourcePlasticShiftRecordId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'ClosedBy') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [ClosedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'ClosedAt') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [ClosedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldTickets', 'CloseNote') IS NULL ALTER TABLE dbo.[MoldTickets] ADD [CloseNote] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.QualityToProductionTickets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[QualityToProductionTickets] (
    [TicketId] NVARCHAR(450) NULL,
    [Status] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [ComputerName] NVARCHAR(MAX) NULL,
    [TrCode] NVARCHAR(450) NULL,
    [DrawingRev] NVARCHAR(MAX) NULL,
    [ProductName] NVARCHAR(MAX) NULL,
    [LotNo] NVARCHAR(MAX) NULL,
    [SerialNo] NVARCHAR(MAX) NULL,
    [EyeCount] NVARCHAR(MAX) NULL,
    [EyeNo] NVARCHAR(MAX) NULL,
    [RecordId] NVARCHAR(450) NULL,
    [SourceQualityTicketId] NVARCHAR(MAX) NULL,
    [SourceType] NVARCHAR(MAX) NULL,
    [IssueSummary] NVARCHAR(MAX) NULL,
    [MeasurementNokCount] NVARCHAR(MAX) NULL,
    [VisualNokCount] NVARCHAR(MAX) NULL,
    [SeenByProduction] NVARCHAR(MAX) NULL,
    [SeenAt] NVARCHAR(MAX) NULL,
    [ClosedBy] NVARCHAR(MAX) NULL,
    [ClosedAt] NVARCHAR(MAX) NULL,
    [CloseNote] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.QualityToProductionTickets', 'TicketId') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [TicketId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'Status') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'CreatedAt') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'CreatedBy') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'ComputerName') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [ComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'TrCode') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [TrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'DrawingRev') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [DrawingRev] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'ProductName') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [ProductName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'LotNo') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [LotNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'SerialNo') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [SerialNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'EyeCount') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [EyeCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'EyeNo') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [EyeNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'RecordId') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [RecordId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'SourceQualityTicketId') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [SourceQualityTicketId] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'SourceType') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [SourceType] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'IssueSummary') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [IssueSummary] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'MeasurementNokCount') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [MeasurementNokCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'VisualNokCount') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [VisualNokCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'SeenByProduction') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [SeenByProduction] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'SeenAt') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [SeenAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'ClosedBy') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [ClosedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'ClosedAt') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [ClosedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.QualityToProductionTickets', 'CloseNote') IS NULL ALTER TABLE dbo.[QualityToProductionTickets] ADD [CloseNote] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.MoldConnectionPlan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[MoldConnectionPlan] (
    [PlanId] NVARCHAR(450) NULL,
    [ImportedAt] NVARCHAR(MAX) NULL,
    [ImportedBy] NVARCHAR(MAX) NULL,
    [SourceFile] NVARCHAR(MAX) NULL,
    [SourceSheet] NVARCHAR(MAX) NULL,
    [SourceRow] NVARCHAR(MAX) NULL,
    [MachineName] NVARCHAR(MAX) NULL,
    [MachineNo] NVARCHAR(MAX) NULL,
    [RunningMolds] NVARCHAR(MAX) NULL,
    [CurrentMoldNo] NVARCHAR(MAX) NULL,
    [CurrentMoldRackNo] NVARCHAR(MAX) NULL,
    [CurrentPlasticCode] NVARCHAR(MAX) NULL,
    [CurrentTrCode] NVARCHAR(MAX) NULL,
    [FirstMoldNo] NVARCHAR(MAX) NULL,
    [FirstMoldRackNo] NVARCHAR(MAX) NULL,
    [FirstPlasticCode] NVARCHAR(MAX) NULL,
    [FirstTrCode] NVARCHAR(MAX) NULL,
    [SecondMoldNo] NVARCHAR(MAX) NULL,
    [SecondMoldRackNo] NVARCHAR(MAX) NULL,
    [SecondPlasticCode] NVARCHAR(MAX) NULL,
    [SecondTrCode] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.MoldConnectionPlan', 'PlanId') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [PlanId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'ImportedAt') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [ImportedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'ImportedBy') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [ImportedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'SourceFile') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [SourceFile] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'SourceSheet') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [SourceSheet] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'SourceRow') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [SourceRow] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'MachineName') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [MachineName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'MachineNo') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [MachineNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'RunningMolds') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [RunningMolds] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'CurrentMoldNo') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [CurrentMoldNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'CurrentMoldRackNo') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [CurrentMoldRackNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'CurrentPlasticCode') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [CurrentPlasticCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'CurrentTrCode') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [CurrentTrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'FirstMoldNo') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [FirstMoldNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'FirstMoldRackNo') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [FirstMoldRackNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'FirstPlasticCode') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [FirstPlasticCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'FirstTrCode') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [FirstTrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'SecondMoldNo') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [SecondMoldNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'SecondMoldRackNo') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [SecondMoldRackNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'SecondPlasticCode') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [SecondPlasticCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MoldConnectionPlan', 'SecondTrCode') IS NULL ALTER TABLE dbo.[MoldConnectionPlan] ADD [SecondTrCode] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.MechanismQualityControlRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[MechanismQualityControlRecords] (
    [ControlId] NVARCHAR(450) NULL,
    [Status] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [ControlDateTime] NVARCHAR(MAX) NULL,
    [IncomingEyeCount] NVARCHAR(MAX) NULL,
    [DeliveredBy] NVARCHAR(MAX) NULL,
    [ProductNameCode] NVARCHAR(MAX) NULL,
    [MountedMechanismCounter] NVARCHAR(MAX) NULL,
    [Explanation] NVARCHAR(MAX) NULL,
    [DeliveryExplanation] NVARCHAR(MAX) NULL,
    [ControlExplanation] NVARCHAR(MAX) NULL,
    [IsSuitable] NVARCHAR(MAX) NULL,
    [IsNotSuitable] NVARCHAR(MAX) NULL,
    [ControlledBy] NVARCHAR(MAX) NULL,
    [ControlledAt] NVARCHAR(MAX) NULL,
    [CreatedComputerName] NVARCHAR(MAX) NULL,
    [ControlledComputerName] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'ControlId') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [ControlId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'Status') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'CreatedAt') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'ControlDateTime') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [ControlDateTime] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'IncomingEyeCount') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [IncomingEyeCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'DeliveredBy') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [DeliveredBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'ProductNameCode') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [ProductNameCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'MountedMechanismCounter') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [MountedMechanismCounter] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'Explanation') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [Explanation] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'DeliveryExplanation') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [DeliveryExplanation] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'ControlExplanation') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [ControlExplanation] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'IsSuitable') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [IsSuitable] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'IsNotSuitable') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [IsNotSuitable] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'ControlledBy') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [ControlledBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'ControlledAt') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [ControlledAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'CreatedComputerName') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [CreatedComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.MechanismQualityControlRecords', 'ControlledComputerName') IS NULL ALTER TABLE dbo.[MechanismQualityControlRecords] ADD [ControlledComputerName] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.PlasticShiftTrackingRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[PlasticShiftTrackingRecords] (
    [RecordId] NVARCHAR(450) NULL,
    [OccurredAt] NVARCHAR(MAX) NULL,
    [DefectiveQuantity] NVARCHAR(MAX) NULL,
    [Responsible] NVARCHAR(MAX) NULL,
    [ProductNameCode] NVARCHAR(MAX) NULL,
    [Problem] NVARCHAR(MAX) NULL,
    [ActionTaken] NVARCHAR(MAX) NULL,
    [YellowCard] NVARCHAR(MAX) NULL,
    [MoldModification] NVARCHAR(MAX) NULL,
    [ErrorReport] NVARCHAR(MAX) NULL,
    [TestPerformed] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL,
    [ComputerName] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'RecordId') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [RecordId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'OccurredAt') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [OccurredAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'DefectiveQuantity') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [DefectiveQuantity] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'Responsible') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [Responsible] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'ProductNameCode') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [ProductNameCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'Problem') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [Problem] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'ActionTaken') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [ActionTaken] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'YellowCard') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [YellowCard] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'MoldModification') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [MoldModification] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'ErrorReport') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [ErrorReport] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'TestPerformed') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [TestPerformed] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'CreatedBy') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'CreatedAt') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'UpdatedBy') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'UpdatedAt') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftTrackingRecords', 'ComputerName') IS NULL ALTER TABLE dbo.[PlasticShiftTrackingRecords] ADD [ComputerName] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.PlasticShiftEmailRecipients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[PlasticShiftEmailRecipients] (
    [Email] NVARCHAR(450) NULL,
    [DisplayName] NVARCHAR(MAX) NULL,
    [RecipientType] NVARCHAR(MAX) NULL,
    [IsActive] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.PlasticShiftEmailRecipients', 'Email') IS NULL ALTER TABLE dbo.[PlasticShiftEmailRecipients] ADD [Email] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.PlasticShiftEmailRecipients', 'DisplayName') IS NULL ALTER TABLE dbo.[PlasticShiftEmailRecipients] ADD [DisplayName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftEmailRecipients', 'RecipientType') IS NULL ALTER TABLE dbo.[PlasticShiftEmailRecipients] ADD [RecipientType] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftEmailRecipients', 'IsActive') IS NULL ALTER TABLE dbo.[PlasticShiftEmailRecipients] ADD [IsActive] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftEmailRecipients', 'CreatedBy') IS NULL ALTER TABLE dbo.[PlasticShiftEmailRecipients] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftEmailRecipients', 'CreatedAt') IS NULL ALTER TABLE dbo.[PlasticShiftEmailRecipients] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftEmailRecipients', 'UpdatedBy') IS NULL ALTER TABLE dbo.[PlasticShiftEmailRecipients] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PlasticShiftEmailRecipients', 'UpdatedAt') IS NULL ALTER TABLE dbo.[PlasticShiftEmailRecipients] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.TestRequestEmailRecipients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[TestRequestEmailRecipients] (
    [Email] NVARCHAR(450) NULL,
    [DisplayName] NVARCHAR(MAX) NULL,
    [IsActive] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.TestRequestEmailRecipients', 'Email') IS NULL ALTER TABLE dbo.[TestRequestEmailRecipients] ADD [Email] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.TestRequestEmailRecipients', 'DisplayName') IS NULL ALTER TABLE dbo.[TestRequestEmailRecipients] ADD [DisplayName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestEmailRecipients', 'IsActive') IS NULL ALTER TABLE dbo.[TestRequestEmailRecipients] ADD [IsActive] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestEmailRecipients', 'CreatedBy') IS NULL ALTER TABLE dbo.[TestRequestEmailRecipients] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestEmailRecipients', 'CreatedAt') IS NULL ALTER TABLE dbo.[TestRequestEmailRecipients] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestEmailRecipients', 'UpdatedBy') IS NULL ALTER TABLE dbo.[TestRequestEmailRecipients] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestEmailRecipients', 'UpdatedAt') IS NULL ALTER TABLE dbo.[TestRequestEmailRecipients] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.TestRequestRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[TestRequestRecords] (
    [RequestId] NVARCHAR(450) NULL,
    [Status] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [CreatedComputerName] NVARCHAR(MAX) NULL,
    [RequestingDepartment] NVARCHAR(MAX) NULL,
    [RequestedDepartment] NVARCHAR(MAX) NULL,
    [RequestReason] NVARCHAR(MAX) NULL,
    [ProductNameTrCode] NVARCHAR(MAX) NULL,
    [RequestedTests] NVARCHAR(MAX) NULL,
    [SampleQuantity] NVARCHAR(MAX) NULL,
    [Priority] NVARCHAR(MAX) NULL,
    [DueDate] NVARCHAR(MAX) NULL,
    [RequesterReportNo] NVARCHAR(MAX) NULL,
    [RequesterExplanation] NVARCHAR(MAX) NULL,
    [AcceptedAt] NVARCHAR(MAX) NULL,
    [AcceptedBy] NVARCHAR(MAX) NULL,
    [CompletedAt] NVARCHAR(MAX) NULL,
    [CompletedBy] NVARCHAR(MAX) NULL,
    [LabReportNo] NVARCHAR(MAX) NULL,
    [Result] NVARCHAR(MAX) NULL,
    [LabExplanation] NVARCHAR(MAX) NULL,
    [CancelledAt] NVARCHAR(MAX) NULL,
    [CancelledBy] NVARCHAR(MAX) NULL,
    [CancelReason] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.TestRequestRecords', 'RequestId') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [RequestId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'Status') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'CreatedAt') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'CreatedBy') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'CreatedComputerName') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [CreatedComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'RequestingDepartment') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [RequestingDepartment] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'RequestedDepartment') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [RequestedDepartment] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'RequestReason') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [RequestReason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'ProductNameTrCode') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [ProductNameTrCode] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'RequestedTests') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [RequestedTests] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'SampleQuantity') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [SampleQuantity] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'Priority') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [Priority] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'DueDate') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [DueDate] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'RequesterReportNo') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [RequesterReportNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'RequesterExplanation') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [RequesterExplanation] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'AcceptedAt') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [AcceptedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'AcceptedBy') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [AcceptedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'CompletedAt') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [CompletedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'CompletedBy') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [CompletedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'LabReportNo') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [LabReportNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'Result') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [Result] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'LabExplanation') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [LabExplanation] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'CancelledAt') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [CancelledAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'CancelledBy') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [CancelledBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'CancelReason') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [CancelReason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'UpdatedAt') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestRecords', 'UpdatedBy') IS NULL ALTER TABLE dbo.[TestRequestRecords] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.TestRequestSteps', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[TestRequestSteps] (
    [RequestId] NVARCHAR(450) NULL,
    [StepId] NVARCHAR(450) NULL,
    [SortNo] NVARCHAR(MAX) NULL,
    [TestName] NVARCHAR(MAX) NULL,
    [TestDescription] NVARCHAR(MAX) NULL,
    [Status] NVARCHAR(MAX) NULL,
    [Result] NVARCHAR(MAX) NULL,
    [Explanation] NVARCHAR(MAX) NULL,
    [CompletedAt] NVARCHAR(MAX) NULL,
    [CompletedBy] NVARCHAR(MAX) NULL,
    [CompletedComputerName] NVARCHAR(MAX) NULL,
    [SkippedAt] NVARCHAR(MAX) NULL,
    [SkippedBy] NVARCHAR(MAX) NULL,
    [SkipReason] NVARCHAR(MAX) NULL,
    [ReopenedAt] NVARCHAR(MAX) NULL,
    [ReopenedBy] NVARCHAR(MAX) NULL,
    [ReopenReason] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.TestRequestSteps', 'RequestId') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [RequestId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'StepId') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [StepId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'SortNo') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [SortNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'TestName') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [TestName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'TestDescription') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [TestDescription] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'Status') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'Result') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [Result] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'Explanation') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [Explanation] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'CompletedAt') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [CompletedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'CompletedBy') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [CompletedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'CompletedComputerName') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [CompletedComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'SkippedAt') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [SkippedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'SkippedBy') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [SkippedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'SkipReason') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [SkipReason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'ReopenedAt') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [ReopenedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'ReopenedBy') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [ReopenedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'ReopenReason') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [ReopenReason] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'CreatedAt') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'CreatedBy') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'UpdatedAt') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestRequestSteps', 'UpdatedBy') IS NULL ALTER TABLE dbo.[TestRequestSteps] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.PackageMeterControls', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[PackageMeterControls] (
    [ControlId] NVARCHAR(450) NULL,
    [Status] NVARCHAR(MAX) NULL,
    [MeterModel] NVARCHAR(MAX) NULL,
    [PulseCount] NVARCHAR(MAX) NULL,
    [Customer] NVARCHAR(MAX) NULL,
    [ControlDate] NVARCHAR(MAX) NULL,
    [OperatorInfo] NVARCHAR(MAX) NULL,
    [ControllerName] NVARCHAR(MAX) NULL,
    [ProductionPanelNo] NVARCHAR(MAX) NULL,
    [ControlPanelNo] NVARCHAR(MAX) NULL,
    [IsSmartMeter] NVARCHAR(MAX) NULL,
    [ReferenceFlowQ4] NVARCHAR(MAX) NULL,
    [ReferenceFlowQ3] NVARCHAR(MAX) NULL,
    [ReferenceFlowQ2] NVARCHAR(MAX) NULL,
    [ReferenceFlowQ1] NVARCHAR(MAX) NULL,
    [RangeValue] NVARCHAR(MAX) NULL,
    [Explanation] NVARCHAR(MAX) NULL,
    [MeterCount] NVARCHAR(MAX) NULL,
    [SuitableCount] NVARCHAR(MAX) NULL,
    [UnsuitableCount] NVARCHAR(MAX) NULL,
    [IncompleteCount] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [CreatedComputerName] NVARCHAR(MAX) NULL,
    [CompletedAt] NVARCHAR(MAX) NULL,
    [CompletedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.PackageMeterControls', 'ControlId') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ControlId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'Status') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [Status] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'MeterModel') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [MeterModel] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'PulseCount') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [PulseCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'Customer') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [Customer] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'ControlDate') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ControlDate] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'OperatorInfo') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [OperatorInfo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'ControllerName') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ControllerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'ProductionPanelNo') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ProductionPanelNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'ControlPanelNo') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ControlPanelNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'IsSmartMeter') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [IsSmartMeter] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'ReferenceFlowQ4') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ReferenceFlowQ4] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'ReferenceFlowQ3') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ReferenceFlowQ3] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'ReferenceFlowQ2') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ReferenceFlowQ2] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'ReferenceFlowQ1') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [ReferenceFlowQ1] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'RangeValue') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [RangeValue] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'Explanation') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [Explanation] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'MeterCount') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [MeterCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'SuitableCount') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [SuitableCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'UnsuitableCount') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [UnsuitableCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'IncompleteCount') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [IncompleteCount] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'CreatedAt') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'CreatedBy') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'CreatedComputerName') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [CreatedComputerName] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'CompletedAt') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [CompletedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'CompletedBy') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [CompletedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'UpdatedAt') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControls', 'UpdatedBy') IS NULL ALTER TABLE dbo.[PackageMeterControls] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.PackageMeterControlLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[PackageMeterControlLines] (
    [ControlId] NVARCHAR(450) NULL,
    [LineId] NVARCHAR(450) NULL,
    [SortNo] NVARCHAR(MAX) NULL,
    [SerialNumber] NVARCHAR(MAX) NULL,
    [LabelErrorQ3] NVARCHAR(MAX) NULL,
    [LabelErrorQ2] NVARCHAR(MAX) NULL,
    [LabelErrorQ1] NVARCHAR(MAX) NULL,
    [TestFlowQ4Manual] NVARCHAR(MAX) NULL,
    [TestFlowQ3] NVARCHAR(MAX) NULL,
    [TestFlowQ2] NVARCHAR(MAX) NULL,
    [TestFlowQ1] NVARCHAR(MAX) NULL,
    [CreditResult] NVARCHAR(MAX) NULL,
    [ValveResult] NVARCHAR(MAX) NULL,
    [OverallResult] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.PackageMeterControlLines', 'ControlId') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [ControlId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'LineId') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [LineId] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'SortNo') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [SortNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'SerialNumber') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [SerialNumber] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'LabelErrorQ3') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [LabelErrorQ3] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'LabelErrorQ2') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [LabelErrorQ2] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'LabelErrorQ1') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [LabelErrorQ1] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'TestFlowQ4Manual') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [TestFlowQ4Manual] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'TestFlowQ3') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [TestFlowQ3] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'TestFlowQ2') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [TestFlowQ2] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'TestFlowQ1') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [TestFlowQ1] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'CreditResult') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [CreditResult] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'ValveResult') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [ValveResult] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'OverallResult') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [OverallResult] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'CreatedAt') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'CreatedBy') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'UpdatedAt') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.PackageMeterControlLines', 'UpdatedBy') IS NULL ALTER TABLE dbo.[PackageMeterControlLines] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.TestCatalog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[TestCatalog] (
    [TestName] NVARCHAR(450) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [IsActive] NVARCHAR(MAX) NULL,
    [SortNo] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.TestCatalog', 'TestName') IS NULL ALTER TABLE dbo.[TestCatalog] ADD [TestName] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.TestCatalog', 'Description') IS NULL ALTER TABLE dbo.[TestCatalog] ADD [Description] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestCatalog', 'IsActive') IS NULL ALTER TABLE dbo.[TestCatalog] ADD [IsActive] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestCatalog', 'SortNo') IS NULL ALTER TABLE dbo.[TestCatalog] ADD [SortNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestCatalog', 'CreatedBy') IS NULL ALTER TABLE dbo.[TestCatalog] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestCatalog', 'CreatedAt') IS NULL ALTER TABLE dbo.[TestCatalog] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestCatalog', 'UpdatedBy') IS NULL ALTER TABLE dbo.[TestCatalog] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestCatalog', 'UpdatedAt') IS NULL ALTER TABLE dbo.[TestCatalog] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.TestGroups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[TestGroups] (
    [GroupName] NVARCHAR(450) NULL,
    [TestsText] NVARCHAR(MAX) NULL,
    [IsActive] NVARCHAR(MAX) NULL,
    [SortNo] NVARCHAR(MAX) NULL,
    [CreatedBy] NVARCHAR(MAX) NULL,
    [CreatedAt] NVARCHAR(MAX) NULL,
    [UpdatedBy] NVARCHAR(MAX) NULL,
    [UpdatedAt] NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('dbo.TestGroups', 'GroupName') IS NULL ALTER TABLE dbo.[TestGroups] ADD [GroupName] NVARCHAR(450) NULL;
IF COL_LENGTH('dbo.TestGroups', 'TestsText') IS NULL ALTER TABLE dbo.[TestGroups] ADD [TestsText] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestGroups', 'IsActive') IS NULL ALTER TABLE dbo.[TestGroups] ADD [IsActive] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestGroups', 'SortNo') IS NULL ALTER TABLE dbo.[TestGroups] ADD [SortNo] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestGroups', 'CreatedBy') IS NULL ALTER TABLE dbo.[TestGroups] ADD [CreatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestGroups', 'CreatedAt') IS NULL ALTER TABLE dbo.[TestGroups] ADD [CreatedAt] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestGroups', 'UpdatedBy') IS NULL ALTER TABLE dbo.[TestGroups] ADD [UpdatedBy] NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.TestGroups', 'UpdatedAt') IS NULL ALTER TABLE dbo.[TestGroups] ADD [UpdatedAt] NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Username' AND object_id = OBJECT_ID(N'dbo.Users'))
    CREATE INDEX [IX_Users_Username] ON dbo.[Users]([Username]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_TrCode' AND object_id = OBJECT_ID(N'dbo.Products'))
    CREATE INDEX [IX_Products_TrCode] ON dbo.[Products]([TrCode]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ControlPoints_TrCode' AND object_id = OBJECT_ID(N'dbo.ControlPoints'))
    CREATE INDEX [IX_ControlPoints_TrCode] ON dbo.[ControlPoints]([TrCode]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MeasurementRecords_RecordId' AND object_id = OBJECT_ID(N'dbo.MeasurementRecords'))
    CREATE INDEX [IX_MeasurementRecords_RecordId] ON dbo.[MeasurementRecords]([RecordId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductionTickets_TicketId' AND object_id = OBJECT_ID(N'dbo.ProductionTickets'))
    CREATE INDEX [IX_ProductionTickets_TicketId] ON dbo.[ProductionTickets]([TicketId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MoldTickets_MoldTicketId' AND object_id = OBJECT_ID(N'dbo.MoldTickets'))
    CREATE INDEX [IX_MoldTickets_MoldTicketId] ON dbo.[MoldTickets]([MoldTicketId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MoldBindingRecords_BindingId' AND object_id = OBJECT_ID(N'dbo.MoldBindingRecords'))
    CREATE INDEX [IX_MoldBindingRecords_BindingId] ON dbo.[MoldBindingRecords]([BindingId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QualityToProductionTickets_TicketId' AND object_id = OBJECT_ID(N'dbo.QualityToProductionTickets'))
    CREATE INDEX [IX_QualityToProductionTickets_TicketId] ON dbo.[QualityToProductionTickets]([TicketId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MoldConnectionPlan_PlanId' AND object_id = OBJECT_ID(N'dbo.MoldConnectionPlan'))
    CREATE INDEX [IX_MoldConnectionPlan_PlanId] ON dbo.[MoldConnectionPlan]([PlanId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MechanismQualityControlRecords_ControlId' AND object_id = OBJECT_ID(N'dbo.MechanismQualityControlRecords'))
    CREATE INDEX [IX_MechanismQualityControlRecords_ControlId] ON dbo.[MechanismQualityControlRecords]([ControlId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PlasticShiftTrackingRecords_RecordId' AND object_id = OBJECT_ID(N'dbo.PlasticShiftTrackingRecords'))
    CREATE INDEX [IX_PlasticShiftTrackingRecords_RecordId] ON dbo.[PlasticShiftTrackingRecords]([RecordId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PlasticShiftEmailRecipients_Email' AND object_id = OBJECT_ID(N'dbo.PlasticShiftEmailRecipients'))
    CREATE INDEX [IX_PlasticShiftEmailRecipients_Email] ON dbo.[PlasticShiftEmailRecipients]([Email]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TestRequestEmailRecipients_Email' AND object_id = OBJECT_ID(N'dbo.TestRequestEmailRecipients'))
    CREATE INDEX [IX_TestRequestEmailRecipients_Email] ON dbo.[TestRequestEmailRecipients]([Email]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TestRequestRecords_RequestId' AND object_id = OBJECT_ID(N'dbo.TestRequestRecords'))
    CREATE INDEX [IX_TestRequestRecords_RequestId] ON dbo.[TestRequestRecords]([RequestId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TestRequestSteps_RequestId' AND object_id = OBJECT_ID(N'dbo.TestRequestSteps'))
    CREATE INDEX [IX_TestRequestSteps_RequestId] ON dbo.[TestRequestSteps]([RequestId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PackageMeterControls_ControlId' AND object_id = OBJECT_ID(N'dbo.PackageMeterControls'))
    CREATE INDEX [IX_PackageMeterControls_ControlId] ON dbo.[PackageMeterControls]([ControlId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PackageMeterControlLines_ControlId' AND object_id = OBJECT_ID(N'dbo.PackageMeterControlLines'))
    CREATE INDEX [IX_PackageMeterControlLines_ControlId] ON dbo.[PackageMeterControlLines]([ControlId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TestCatalog_TestName' AND object_id = OBJECT_ID(N'dbo.TestCatalog'))
    CREATE INDEX [IX_TestCatalog_TestName] ON dbo.[TestCatalog]([TestName]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TestGroups_GroupName' AND object_id = OBJECT_ID(N'dbo.TestGroups'))
    CREATE INDEX [IX_TestGroups_GroupName] ON dbo.[TestGroups]([GroupName]);
GO
