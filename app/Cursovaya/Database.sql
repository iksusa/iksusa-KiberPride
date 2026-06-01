USE master;
GO

IF DB_ID(N'KiberPride') IS NULL
BEGIN
    CREATE DATABASE KiberPride;
END
GO

USE KiberPride;
GO

IF OBJECT_ID(N'dbo.Statuses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Statuses
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL UNIQUE
    );
END
GO

IF OBJECT_ID(N'dbo.SystemUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemUsers
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Login NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(200) NOT NULL,
        RoleName NVARCHAR(50) NOT NULL DEFAULT N'Администратор',
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clients
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Login NVARCHAR(100) NOT NULL UNIQUE,
        FullName NVARCHAR(150) NULL,
        Phone NVARCHAR(30) NULL,
        BalanceMoney DECIMAL(10,2) NOT NULL DEFAULT 0,
        BonusBalance INT NOT NULL DEFAULT 0,
        RemainingMinutes INT NOT NULL DEFAULT 0,
        RemainingSeconds INT NOT NULL DEFAULT 0,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

IF OBJECT_ID(N'dbo.SubscriptionTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionTypes
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE,
        Description NVARCHAR(200) NULL
    );
END
GO

IF OBJECT_ID(N'dbo.Tariffs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tariffs
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Price DECIMAL(10,2) NOT NULL,
        DurationMinutes INT NOT NULL,
        Description NVARCHAR(200) NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID(N'dbo.Subscriptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Subscriptions
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Price DECIMAL(10,2) NOT NULL,
        DurationDays INT NOT NULL,
        HoursCount INT NOT NULL,
        Description NVARCHAR(200) NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID(N'dbo.Computers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Computers
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO

IF OBJECT_ID(N'dbo.IssuedSubscriptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IssuedSubscriptions
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClientId INT NOT NULL,
        SubscriptionId INT NOT NULL,
        ComputerId INT NULL,
        IssuedAt DATETIME NOT NULL DEFAULT GETDATE(),
        ValidUntil DATETIME NOT NULL,
        CONSTRAINT FK_IssuedSubscriptions_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_IssuedSubscriptions_Subscriptions FOREIGN KEY (SubscriptionId) REFERENCES dbo.Subscriptions(Id),
        CONSTRAINT FK_IssuedSubscriptions_Computers FOREIGN KEY (ComputerId) REFERENCES dbo.Computers(Id)
    );
END
GO

IF OBJECT_ID(N'dbo.Visits', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Visits
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClientId INT NOT NULL,
        ComputerId INT NULL,
        TariffId INT NULL,
        SubscriptionId INT NULL,
        StartTime DATETIME NOT NULL DEFAULT GETDATE(),
        EndTime DATETIME NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT N'Активно',
        CONSTRAINT FK_Visits_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_Visits_Computers FOREIGN KEY (ComputerId) REFERENCES dbo.Computers(Id),
        CONSTRAINT FK_Visits_Tariffs FOREIGN KEY (TariffId) REFERENCES dbo.Tariffs(Id),
        CONSTRAINT FK_Visits_Subscriptions FOREIGN KEY (SubscriptionId) REFERENCES dbo.Subscriptions(Id)
    );
END
GO

IF OBJECT_ID(N'dbo.ClientSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClientSessions
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClientId INT NOT NULL,
        ComputerId INT NULL,
        VisitId INT NULL,
        StartedAt DATETIME NOT NULL DEFAULT GETDATE(),
        EndAt DATETIME NULL,
        RemainingSeconds INT NOT NULL DEFAULT 0,
        Status NVARCHAR(50) NOT NULL DEFAULT N'Активно',
        CONSTRAINT FK_ClientSessions_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_ClientSessions_Computers FOREIGN KEY (ComputerId) REFERENCES dbo.Computers(Id),
        CONSTRAINT FK_ClientSessions_Visits FOREIGN KEY (VisitId) REFERENCES dbo.Visits(Id)
    );
END
GO

IF OBJECT_ID(N'dbo.BonusOperations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BonusOperations
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClientId INT NOT NULL,
        OperationDate DATETIME NOT NULL DEFAULT GETDATE(),
        Reason NVARCHAR(200) NULL,
        Amount INT NOT NULL,
        OperationType NVARCHAR(50) NULL,
        CONSTRAINT FK_BonusOperations_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id)
    );
END
GO

IF OBJECT_ID(N'dbo.Sales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClientId INT NOT NULL,
        SubscriptionId INT NULL,
        TariffId INT NULL,
        ComputerId INT NULL,
        SaleDate DATETIME NOT NULL DEFAULT GETDATE(),
        Amount DECIMAL(10,2) NOT NULL,
        PaymentType NVARCHAR(50) NULL,
        MinutesAdded INT NOT NULL DEFAULT 0,
        Comment NVARCHAR(200) NULL,
        CONSTRAINT FK_Sales_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_Sales_Subscriptions FOREIGN KEY (SubscriptionId) REFERENCES dbo.Subscriptions(Id),
        CONSTRAINT FK_Sales_Tariffs FOREIGN KEY (TariffId) REFERENCES dbo.Tariffs(Id),
        CONSTRAINT FK_Sales_Computers FOREIGN KEY (ComputerId) REFERENCES dbo.Computers(Id)
    );
END
GO

IF OBJECT_ID(N'dbo.ClientOperations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClientOperations
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClientId INT NOT NULL,
        OperationDate DATETIME NOT NULL DEFAULT GETDATE(),
        OperationType NVARCHAR(100) NOT NULL,
        AmountMoney DECIMAL(10,2) NOT NULL DEFAULT 0,
        AmountBonus INT NOT NULL DEFAULT 0,
        MinutesChanged INT NOT NULL DEFAULT 0,
        Comment NVARCHAR(300) NULL,
        CONSTRAINT FK_ClientOperations_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id)
    );
END
GO

-- Безопасное добавление недостающих колонок в уже созданную базу
IF COL_LENGTH('dbo.Clients', 'BalanceMoney') IS NULL ALTER TABLE dbo.Clients ADD BalanceMoney DECIMAL(10,2) NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Clients', 'BonusBalance') IS NULL ALTER TABLE dbo.Clients ADD BonusBalance INT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Clients', 'RemainingMinutes') IS NULL ALTER TABLE dbo.Clients ADD RemainingMinutes INT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Clients', 'RemainingSeconds') IS NULL ALTER TABLE dbo.Clients ADD RemainingSeconds INT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Clients', 'IsDeleted') IS NULL ALTER TABLE dbo.Clients ADD IsDeleted BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Tariffs', 'IsDeleted') IS NULL ALTER TABLE dbo.Tariffs ADD IsDeleted BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Subscriptions', 'IsDeleted') IS NULL ALTER TABLE dbo.Subscriptions ADD IsDeleted BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Visits', 'ComputerId') IS NULL ALTER TABLE dbo.Visits ADD ComputerId INT NULL;
IF COL_LENGTH('dbo.Visits', 'TariffId') IS NULL ALTER TABLE dbo.Visits ADD TariffId INT NULL;
IF COL_LENGTH('dbo.Visits', 'SubscriptionId') IS NULL ALTER TABLE dbo.Visits ADD SubscriptionId INT NULL;
IF COL_LENGTH('dbo.Visits', 'EndTime') IS NULL ALTER TABLE dbo.Visits ADD EndTime DATETIME NULL;
IF COL_LENGTH('dbo.Visits', 'Status') IS NULL ALTER TABLE dbo.Visits ADD Status NVARCHAR(50) NOT NULL DEFAULT N'Активно';
IF COL_LENGTH('dbo.Sales', 'ComputerId') IS NULL ALTER TABLE dbo.Sales ADD ComputerId INT NULL;
IF COL_LENGTH('dbo.Sales', 'PaymentType') IS NULL ALTER TABLE dbo.Sales ADD PaymentType NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.Sales', 'MinutesAdded') IS NULL ALTER TABLE dbo.Sales ADD MinutesAdded INT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.Sales', 'Comment') IS NULL ALTER TABLE dbo.Sales ADD Comment NVARCHAR(200) NULL;
GO

UPDATE dbo.Clients
SET RemainingSeconds = RemainingMinutes * 60
WHERE ISNULL(RemainingSeconds,0)=0 AND ISNULL(RemainingMinutes,0)>0;
GO

-- Стартовые данные
IF NOT EXISTS (SELECT 1 FROM dbo.Statuses WHERE Name = N'Активно')
    INSERT INTO dbo.Statuses (Name) VALUES (N'Активно'),(N'Завершено'),(N'Отменено');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SystemUsers WHERE Login = N'admin')
    INSERT INTO dbo.SystemUsers (Login, PasswordHash, RoleName) VALUES (N'admin', N'admin', N'Старший администратор');
GO

UPDATE dbo.SystemUsers SET RoleName = N'Старший администратор' WHERE Login = N'admin';
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SystemUsers WHERE Login = N'operator')
    INSERT INTO dbo.SystemUsers (Login, PasswordHash, RoleName) VALUES (N'operator', N'operator', N'Администратор');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionTypes WHERE Name = N'Почасовой')
    INSERT INTO dbo.SubscriptionTypes (Name, Description) VALUES (N'Почасовой', N'Оплата по тарифу'), (N'Абонемент', N'Пакет часов на срок');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Computers)
    INSERT INTO dbo.Computers (Name, IsActive) VALUES (N'ПК-1',1),(N'ПК-2',1),(N'ПК-3',1),(N'ПК-4',1),(N'ПК-5',1),(N'ПК-6',1),(N'ПК-7',1),(N'ПК-8',1),(N'ПК-9',1),(N'ПК-10',1);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Tariffs WHERE Name = N'Стандарт')
    INSERT INTO dbo.Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'Стандарт', 170, 60, N'Стандартный тариф');
IF NOT EXISTS (SELECT 1 FROM dbo.Tariffs WHERE Name = N'VIP')
    INSERT INTO dbo.Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'VIP', 210, 60, N'VIP тариф');
IF NOT EXISTS (SELECT 1 FROM dbo.Tariffs WHERE Name = N'HomeVIP')
    INSERT INTO dbo.Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'HomeVIP', 230, 60, N'Домашний VIP тариф');
IF NOT EXISTS (SELECT 1 FROM dbo.Tariffs WHERE Name = N'DUO')
    INSERT INTO dbo.Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'DUO', 400, 60, N'Тариф для двоих');
IF NOT EXISTS (SELECT 1 FROM dbo.Tariffs WHERE Name = N'TRIO')
    INSERT INTO dbo.Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'TRIO', 210, 60, N'Тариф для троих');
IF NOT EXISTS (SELECT 1 FROM dbo.Tariffs WHERE Name = N'SOLO')
    INSERT INTO dbo.Tariffs (Name, Price, DurationMinutes, Description) VALUES (N'SOLO', 250, 60, N'Индивидуальный тариф');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Subscriptions WHERE Name = N'Абонемент на 5 часов')
    INSERT INTO dbo.Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (N'Абонемент на 5 часов', 1500, 30, 5, N'Пакет на 5 часов');
IF NOT EXISTS (SELECT 1 FROM dbo.Subscriptions WHERE Name = N'Абонемент на 3 часа')
    INSERT INTO dbo.Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (N'Абонемент на 3 часа', 1000, 30, 3, N'Пакет на 3 часа');
IF NOT EXISTS (SELECT 1 FROM dbo.Subscriptions WHERE Name = N'Ночной')
    INSERT INTO dbo.Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (N'Ночной', 2000, 30, 8, N'Ночной абонемент');
IF NOT EXISTS (SELECT 1 FROM dbo.Subscriptions WHERE Name = N'Дневной')
    INSERT INTO dbo.Subscriptions (Name, Price, DurationDays, HoursCount, Description) VALUES (N'Дневной', 1800, 30, 8, N'Дневной абонемент');
GO

SELECT name FROM sys.tables ORDER BY name;
GO

-- Дополнительно: таблица бонусного счёта для требований курсовой
IF OBJECT_ID(N'dbo.Bonuses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bonuses
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClientId INT NOT NULL,
        Balance INT NOT NULL DEFAULT 0,
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Bonuses_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id)
    );
END
GO

INSERT INTO dbo.Bonuses (ClientId, Balance)
SELECT c.Id, ISNULL(c.BonusBalance,0)
FROM dbo.Clients c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Bonuses b WHERE b.ClientId = c.Id);
GO

SELECT name FROM sys.tables ORDER BY name;
GO
