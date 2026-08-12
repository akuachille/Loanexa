-- Initial data for PaymentTypes
INSERT INTO [PaymentTypes] ([PaymentTypeName], [CreatedAt], [UpdatedAt], [IsActive]) VALUES
('Cash', GETDATE(), GETDATE(), 1),
('Bank Transfer', GETDATE(), GETDATE(), 1),
('Mobile Money', GETDATE(), GETDATE(), 1),
('Cheque', GETDATE(), GETDATE(), 1);

-- Initial data for Reasons
INSERT INTO [Reasons] ([Name], [IsActive]) VALUES
('Late Payment Penalty', 1),
('Default Penalty', 1),
('Returned Cheque Penalty', 1),
('Breach of Contract Penalty', 1);

-- Initial data for AccountTypes
INSERT INTO [AccountTypes] ([AccountTypeName]) VALUES
('Bank'),
('Cash'),
('Mobile Money'),
('Airtel Money');

-- Initial data for BorrowerTypes
INSERT INTO [BorrowerTypes] ([Type], [Status]) VALUES
('Individual', 'Active'),
('Business', 'Active'),
('Group', 'Active');

-- Initial data for GuarantorTypes
INSERT INTO [GuarantorTypes] ([Name], [Status], [CreatedAt]) VALUES
('Individual', 'Active', GETDATE()),
('Business', 'Active', GETDATE()),
('Group', 'Active', GETDATE());

-- Initial data for PaymentModalities
INSERT INTO [PaymentModalities] ([Mode], [CreatedAt]) VALUES
('Daily', GETDATE()),
('Weekly', GETDATE()),
('Bi-Weekly', GETDATE()),
('Monthly', GETDATE()),
('Quarterly', GETDATE()),
('Annually', GETDATE());