-- Manual SQL script to add IsAdmin column to AspNetUsers in Azure SQL Database
-- Use this if automatic migrations fail due to FK conflicts

-- Check if column already exists
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') 
    AND name = 'IsAdmin'
)
BEGIN
    -- Add IsAdmin column with default value
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [IsAdmin] bit NOT NULL DEFAULT 0;
    
    PRINT 'Column IsAdmin added successfully to AspNetUsers table';
END
ELSE
BEGIN
    PRINT 'Column IsAdmin already exists in AspNetUsers table';
END
GO
