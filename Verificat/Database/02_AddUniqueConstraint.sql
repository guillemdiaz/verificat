-- =============================================================================
-- Projecte:	Verificat - Eina d'encadenament de factures Verifactu
-- Arxiu:		02_AddUniqueConstraint.sql
-- Descripció:	Afegeix una restricció sobre (SerieFactura, NumeroFactura) per 
--              tal que no es creïn factures duplicades
-- Data:		Maig 2026
-- =============================================================================
USE VerificatDB;
GO

IF NOT EXISTS (
    SELECT * FROM sys.key_constraints 
    WHERE name = 'UQ_RegistresFacturacio_Serie_Numero'
)
BEGIN
    ALTER TABLE RegistresFacturacio
    ADD CONSTRAINT UQ_RegistresFacturacio_Serie_Numero 
        UNIQUE (SerieFactura, NumeroFactura);
    PRINT 'Constraint UQ afegit correctament';
END
GO