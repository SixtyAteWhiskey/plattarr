# Password work-factor candidate — 2026-11-03

The pre-public release review found that Forms passwords used 10,000
PBKDF2-HMAC-SHA512 iterations. This slice raises the work factor for new
passwords to 220,000 iterations, matching the current
[OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2)
guidance for that algorithm.

Existing salted hashes continue to verify with the iteration count stored in
each user row. A successful login rehashes a lower-work-factor password with a
fresh salt and saves it; a failed login leaves the stored hash unchanged. The
existing unsalted SHA-256 migration path also writes the new work factor after
a successful login. No database migration, forced reset, or production
credential access is needed. Accounts that do not log in retain their older
hash until a later successful login or password change.

The focused `UserServiceFixture` covers new hashes, wrong passwords, a
10,000-iteration stored hash, the legacy unsalted format, and one-time upgrade
on successful login. The nonincremental Release solution build passed with
zero warnings and errors; API tests passed 221, Host tests passed 100 with five
skipped, and Core tests passed 10,951 with 53 skipped. The focused
authentication and qBittorrent/Hygiene fixture run passed all 146 tests.

This is a local code candidate; production has not been changed. Login
latency on the production host has not been measured, and rollout remains a
separate decision.
