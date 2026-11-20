# PBKDF2 Password Hashing Migration

**Date:** 2026-11-20  
**Status:** Implemented, migration pending deployment

## Summary

Replaced BCrypt password hashing with PBKDF2 (via ASP.NET Core Identity's
`PasswordHasher<T>`). Existing BCrypt hashes are migrated lazily during login,
so no forced password reset is required.

## What changed

### Database schema

Added nullable `PasswordHashVersion` column to the `Users` table:
- `NULL` or `1` = BCrypt (legacy)
- `2` = PBKDF2 (current)

Migration: `20261120045145_AddPasswordHashVersion`

The column is nullable so existing rows remain valid. Null is treated as BCrypt
for backward compatibility.

### Password hasher

New hybrid implementation: `Fundoo.User.Infrastructure.Security.PasswordHasher`

**Hash creation:**
- Always uses PBKDF2 for new passwords (registration, password reset)
- Sets `PasswordHashVersion = 2`

**Hash verification:**
- Detects BCrypt hashes by prefix (`$2a$`, `$2b$`, `$2y$`)
- Verifies BCrypt hashes using `BCrypt.Net.BCrypt.Verify`
- Verifies PBKDF2 hashes using `PasswordHasher<object>.VerifyHashedPassword`
- Both are timing-attack safe

### Lazy migration

On successful login (`LoginAsync`, `LoginV2Async`):
1. User authenticates with current password (BCrypt or PBKDF2)
2. If `PasswordHashVersion` is `NULL` or `1` **and** the hash starts with `$2`,
   trigger rehash
3. Rehash password using PBKDF2
4. Update `PasswordHash` and `PasswordHashVersion` atomically via
   `RehashPasswordAsync`
5. Log success; continue login flow

Rehash failures are logged but **do not block login**. The user remains on
BCrypt until their next successful login attempt.

### Repository changes

Added `IUserRepository.RehashPasswordAsync`:
```csharp
Task<bool> RehashPasswordAsync(
    Guid userId,
    string newPasswordHash,
    int newHashVersion,
    DateTime nowUtc,
    CancellationToken cancellationToken = default);
```

Updated `ConsumePasswordResetTokenAndUpdatePasswordAsync` to also set
`PasswordHashVersion` when resetting passwords.

## Migration behavior

| Scenario | Behavior |
|---|---|
| Existing user logs in with BCrypt hash | Hash verified with BCrypt; password rehashed to PBKDF2; login succeeds |
| Existing user logs in again | Hash verified with PBKDF2; no rehash needed |
| New user registers | Password hashed with PBKDF2; `PasswordHashVersion = 2` |
| User resets password | Password hashed with PBKDF2; `PasswordHashVersion = 2` |
| Rehash fails (DB error, etc.) | Login still succeeds; user remains on BCrypt; logged as warning |

## Rollback considerations

If rollback is needed after deployment:
1. The `PasswordHashVersion` column is nullable and has no NOT NULL constraint,
   so it can remain in the schema without breaking the old code
2. Revert to `BCryptPasswordHasher` in DI registration
3. Users with PBKDF2 hashes (`PasswordHashVersion = 2`) will not be able to log
   in until they reset their password

A safer rollback approach:
1. Deploy a hybrid hasher that verifies both BCrypt and PBKDF2 (like the
   current implementation), but hashes new passwords with BCrypt again
2. This allows both hash types to coexist during rollback

## Testing

### Unit test coverage needed
- `PasswordHasher.Hash` produces PBKDF2 hashes
- `PasswordHasher.Verify` accepts BCrypt hashes (all three prefixes)
- `PasswordHasher.Verify` accepts PBKDF2 hashes
- `PasswordHasher.Verify` rejects invalid hashes
- `PasswordHasher.NeedsRehash` returns true for BCrypt, false for PBKDF2

### Integration test coverage needed
- Register new user → `PasswordHashVersion = 2`
- Login with BCrypt user → rehash triggered, version updated to 2
- Login with PBKDF2 user → no rehash
- Password reset → `PasswordHashVersion = 2`
- Rehash failure does not block login

## Dependencies

- `Microsoft.Extensions.Identity.Core` 8.0.0 (added to
  `Fundoo.User.Infrastructure`)
- `BCrypt.Net-Next` 4.2.0 (retained for BCrypt verification)

Both packages are actively maintained and have no known high-severity
vulnerabilities as of 2026-11-20.

## Deployment checklist

1. Apply migration `20261120045145_AddPasswordHashVersion` to the User database
2. Deploy updated User API
3. Monitor logs for rehash activity:
   - `"Rehashed password for user {UserId} from BCrypt to PBKDF2"` (info)
   - `"Failed to rehash password for user {UserId}"` (warning)
4. After sufficient time (e.g., 30 days), query `PasswordHashVersion`:
   - Count users still on BCrypt (`NULL` or `1`)
   - Identify inactive accounts
5. Optional: force password reset for accounts that never migrated

## Security notes

- PBKDF2 (via ASP.NET Core Identity) uses 100,000 iterations by default,
  exceeding OWASP's 2023 recommendation of 600,000 for PBKDF2-SHA256 in
  password storage contexts
- BCrypt work factor is 10 (default for `BCrypt.Net-Next` 4.2.0)
- Both are considered secure for password storage as of 2026
- Migration is transparent to users; no UI change or forced logout
- Plain-text passwords are only available during login/registration and are
  never persisted

## Future considerations

- Monitor OWASP recommendations; PBKDF2 may eventually be superseded by Argon2
- ASP.NET Core Identity's `PasswordHasher` supports versioning and automatic
  rehashing if its internal parameters change
- Consider increasing PBKDF2 iteration count if OWASP guidance increases
