$query = @"
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- Disable all constraints
EXEC sp_MSforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT all";

-- Delete operational data
DELETE FROM Waivers;
DELETE FROM Payments;
DELETE FROM ProvidedDocuments;
DELETE FROM ProcessFeeDeposits;
DELETE FROM Penalties;
DELETE FROM Guarantors;
DELETE FROM Disbursements;
DELETE FROM Collaterals;
DELETE FROM Expenses;
DELETE FROM LoanApplications;
DELETE FROM Borrowers;
DELETE FROM Accounts;
DELETE FROM ActivityLogs;
DELETE FROM Addresses;

-- Delete settings data
DELETE FROM RequiredDocuments;
DELETE FROM Requirements;
DELETE FROM LoanProductSettings;
DELETE FROM LoanProducts;
DELETE FROM AccountTypes;
DELETE FROM BorrowerTypes;
DELETE FROM GuarantorTypes;
DELETE FROM PaymentModalities;
DELETE FROM PaymentTypes;
DELETE FROM Reasons;
DELETE FROM WaiverTypes;

-- Delete Persons not associated with Users
DELETE FROM Persons WHERE Id NOT IN (SELECT PersonId FROM Users WHERE PersonId IS NOT NULL);

-- Re-enable constraints
EXEC sp_MSforeachtable "ALTER TABLE ? WITH NOCHECK CHECK CONSTRAINT all";
"@

Set-Content -Path wipe_data.sql -Value $query
sqlcmd -S . -d DigitalLoanPlatForm -i wipe_data.sql
Remove-Item wipe_data.sql
