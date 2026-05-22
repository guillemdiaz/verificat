-- =============================================================================
-- Projecte:	Verificat - Eina d'encadenament de factures Verifactu
-- Arxiu:		01_CreateSchema.sql
-- Descripció:	Crea la base de dades VerificatDB i la taula RegistresFacturacio
-- Data:		Maig 2026
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'VerificatDB')
BEGIN
	CREATE DATABASE VerificatDB;
END
GO

USE VerificatDB;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RegistresFacturacio')
BEGIN
	CREATE TABLE RegistresFacturacio(
		ID INT PRIMARY KEY IDENTITY(1,1),

		-- Identificació de la factura
		SerieFactura		NVARCHAR(20)	NOT NULL,				-- AEAT: SerieFactura
		NumeroFactura		NVARCHAR(60)	NOT NULL,				-- AEAT: NumSerieFactura
		DataExpedicio		DATETIME		NOT NULL,				-- AEAT: FechaExpedicionFactura
		NIFEmissor			NVARCHAR(20)	NOT NULL,				-- AEAT: NIF (IDEmisorFactura)
		
		-- Dades econòmiques
		ImportTotal			DECIMAL(12,2)	NOT NULL,				-- AEAT: ImporteTotal
		TipusImpositiu		DECIMAL(4,2)	NOT NULL,				-- AEAT: TipoImpositivo 
		QuotaIVA			DECIMAL(12,2)	NOT NULL,				-- AEAT: CuotaRepercutida
		
		-- Encadenament (Verifactu)
		PrimerRegistre		NVARCHAR(1)		NOT NULL DEFAULT 'N',	-- AEAT: PrimerRegistro (S/N)
		EmpremtaAnterior	NVARCHAR(64)	NOT NULL,				-- AEAT: Huella (registre anterior)
		Empremta			NVARCHAR(64)	NOT NULL				-- AEAT: Huella (aquest registre)
	);
	PRINT 'S''ha creat amb èxit la taula RegistresFacturacio';
END
ELSE
BEGIN
	PRINT 'La taula RegistresFacturacio ja existeix';
END
GO
