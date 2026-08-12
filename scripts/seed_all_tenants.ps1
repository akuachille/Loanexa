$query = @"
-- Insert PaymentTypes
INSERT INTO [PaymentTypes] ([PaymentTypeName], [CreatedAt], [UpdatedAt], [IsActive], [PersonId])
SELECT val.Name, GETDATE(), GETDATE(), 1, p.Id
FROM Persons p
JOIN Users u ON p.Id = u.PersonId
JOIN Tenants t ON u.TenantId = t.Id
CROSS JOIN (
    VALUES ('Cash'), ('Bank Transfer'), ('Mobile Money'), ('Cheque')
) AS val(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM PaymentTypes pt WHERE pt.PersonId = p.Id AND pt.PaymentTypeName = val.Name
);

-- Insert Reasons
INSERT INTO [Reasons] ([Name], [IsActive], [PersonId])
SELECT val.Name, 1, p.Id
FROM Persons p
JOIN Users u ON p.Id = u.PersonId
JOIN Tenants t ON u.TenantId = t.Id
CROSS JOIN (
    VALUES ('Late Payment Penalty'), ('Default Penalty'), ('Returned Cheque Penalty'), ('Breach of Contract Penalty')
) AS val(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM Reasons pt WHERE pt.PersonId = p.Id AND pt.Name = val.Name
);

-- Insert AccountTypes
INSERT INTO [AccountTypes] ([AccountTypeName], [PersonId])
SELECT val.Name, p.Id
FROM Persons p
JOIN Users u ON p.Id = u.PersonId
JOIN Tenants t ON u.TenantId = t.Id
CROSS JOIN (
    VALUES ('Bank'), ('Cash'), ('Mobile Money'), ('Airtel Money')
) AS val(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM AccountTypes pt WHERE pt.PersonId = p.Id AND pt.AccountTypeName = val.Name
);

-- Insert BorrowerTypes
INSERT INTO [BorrowerTypes] ([Type], [Status], [PersonId])
SELECT val.Name, 'Active', p.Id
FROM Persons p
JOIN Users u ON p.Id = u.PersonId
JOIN Tenants t ON u.TenantId = t.Id
CROSS JOIN (
    VALUES ('Individual'), ('Business'), ('Group')
) AS val(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM BorrowerTypes pt WHERE pt.PersonId = p.Id AND pt.Type = val.Name
);

-- Insert GuarantorTypes
INSERT INTO [GuarantorTypes] ([Name], [Status], [CreatedAt], [PersonId])
SELECT val.Name, 'Active', GETDATE(), p.Id
FROM Persons p
JOIN Users u ON p.Id = u.PersonId
JOIN Tenants t ON u.TenantId = t.Id
CROSS JOIN (
    VALUES ('Individual'), ('Business'), ('Group')
) AS val(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM GuarantorTypes pt WHERE pt.PersonId = p.Id AND pt.Name = val.Name
);

-- Insert PaymentModalities
INSERT INTO [PaymentModalities] ([Mode], [CreatedAt], [PersonId])
SELECT val.Name, GETDATE(), p.Id
FROM Persons p
JOIN Users u ON p.Id = u.PersonId
JOIN Tenants t ON u.TenantId = t.Id
CROSS JOIN (
    VALUES ('Daily'), ('Weekly'), ('Bi-Weekly'), ('Monthly'), ('Quarterly'), ('Annually')
) AS val(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM PaymentModalities pt WHERE pt.PersonId = p.Id AND pt.Mode = val.Name
);
"@

Set-Content -Path seed_all_tenants.sql -Value $query
sqlcmd -S . -d DigitalLoanPlatForm -i seed_all_tenants.sql
Remove-Item seed_all_tenants.sql
