INSERT OR IGNORE INTO Users (Id, Username, PasswordSalt, PasswordHash, FullName)
VALUES (
    1,
    'demo',
    'demo-salt-64526',
    '98b42914ad06b93c94bd929c75e3dc579dc2206c323497ed3d96026b64fbc511',
    'Demo User'
);

INSERT OR IGNORE INTO Balances (UserId, CurrencyCode, Amount)
VALUES
    (1, 'PLN', 10000),
    (1, 'USD', 50),
    (1, 'EUR', 25);
